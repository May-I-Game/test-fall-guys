using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;

public class Connection : MonoBehaviour
{
    [Header("WS")]
    [SerializeField] string url = "ws://localhost:8080/ws";
    WebSocket websocket;

    [Header("Player (optional)")]
    [SerializeField] Transform player;   // 없으면 이 컴포넌트의 transform 사용

    // --- JSON 직렬화용 단순 벡터 ---
    [Serializable]
    public struct Vec3 { public float x, y, z; }
    static Vec3 V3(Vector3 v) => new Vec3 { x = v.x, y = v.y, z = v.z };

    // (선택) 최초 1회 join은 유지하고 싶으면 사용
    [Serializable]
    public class JoinMsg
    {
        public string type = "join";
        public string playerId;
        public string platform;
    }

    async void Start()
    {
        websocket = new WebSocket(url);

        websocket.OnOpen += () =>
        {
            Debug.Log("WS open");

            // 필요하면 최초 1회 join 전송 (서버 식별용)
            var join = new JoinMsg
            {
                playerId = SystemInfo.deviceUniqueIdentifier,
                platform = Application.platform.ToString()
            };
            websocket.SendText(JsonUtility.ToJson(join));
        };

        websocket.OnError += e => Debug.LogError("WS Error: " + e);
        websocket.OnClose += code => Debug.Log("WS close: " + code);
        websocket.OnMessage += bytes =>
        {
            var msg = Encoding.UTF8.GetString(bytes);
            Debug.Log("<< " + msg);
        };

        // 0.1초마다 위치만 전송
        InvokeRepeating(nameof(SendPosJson), 0f, 0.1f);

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    void SendPosJson()
    {
        if (websocket == null || websocket.State != WebSocketState.Open) return;

        // player가 없으면 이 객체의 transform 사용
        Transform t = player != null ? player : transform;

        Vec3 pos = V3(t.position);
        string json = JsonUtility.ToJson(pos); // {"x":..., "y":..., "z":...}
        websocket.SendText(json);
        // Debug.Log($">> {json}");
    }

    async void OnApplicationQuit()
    {
        if (websocket != null)
            await websocket.Close();
    }
}