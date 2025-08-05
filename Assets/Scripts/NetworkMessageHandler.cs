using UnityEngine;
using System;
using System.Collections.Generic;

// Message types for network communication
public enum MessageType
{
    // Lobby messages
    CreateLobby,
    JoinLobby,
    LeaveLobby,
    UpdateLobbyList,
    LobbyInfo,
    PlayerReady,
    StartGame,

    // Game messages
    MakeMove,
    OfferDraw,
    AcceptDraw,
    DeclineDraw,
    Resign,
    GameOver,
    RevealBoard,
    ChatMessage,

    // Connection messages
    Connect,
    Disconnect,
    Ping,
    Pong
}

[Serializable]
public class NetworkMessage
{
    public MessageType type;
    public string senderId;
    public string recipientId;
    public string lobbyId;
    public string data;
    public long timestamp;

    public NetworkMessage(MessageType msgType, string msgData = "")
    {
        type = msgType;
        data = msgData;
        timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
    }
}

[Serializable]
public class MoveMessage
{
    public string move;
    public int moveNumber;
    public float timeRemaining;
    public bool isCheck;
    public bool isCheckmate;
    public bool isStalemate;
}

public class NetworkMessageHandler : MonoBehaviour
{
    [Header("Network Settings")]
    public string serverUrl = "ws://localhost:8080";
    public float reconnectDelay = 3f;
    public float pingInterval = 30f;
    public int maxReconnectAttempts = 5;

    // Events
    public event Action<string> OnConnected;
    public event Action<string> OnDisconnected;
    public event Action<NetworkMessage> OnMessageReceived;
    public event Action<string> OnError;

    // Connection state
    private bool isConnected = false;
    private string clientId;
    private int reconnectAttempts = 0;
    private float lastPingTime;

    // Message queue for offline handling
    private Queue<NetworkMessage> messageQueue = new Queue<NetworkMessage>();

    // References
    private MultiplayerLobbyManager lobbyManager;
    private BlindfoldMultiplayerUI gameUI;

    void Start()
    {
        clientId = System.Guid.NewGuid().ToString();
        lobbyManager = GetComponent<MultiplayerLobbyManager>();

        // Initialize connection
        ConnectToServer();
    }

    void Update()
    {
        // Send ping to keep connection alive
        if (isConnected && Time.time - lastPingTime > pingInterval)
        {
            SendPing();
            lastPingTime = Time.time;
        }
    }

    #region Connection Management

    void ConnectToServer()
    {
        // This is where you would implement actual WebSocket or other network connection
        // For demonstration, we'll simulate a connection

        Debug.Log($"Attempting to connect to {serverUrl}...");

        // Simulate successful connection
        StartCoroutine(SimulateConnection());
    }

    System.Collections.IEnumerator SimulateConnection()
    {
        yield return new WaitForSeconds(1f);

        isConnected = true;
        reconnectAttempts = 0;

        Debug.Log($"Connected to server with ID: {clientId}");
        OnConnected?.Invoke(clientId);

        // Process any queued messages
        ProcessMessageQueue();
    }

    void Disconnect()
    {
        if (!isConnected) return;

        isConnected = false;
        Debug.Log("Disconnected from server");
        OnDisconnected?.Invoke("User initiated disconnect");
    }

    void HandleConnectionLost()
    {
        isConnected = false;
        OnDisconnected?.Invoke("Connection lost");

        if (reconnectAttempts < maxReconnectAttempts)
        {
            reconnectAttempts++;
            Debug.Log($"Attempting to reconnect... (Attempt {reconnectAttempts}/{maxReconnectAttempts})");
            Invoke(nameof(ConnectToServer), reconnectDelay);
        }
        else
        {
            OnError?.Invoke("Failed to reconnect after maximum attempts");
        }
    }

    #endregion

    #region Message Sending

    public void SendMessage(NetworkMessage message)
    {
        message.senderId = clientId;

        if (isConnected)
        {
            // Send message through network
            TransmitMessage(message);
        }
        else
        {
            // Queue message for later
            messageQueue.Enqueue(message);
            Debug.LogWarning("Not connected. Message queued for sending.");
        }
    }

    void TransmitMessage(NetworkMessage message)
    {
        // Convert message to JSON
        string json = JsonUtility.ToJson(message);

        // This is where you would actually send the message through your network solution
        Debug.Log($"Sending message: {json}");

        // For demonstration, simulate message echo for local testing
        if (Application.isEditor)
        {
            StartCoroutine(SimulateMessageEcho(message));
        }
    }

    System.Collections.IEnumerator SimulateMessageEcho(NetworkMessage message)
    {
        yield return new WaitForSeconds(0.1f);

        // Simulate receiving the message back for testing
        if (message.type == MessageType.MakeMove)
        {
            message.senderId = "opponent";
            ReceiveMessage(JsonUtility.ToJson(message));
        }
    }

    void ProcessMessageQueue()
    {
        while (messageQueue.Count > 0 && isConnected)
        {
            var message = messageQueue.Dequeue();
            TransmitMessage(message);
        }
    }

    #endregion

    #region Message Receiving

    void ReceiveMessage(string jsonMessage)
    {
        try
        {
            NetworkMessage message = JsonUtility.FromJson<NetworkMessage>(jsonMessage);

            // Don't process our own messages
            if (message.senderId == clientId) return;

            Debug.Log($"Received message: {message.type} from {message.senderId}");

            // Process message based on type
            ProcessMessage(message);

            // Notify listeners
            OnMessageReceived?.Invoke(message);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
            OnError?.Invoke($"Failed to process message: {e.Message}");
        }
    }

    void ProcessMessage(NetworkMessage message)
    {
        switch (message.type)
        {
            case MessageType.MakeMove:
                ProcessMoveMessage(message);
                break;

            case MessageType.ChatMessage:
                ProcessChatMessage(message);
                break;

            case MessageType.JoinLobby:
                ProcessJoinLobbyMessage(message);
                break;

            case MessageType.LeaveLobby:
                ProcessLeaveLobbyMessage(message);
                break;

            case MessageType.PlayerReady:
                ProcessPlayerReadyMessage(message);
                break;

            case MessageType.StartGame:
                ProcessStartGameMessage(message);
                break;

            case MessageType.OfferDraw:
                ProcessDrawOfferMessage(message);
                break;

            case MessageType.Resign:
                ProcessResignMessage(message);
                break;

            case MessageType.Ping:
                SendPong(message.senderId);
                break;

            case MessageType.Pong:
                // Connection is alive
                break;

            default:
                Debug.LogWarning($"Unhandled message type: {message.type}");
                break;
        }
    }

    #endregion

    #region Message Processors

    void ProcessMoveMessage(NetworkMessage message)
    {
        if (gameUI != null)
        {
            MoveMessage moveData = JsonUtility.FromJson<MoveMessage>(message.data);
            gameUI.OnRemoteMoveReceived(moveData.move);
        }
    }

    void ProcessChatMessage(NetworkMessage message)
    {
        if (lobbyManager != null)
        {
            // Display chat message in lobby
            Debug.Log($"Chat from {message.senderId}: {message.data}");
        }
    }

    void ProcessJoinLobbyMessage(NetworkMessage message)
    {
        // Handle player joining lobby
        Debug.Log($"Player {message.senderId} joined lobby {message.lobbyId}");
    }

    void ProcessLeaveLobbyMessage(NetworkMessage message)
    {
        // Handle player leaving lobby
        Debug.Log($"Player {message.senderId} left lobby {message.lobbyId}");
    }

    void ProcessPlayerReadyMessage(NetworkMessage message)
    {
        // Handle player ready state change
        bool isReady = message.data == "true";
        Debug.Log($"Player {message.senderId} is {(isReady ? "ready" : "not ready")}");
    }

    void ProcessStartGameMessage(NetworkMessage message)
    {
        // Handle game start
        Debug.Log($"Game starting in lobby {message.lobbyId}");
        if (lobbyManager != null)
        {
            // Trigger game start
        }
    }

    void ProcessDrawOfferMessage(NetworkMessage message)
    {
        if (gameUI != null)
        {
            // Show draw offer UI
            Debug.Log("Opponent offered a draw");
        }
    }

    void ProcessResignMessage(NetworkMessage message)
    {
        if (gameUI != null)
        {
            // Handle opponent resignation
            Debug.Log("Opponent resigned");
        }
    }

    #endregion

    #region Utility Methods

    void SendPing()
    {
        var pingMessage = new NetworkMessage(MessageType.Ping);
        SendMessage(pingMessage);
    }

    void SendPong(string targetId)
    {
        var pongMessage = new NetworkMessage(MessageType.Pong);
        pongMessage.recipientId = targetId;
        SendMessage(pongMessage);
    }

    public void SendMove(string move, int moveNumber)
    {
        MoveMessage moveData = new MoveMessage
        {
            move = move,
            moveNumber = moveNumber,
            timeRemaining = 0, // Add timer implementation if needed
            isCheck = false,
            isCheckmate = false,
            isStalemate = false
        };

        NetworkMessage message = new NetworkMessage(MessageType.MakeMove, JsonUtility.ToJson(moveData));
        SendMessage(message);
    }

    public void SendChatMessage(string text)
    {
        NetworkMessage message = new NetworkMessage(MessageType.ChatMessage, text);
        SendMessage(message);
    }

    public void SendLobbyUpdate(LobbyData lobbyData)
    {
        NetworkMessage message = new NetworkMessage(MessageType.LobbyInfo, JsonUtility.ToJson(lobbyData));
        SendMessage(message);
    }

    #endregion

    void OnDestroy()
    {
        Disconnect();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // App paused - might want to handle reconnection
            Debug.Log("Application paused");
        }
        else
        {
            // App resumed
            if (!isConnected)
            {
                ConnectToServer();
            }
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // Lost focus
        }
        else
        {
            // Regained focus - check connection
            if (!isConnected)
            {
                ConnectToServer();
            }
        }
    }
}