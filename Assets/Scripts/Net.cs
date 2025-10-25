using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using NativeWebSocket;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI; // NavMeshAgent

public class Net : MonoBehaviour
{
    [Header("WebSocket")]
    [SerializeField] private string url = "ws://localhost:8080/ws";

    [Header("Target & UI")]
    [SerializeField] private Transform target;   // 내 로컬 Player
    [SerializeField] private Text netStateText;

    [Header("Replication (Player/Cube)")]
    [Tooltip("비워두면 Player/Cube 태그를 자동 수집(로컬만). Others는 원격 전용.")]
    [SerializeField] private Transform[] replicated;
    [SerializeField, Range(0.05f, 0.5f)] private float sendInterval = 0.1f;
    [SerializeField] private float posThreshold = 0.01f;
    [SerializeField] private float rotThresholdDeg = 1f;

    [Header("Auto Discover")]
    [SerializeField] private bool autoDiscover = true;
    [SerializeField] private float rescanInterval = 0.5f;

    [Header("Reliability / Resync")]
    [SerializeField] private bool forceAnnounceOnConnect = true;
    [SerializeField] private float periodicAnnounceInterval = 2f;

    [Header("Debug Log")]
    [SerializeField] private bool logOthersOnly = true;
    [SerializeField] private bool logRawIfNothing = true;
    [SerializeField] private bool logLifecycle = true;
    [SerializeField] private bool logOutgoing = true;

    [Header("Spawner")]
    [SerializeField] private CubeSpawner spawner;   // OthersPrefab / CubePrefab 지정 필수

    [Header("Remote Replica Filtering")]
    [Tooltip("원격 복제체에서 비활성화할 로컬 전용 스크립트 이름들")]
    [SerializeField]
    private string[] localOnlyBehaviourNames =
    {
        "PlayerController",
        "ThirdPersonController",
        "StarterAssetsInputs",
        "CubeJump",
        "PlayerInput"
    };

    private WebSocket ws;
    private bool shuttingDown;

    private WebSocketOpenEventHandler _onOpen;
    private WebSocketMessageEventHandler _onMessage;
    private WebSocketErrorEventHandler _onError;
    private WebSocketCloseEventHandler _onClose;

    private enum UIState { Disconnected, Connecting, Connected }
    private UIState uiState = UIState.Disconnected;

    private bool IsOpen => ws != null && ws.State == WebSocketState.Open;

    // ---- 상태 ----
    private readonly List<Transform> _rep = new();                 // 송신 대상(로컬만)
    private readonly Dictionary<Transform, string> _ids = new();   // 로컬 Transform -> 네트워크 ID
    private readonly Dictionary<string, GameObject> _world = new();// 원격 ID -> 생성된 GO

    private readonly Dictionary<Transform, Vector3> _lastPosD = new();
    private readonly Dictionary<Transform, Quaternion> _lastRotD = new();

    private readonly Queue<string> _inbox = new();
    private readonly object _inboxLock = new object();

    private const string PlayerTag = "Player";
    private const string CubeTag = "Cube";

    private static string _playerIdCache;
    private float _lastRecvTime = -1f;
    private float _nextScanTime;
    private float _nextAnnounceTime;

    // === 내부 네트 리플리카(원격 전용 표식) ===
    [DisallowMultipleComponent]
    private class NetReplica : MonoBehaviour
    {
        public string netId;
        public bool configured;
    }

    private void Awake() => Application.runInBackground = true;

    private void Update()
    {
        ws?.DispatchMessageQueue();

        // 로컬만 자동 재스캔 (원격 Others/큐브는 제외)
        if (autoDiscover && Time.time >= _nextScanTime)
        {
            _nextScanTime = Time.time + rescanInterval;
            DiscoverLocalByTags();
            PruneDestroyed();
        }

        // 수신 처리
        while (true)
        {
            string msg = null;
            lock (_inboxLock) if (_inbox.Count > 0) msg = _inbox.Dequeue();
            if (msg == null) break;

            var type = JsonType(msg);
            if (type == "cubes")
            {
                bool anyOthers = LogOthersFromJson(msg, logOthersOnly);
                if (!anyOthers && logRawIfNothing) Debug.Log("<< raw(no-others) " + msg);
                ApplyCubes(msg);
            }
            else
            {
                if (logLifecycle) Debug.Log("<< unknown type: " + msg);
            }
        }

        // 주기적 재발표
        if (IsOpen && periodicAnnounceInterval > 0f && Time.time >= _nextAnnounceTime)
        {
            _nextAnnounceTime = Time.time + periodicAnnounceInterval;
            _ = SendCubes(forceAll: true);
            if (logLifecycle) Debug.Log("[Net] periodic announce (forceAll)");
        }

        // 수신 없음 경고
        if (IsOpen && _lastRecvTime > 0f && (Time.realtimeSinceStartup - _lastRecvTime) > 5f)
        {
            if (logLifecycle) Debug.LogWarning("[Net] Connected but no incoming messages for 5s.");
            _lastRecvTime = Time.realtimeSinceStartup;
        }
    }

    public async void OnClickConnect()
    {
        if (uiState == UIState.Connecting) return;
        if (uiState == UIState.Connected) await DisconnectAsync();
        else await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        if (ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting)) return;

        SetUI(UIState.Connecting);
        ws = new WebSocket(url);
        SubscribeWsHandlers();

        try { await ws.Connect(); }
        catch (Exception ex) { Debug.LogException(ex); await DisconnectAsync(); }
    }

    private async Task DisconnectAsync()
    {
        CancelInvoke(nameof(SendCubesSafe));
        if (ws != null)
        {
            UnsubscribeWsHandlers();
            try { await ws.Close(); } catch { }
            ws = null;
        }
        SetUI(UIState.Disconnected);
    }

    private void SubscribeWsHandlers()
    {
        _onOpen = () =>
        {
            if (shuttingDown || this == null) return;

            SetUI(UIState.Connected);
            if (logLifecycle) Debug.Log("[Net] OnOpen");

            _rep.Clear();
            if (replicated != null && replicated.Length > 0) _rep.AddRange(replicated);
            DiscoverLocalByTags();    // Others는 추가 안 함
            PruneDestroyed();

            if (logLifecycle) Debug.Log($"[Net] replicated ready: count={_rep.Count}");
            InvokeRepeating(nameof(SendCubesSafe), 0f, sendInterval);
            _lastRecvTime = Time.realtimeSinceStartup;

            _nextAnnounceTime = Time.time + periodicAnnounceInterval;
            if (forceAnnounceOnConnect)
            {
                _ = SendCubes(forceAll: true);
                if (logLifecycle) Debug.Log("[Net] announce on connect (forceAll)");
            }
        };

        _onMessage = (bytes) =>
        {
            if (shuttingDown || this == null) return;
            _lastRecvTime = Time.realtimeSinceStartup;
            var msg = Encoding.UTF8.GetString(bytes);
            if (logLifecycle) Debug.Log($"[Net] OnMessage ({bytes.Length} bytes)");
            lock (_inboxLock) _inbox.Enqueue(msg);
        };

        _onError = (err) =>
        {
            if (shuttingDown || this == null) return;
            Debug.LogError("[Net] WS error: " + err);
            SetUI(UIState.Disconnected);
        };

        _onClose = (code) =>
        {
            if (shuttingDown || this == null) return;
            if (logLifecycle) Debug.LogWarning("[Net] OnClose: " + code);
            CancelInvoke(nameof(SendCubesSafe));
            SetUI(UIState.Disconnected);
        };

        ws.OnOpen += _onOpen;
        ws.OnMessage += _onMessage;
        ws.OnError += _onError;
        ws.OnClose += _onClose;
    }

    private void UnsubscribeWsHandlers()
    {
        if (ws == null) return;
        if (_onOpen != null) ws.OnOpen -= _onOpen;
        if (_onMessage != null) ws.OnMessage -= _onMessage;
        if (_onError != null) ws.OnError -= _onError;
        if (_onClose != null) ws.OnClose -= _onClose;
        _onOpen = null; _onMessage = null; _onError = null; _onClose = null;
    }

    // -------------------- Discover (로컬만) --------------------

    private bool IsRemoteTransform(Transform t)
        => t && t.GetComponentInParent<NetReplica>() != null;

    private void DiscoverLocalByTags()
    {
        if (target && !_rep.Contains(target) && !IsRemoteTransform(target)) _rep.Add(target);

        try
        {
            foreach (var go in GameObject.FindGameObjectsWithTag(PlayerTag))
            {
                if (!go) continue;
                var tr = go.transform;
                if (IsRemoteTransform(tr)) continue; // Others(원격)는 제외
                if (!_rep.Contains(tr)) _rep.Add(tr);
            }
        }
        catch (UnityException) { }

        try
        {
            foreach (var go in GameObject.FindGameObjectsWithTag(CubeTag))
            {
                if (!go) continue;
                var tr = go.transform;
                if (IsRemoteTransform(tr)) continue; // 원격 제외
                if (!_rep.Contains(tr)) _rep.Add(tr);
            }
        }
        catch (UnityException) { }
    }

    private void PruneDestroyed()
    {
        for (int i = _rep.Count - 1; i >= 0; i--) if (_rep[i] == null) _rep.RemoveAt(i);

        var rm = new List<Transform>();
        foreach (var kv in _lastPosD) if (kv.Key == null) rm.Add(kv.Key);
        foreach (var t in rm) _lastPosD.Remove(t);
        rm.Clear();
        foreach (var kv in _lastRotD) if (kv.Key == null) rm.Add(kv.Key);
        foreach (var t in rm) _lastRotD.Remove(t);
        rm.Clear();
        foreach (var kv in _ids) if (kv.Key == null) rm.Add(kv.Key);
        foreach (var t in rm) _ids.Remove(t);
    }

    // -------------------- Sending (로컬만 송신) --------------------

    private async void SendCubesSafe()
    {
        if (!IsOpen || shuttingDown) return;
        await SendCubes(false);
    }

    private async Task SendCubes(bool forceAll = false)
    {
        if (_rep.Count == 0) return;

        var list = new List<Cube>(_rep.Count);

        for (int i = 0; i < _rep.Count; i++)
        {
            var t = _rep[i];
            if (!t) continue;
            if (IsRemoteTransform(t)) continue; // Others/원격 큐브 송신 금지

            var id = GetOrMakeId(t);
            if (string.IsNullOrEmpty(id)) continue;

            var p = t.position;
            var r = t.rotation;

            _lastPosD.TryGetValue(t, out var lp);
            _lastRotD.TryGetValue(t, out var lr);

            bool noPos = !_lastPosD.ContainsKey(t);
            bool noRot = !_lastRotD.ContainsKey(t);

            bool posChanged = noPos || !Approximately(lp, p, posThreshold);
            bool rotChanged = noRot || Quaternion.Angle(lr, r) > rotThresholdDeg;

            if (forceAll || posChanged || rotChanged)
            {
                // ★ 서버 통신용 색 포함(렌더러에서 읽어와 hex로)
                string hex = ReadColorHex(t);

                list.Add(new Cube
                {
                    id = id,
                    color = hex,
                    x = Round3(p.x),
                    y = Round3(p.y),
                    z = Round3(p.z),
                    qx = Round3(r.x),
                    qy = Round3(r.y),
                    qz = Round3(r.z),
                    qw = Round3(r.w)
                });

                _lastPosD[t] = p;
                _lastRotD[t] = r;
            }
        }

        if (list.Count == 0) return;

        var batch = new CubeBatch { cubes = list.ToArray() };
        var json = JsonUtility.ToJson(batch);

        if (logOutgoing)
        {
#if UNITY_EDITOR
            Debug.Log(">> " + json);
#else
            Debug.Log($">> cubes={list.Count}, bytes={Encoding.UTF8.GetByteCount(json)}");
#endif
        }

        await SendText(json);
    }

    private Task SendText(string text)
        => IsOpen && !string.IsNullOrEmpty(text) ? ws.SendText(text) : Task.CompletedTask;

    // -------------------- Receiving (Others + Cube) --------------------

    private void ApplyCubes(string json)
    {
        var b = JsonUtility.FromJson<CubeBatch>(json);
        if (b?.cubes == null) return;

        foreach (var c in b.cubes)
        {
            if (IsMineId(c.id)) continue;

            bool isRemotePlayer = IsPlayerId(c.id); // 원격 플레이어는 'Others'로 취급
            GameObject go;

            if (!_world.TryGetValue(c.id, out go) || go == null)
            {
                // 씬에 남아있는 복제체 재사용(최신 API)
                var reps = UnityEngine.Object.FindObjectsByType<NetReplica>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < reps.Length; i++)
                {
                    if (reps[i] && reps[i].netId == c.id) { go = reps[i].gameObject; break; }
                }

                if (go == null)
                {
                    var pos = new Vector3(c.x, c.y, c.z);
                    var q = Normalize(new Quaternion(c.qx, c.qy, c.qz, c.qw));

                    if (spawner != null)
                    {
                        go = isRemotePlayer
                            ? spawner.SpawnNetworkOthers(pos, q, $"others [{c.id}]")
                            : spawner.SpawnNetworkCube(pos, q, $"cube [{c.id}]"); // color는 통신만, 적용 안 함
                    }
                    else
                    {
                        // 폴백(프리팹 없을 때)
                        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.name = (isRemotePlayer ? "others " : "cube ") + $"[{c.id}]";
                        go.transform.SetPositionAndRotation(pos, q);
                        go.tag = "Untagged";
                    }
                }

                var rep = go.GetComponent<NetReplica>(); if (!rep) rep = go.AddComponent<NetReplica>();
                rep.netId = c.id;

                if (!rep.configured)
                {
                    ConfigureAsRemoteReplica(go);
                    rep.configured = true;
                }

                _world[c.id] = go;
            }

            // 좌표/회전 업데이트 (색은 적용하지 않음)
            if (go != null)
            {
                var newPos = new Vector3(c.x, c.y, c.z);
                var newQ = Normalize(new Quaternion(c.qx, c.qy, c.qz, c.qw));
                go.transform.SetPositionAndRotation(newPos, newQ);
            }
        }
    }

    // ------- Remote replica: 로컬 입력/물리 차단 + 태그 제거 -------

    private void ConfigureAsRemoteReplica(GameObject go)
    {
        if (!go) return;

        // 태그 제거(계층)
        StripTagsRecursively(go.transform);

        // 로컬 전용 스크립트 비활성화
        if (localOnlyBehaviourNames != null && localOnlyBehaviourNames.Length > 0)
        {
            var mbs = go.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in mbs)
            {
                if (!mb) continue;
                var name = mb.GetType().Name;
                for (int i = 0; i < localOnlyBehaviourNames.Length; i++)
                {
                    if (string.Equals(name, localOnlyBehaviourNames[i], StringComparison.Ordinal))
                    {
                        mb.enabled = false;
                        break;
                    }
                }
            }
        }

        // 물리/이동 계열 차단 (정책상 원격은 변환만 업데이트)
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        foreach (var cc in go.GetComponentsInChildren<CharacterController>(true))
            cc.enabled = false;
        foreach (var ag in go.GetComponentsInChildren<NavMeshAgent>(true))
        {
            ag.enabled = false;
            ag.updatePosition = false;
            ag.updateRotation = false;
        }

        foreach (var anim in go.GetComponentsInChildren<Animator>(true))
            anim.applyRootMotion = false;
    }

    private void StripTagsRecursively(Transform root)
    {
        if (!root) return;
        root.tag = "Untagged";
        for (int i = 0; i < root.childCount; i++)
            StripTagsRecursively(root.GetChild(i));
    }

    // -------------------- Helpers --------------------

    private static bool IsPlayerId(string id)
        => !string.IsNullOrEmpty(id) && id.StartsWith("player:", StringComparison.Ordinal);

    private bool LogOthersFromJson(string json, bool onlyOthers)
    {
        var b = JsonUtility.FromJson<CubeBatch>(json);
        if (b?.cubes == null || b.cubes.Length == 0) return false;

        if (!onlyOthers) { Debug.Log("<< " + json); return true; }

        var others = new List<Cube>();
        foreach (var c in b.cubes) if (!IsMineId(c.id)) others.Add(c);
        if (others.Count == 0) return false;

#if UNITY_EDITOR
        var outBatch = new CubeBatch { cubes = others.ToArray() };
        Debug.Log("<< others " + JsonUtility.ToJson(outBatch));
#else
        Debug.Log($"<< others count={others.Count}");
#endif
        return true;
    }

    private bool IsMineId(string id)
    {
        string myPlayerId = $"player:{GetPlayerId()}";
        string myCubePrefix = $"cube:{GetPlayerId()}:";
        return id == myPlayerId || id.StartsWith(myCubePrefix, StringComparison.Ordinal);
    }

    private void SetUI(UIState state)
    {
        uiState = state;
        if (!netStateText) return;
        switch (state)
        {
            case UIState.Disconnected: netStateText.text = "Disconnected"; break;
            case UIState.Connecting: netStateText.text = "Connecting..."; break;
            case UIState.Connected: netStateText.text = "Connected"; break;
        }
    }

    private async void OnDisable() { shuttingDown = true; await DisconnectAsync(); }
    private async void OnApplicationQuit() { shuttingDown = true; await DisconnectAsync(); }

    private static string GetPlayerId()
    {
        if (!string.IsNullOrEmpty(_playerIdCache)) return _playerIdCache;

        const string K = "player_id_base";
        var baseId = PlayerPrefs.GetString(K, "");
        if (string.IsNullOrEmpty(baseId))
        {
            baseId = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(K, baseId);
            PlayerPrefs.Save();
        }

        var session = Guid.NewGuid().ToString("N").Substring(0, 8);
        _playerIdCache = $"{baseId}-{session}";
        Debug.Log($"[Net] MyPlayerId = {_playerIdCache}");
        return _playerIdCache;
    }

    private string GetOrMakeId(Transform t)
    {
        if (_ids.TryGetValue(t, out var id)) return id;
        if (IsRemoteTransform(t)) return null; // Others/원격 큐브는 ID 부여 금지(송신 제외)

        if (t.CompareTag(PlayerTag))
            id = $"player:{GetPlayerId()}";
        else if (t.CompareTag(CubeTag))
            id = $"cube:{GetPlayerId()}:{Guid.NewGuid():N}";
        else
            return null;

        _ids[t] = id;
        return id;
    }

    // ★ 렌더러의 현재 색을 #RRGGBB로 읽어 서버 통신에 포함
    private static string ReadColorHex(Transform t)
    {
        var rend = t ? t.GetComponentInChildren<Renderer>() : null;
        if (rend && rend.material)
        {
            if (rend.material.HasProperty("_BaseColor"))
            {
                var c = rend.material.GetColor("_BaseColor");
                return $"#{ColorUtility.ToHtmlStringRGB(c)}";
            }
            if (rend.material.HasProperty("_Color"))
            {
                var c = rend.material.GetColor("_Color");
                return $"#{ColorUtility.ToHtmlStringRGB(c)}";
            }
        }
        return "#FFFFFF";
    }

    private static string JsonType(string json)
    {
        const string k = "\"type\":\"";
        int i = json.IndexOf(k, StringComparison.Ordinal);
        if (i < 0) return "";
        i += k.Length;
        int j = json.IndexOf('"', i);
        return j > i ? json.Substring(i, j - i) : "";
    }

    private static bool Approximately(Vector3 a, Vector3 b, float eps)
        => (a - b).sqrMagnitude <= eps * eps;

    private static float Round3(float v) => (float)Math.Round(v, 3);

    private static Quaternion Normalize(Quaternion q)
    {
        var mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (mag > 0f)
        {
            float inv = 1f / mag;
            q.x *= inv; q.y *= inv; q.z *= inv; q.w *= inv;
        }
        else q = Quaternion.identity;
        return q;
    }

    // DTO
    [Serializable] public class CubeBatch { public string type = "cubes"; public Cube[] cubes; }
    [Serializable]
    public class Cube
    {
        public string id;     // "player:..." or "cube:...:guid"
        public string color;  // ★ 서버 통신용 색 (#RRGGBB)
        public float x, y, z;
        public float qx, qy, qz, qw;
    }
}
