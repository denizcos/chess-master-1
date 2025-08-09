using Unity.Netcode;
using UnityEngine;

public class ChessNetworkSync : NetworkBehaviour
{
    private BlindfoldMultiplayerUI multiplayerUI;
    private MultiplayerLobbyManager lobbyManager;

    void Start()
    {
        // Use new Unity method to avoid obsolete warning
        multiplayerUI = FindFirstObjectByType<BlindfoldMultiplayerUI>();
        lobbyManager = FindFirstObjectByType<MultiplayerLobbyManager>();
    }

    // Send chess move
    public void SendMove(string move)
    {
        if (IsClient)
        {
            SendMoveServerRpc(move);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendMoveServerRpc(string move, ServerRpcParams rpcParams = default)
    {
        // Broadcast to all clients except sender
        ReceiveMoveClientRpc(move, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    void ReceiveMoveClientRpc(string move, ulong senderId)
    {
        // Don't process our own moves
        if (NetworkManager.Singleton.LocalClientId == senderId)
            return;

        // Process opponent's move
        if (multiplayerUI != null)
        {
            multiplayerUI.OnRemoteMoveReceived(move);
        }
    }

    // Send chat message
    public void SendChat(string message, string senderName)
    {
        if (IsClient)
        {
            SendChatServerRpc(message, senderName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendChatServerRpc(string message, string senderName)
    {
        ReceiveChatClientRpc(message, senderName);
    }

    [ClientRpc]
    void ReceiveChatClientRpc(string message, string senderName)
    {
        // Don't add our own messages again
        if (lobbyManager != null && senderName == lobbyManager.currentPlayerName)
            return;

        if (lobbyManager != null)
        {
            lobbyManager.AddChatMessage(senderName, message);
        }
    }

    // Color swap (host only)
    public void RequestColorSwap()
    {
        if (IsHost)
        {
            SwapColorsClientRpc();
        }
    }

    [ClientRpc]
    void SwapColorsClientRpc()
    {
        // Update local player colors using the new method
        if (lobbyManager != null)
        {
            lobbyManager.SwapPlayerColors();

            // Refresh UI
            lobbyManager.UpdateLobbyRoomUI();
        }
    }

    // Start game for all players
    public void StartGameForAll()
    {
        if (IsHost)
        {
            StartGameClientRpc();
        }
    }

    [ClientRpc]
    void StartGameClientRpc()
    {
        if (lobbyManager != null)
        {
            lobbyManager.ShowGamePanel();
        }
    }

    // Handle ready state synchronization
    public void SyncReadyState(string playerId, bool isReady)
    {
        if (IsClient)
        {
            SyncReadyServerRpc(playerId, isReady);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SyncReadyServerRpc(string playerId, bool isReady)
    {
        SyncReadyClientRpc(playerId, isReady);
    }

    [ClientRpc]
    void SyncReadyClientRpc(string playerId, bool isReady)
    {
        // Update ready state in UI
        Debug.Log($"Player {playerId} is {(isReady ? "ready" : "not ready")}");
    }

    // Handle game over
    public void EndGameForAll(string result)
    {
        if (IsHost)
        {
            EndGameClientRpc(result);
        }
    }

    [ClientRpc]
    void EndGameClientRpc(string result)
    {
        if (lobbyManager != null)
        {
            lobbyManager.EndGame(result);
        }
    }

    // Handle player disconnect
    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            // Notify other players
            PlayerDisconnectedClientRpc();
        }

        base.OnNetworkDespawn();
    }

    [ClientRpc]
    void PlayerDisconnectedClientRpc()
    {
        if (lobbyManager != null)
        {
            lobbyManager.AddChatMessage("System", "Opponent disconnected!");
        }
    }
}