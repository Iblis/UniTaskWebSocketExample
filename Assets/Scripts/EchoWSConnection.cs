using UnityEngine;
using UniTaskWebSocket;
using System.Net.WebSockets;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using System.Text;
using UnityEngine.UI;

public class EchoWSConnection : MonoBehaviour
{
    public String _echoHost;
    public TMPro.TMP_Text _echoMessage;
    public TMPro.TMP_InputField _messageToSend;
    public Button _sendButton;

    CancellationTokenSource _cancellation;
    IWebSocket _socket;
    
    void Start()
    {
        _ = Connect();
    }

    async UniTask Connect()
    {
        if (_cancellation != null)
        {
            throw new InvalidOperationException("Connection already established");
        }

        _cancellation = new CancellationTokenSource();

        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            _socket = new UniTaskWebSocket.WebGLWebSocket();
#else
            _socket = new WebSocketWrapper(new ClientWebSocket());
#endif
            await _socket.ConnectAsync(new Uri($"wss://{_echoHost}"), _cancellation.Token);
            _sendButton.onClick.AddListener(() => _socket.SendText(_messageToSend.text, _cancellation.Token));
            
            // Fire & Forget the next operations, thex are executed async
            _ = ReceiveText();
            _ = _socket.SendText("Echo received", _cancellation.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is WebSocketException)
        {
            _cancellation = null;
            Debug.Log($"Connection failed: {ex.Message}");
        }
    }

    async UniTaskVoid ReceiveText()
    {
        var buffer = new ArraySegment<byte>(new byte[1024]);
        while (!_cancellation.IsCancellationRequested)
        {
            // Note that the received block might only be part of a larger message. If this applies in your scenario,
            // check the received.EndOfMessage and consider buffering the blocks until that property is true.
            var received = await _socket.ReceiveAsync(buffer, _cancellation.Token);
            if (received.MessageType == WebSocketMessageType.Text)
            {
                OnMessageReceived(Encoding.UTF8.GetString(buffer.Array, 0, received.Count));
            }
        }
    }

    private void OnMessageReceived(string message)
    {
        _echoMessage.text = message;
    }

    void OnDestroy()
    {
        _ = Disconnect();
    }

    public async UniTask Disconnect()
    {
        if (_cancellation != null)
        {
            _sendButton.onClick.RemoveAllListeners();
            _cancellation.Cancel();
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
        }
        _cancellation = null;
    }
}
