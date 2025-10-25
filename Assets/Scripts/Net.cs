using System;
using System.Text;
using System.Threading.Tasks;

using NativeWebSocket;

using UnityEngine;
using UnityEngine.UI;

public class Net : MonoBehaviour
{
    [Header("WebSocket")]
    [SerializeField] private string url = "ws://localhost:8080/ws";

    [Header("Target & UI")]
    [SerializeField] private Transform target;   // 없으면 this.transform
    [SerializeField] private Text netStateText;  // 버튼/라벨 텍스트

    private WebSocket ws;
    private bool shuttingDown;                   // 파괴/종료 플래그

    // NativeWebSocket 전용 델리게이트 타입으로 선언해야 -= 해제가 가능
    private WebSocketOpenEventHandler _onOpen;
    private WebSocketMessageEventHandler _onMessage;
    private WebSocketErrorEventHandler _onError;
    private WebSocketCloseEventHandler _onClose;

    private enum UIState { Disconnected, Connecting, Connected }
    private UIState uiState = UIState.Disconnected;

    private bool IsOpen => ws != null && ws.State == WebSocketState.Open;

    private void Awake() => Application.runInBackground = true;

    private void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ws?.DispatchMessageQueue(); // WebGL 필수
#endif
    }

    // 버튼에서 호출 (토글)
    public async void OnClickConnect()
    {
        if (uiState == UIState.Connecting) return;
        if (uiState == UIState.Connected) await DisconnectAsync();
        else await ConnectAsync();
    }

    // -------------------- Connect / Disconnect --------------------

    private async Task ConnectAsync()
    {
        if (ws != null && (ws.State == WebSocketState.Open || ws.State == WebSocketState.Connecting))
            return;

        SetUI(UIState.Connecting);
        ws = new WebSocket(url);

        SubscribeWsHandlers(); // 이벤트 구독

        try
        {
            await ws.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            await DisconnectAsync(); // 부분 정리 + UI 복구
        }
    }

    private async Task DisconnectAsync()
    {
        CancelInvoke(nameof(SendPosJsonSafe));

        if (ws != null)
        {
            UnsubscribeWsHandlers();          // 반드시 -= 로 해제
            try { await ws.Close(); } catch { /* ignore */ }
            ws = null;
        }

        SetUI(UIState.Disconnected);
    }

    // -------------------- WS Handlers --------------------

    private void SubscribeWsHandlers()
    {
        _onOpen = () =>
        {
            if (shuttingDown || this == null)
                return;

            SetUI(UIState.Connected);

            var join = new JoinMsg { playerId = GetPlayerId(), platform = Application.platform.ToString() };
            _ = SendText(JsonUtility.ToJson(join));

            // 연결된 뒤에 주기 송신 시작
            InvokeRepeating(nameof(SendPosJsonSafe), 0f, 0.1f);
        };

        _onMessage = (bytes) =>
        {
            if (shuttingDown || this == null) return;
            var msg = Encoding.UTF8.GetString(bytes);
            // TODO: 수신 처리
            // Debug.Log("<< " + msg);
        };

        _onError = (err) =>
        {
            if (shuttingDown || this == null) return;
            Debug.LogError("WS error: " + err);
            SetUI(UIState.Disconnected);
        };

        _onClose = (code) =>
        {
            if (shuttingDown || this == null) return;

            CancelInvoke(nameof(SendPosJsonSafe));
            SetUI(UIState.Disconnected);
            // 재연결을 원하면 여기서 백오프 로직 시작 가능
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

    // -------------------- Sending --------------------

    private async void SendPosJsonSafe()
    {
        if (!IsOpen || shuttingDown) return;
        await SendPosJson();
    }

    private async Task SendPosJson()
    {
        var t = target ? target : transform;
        var p = t.position;
        var payload = new PosMsg { x = p.x, y = p.y, z = p.z };
        await SendText(JsonUtility.ToJson(payload));
    }

    private Task SendText(string text)
        => IsOpen && !string.IsNullOrEmpty(text) ? ws.SendText(text) : Task.CompletedTask;

    // -------------------- UI / Lifecycle --------------------

    private void SetUI(UIState state)
    {
        uiState = state;
        if (!netStateText) return;

        // 버튼/라벨에 표시할 텍스트 (토글형)
        switch (state)
        {
            case UIState.Disconnected: netStateText.text = "Disconnected"; break;
            case UIState.Connecting: netStateText.text = "Connecting..."; break;
            case UIState.Connected: netStateText.text = "Connected"; break;
        }
    }

    // 파괴/비활성화/종료 경로: 콜백 무시 플래그 세우고 안전 정리
    private async void OnDisable() { shuttingDown = true; await DisconnectAsync(); }
    private async void OnApplicationQuit() { shuttingDown = true; await DisconnectAsync(); }

    // WebGL에선 deviceUniqueIdentifier가 비어있을 수 있으므로 GUID 저장/재사용
    private static string GetPlayerId()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        const string K = "player_id";
        if (!PlayerPrefs.HasKey(K)) PlayerPrefs.SetString(K, Guid.NewGuid().ToString("N"));
        return PlayerPrefs.GetString(K);
#else
        return string.IsNullOrEmpty(SystemInfo.deviceUniqueIdentifier)
            ? Guid.NewGuid().ToString("N")
            : SystemInfo.deviceUniqueIdentifier;
#endif
    }

    // -------------------- DTO --------------------

    [Serializable] public class JoinMsg { public string playerId; public string platform; }
    [Serializable] public class PosMsg { public float x, y, z; }
}
