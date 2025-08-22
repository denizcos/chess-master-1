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

    // Color swap (host only) - IMPROVED VERSION
    public void RequestColorSwap()
    {
        if (IsHost)
        {
            Debug.Log("[NETWORK] Host requesting color swap");
            SwapColorsClientRpc();
        }
    }

    [ClientRpc]
    void SwapColorsClientRpc()
    {
        Debug.Log("[NETWORK] Received color swap request");

        // Update local player colors using the new method
        if (lobbyManager != null)
        {
            lobbyManager.SwapPlayerColors();
            Debug.Log("[NETWORK] Player colors swapped locally");

            // Force UI refresh after a short delay
            lobbyManager.StartCoroutine(DelayedUIUpdateAfterSwap());
        }
    }

    System.Collections.IEnumerator DelayedUIUpdateAfterSwap()
    {
        yield return new WaitForSeconds(0.5f);
        if (lobbyManager != null)
        {
            Debug.Log("[NETWORK] Updating UI after color swap");
            // DELETE THIS LINE: lobbyManager.lastUIUpdateTime = 0f; 
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

    // Handle ready state synchronization - IMPROVED VERSION
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
        // Broadcast to all clients including sender for confirmation
        SyncReadyClientRpc(playerId, isReady);
    }

    [ClientRpc]
    void SyncReadyClientRpc(string playerId, bool isReady)
    {
        // Update ready state in UI
        Debug.Log($"[NETWORK] Player {playerId} is {(isReady ? "ready" : "not ready")}");

        // Force UI update to reflect the change
        if (lobbyManager != null)
        {
            // Trigger UI update after a short delay to ensure Unity Lobby is updated
            lobbyManager.StartCoroutine(DelayedUIUpdate());
        }
    }

    System.Collections.IEnumerator DelayedUIUpdate()
    {
        yield return new WaitForSeconds(0.5f);
        if (lobbyManager != null)
        {
            lobbyManager.UpdateLobbyRoomUI();
        }
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
            // Notify other players about disconnection
            PlayerDisconnectedClientRpc();
        }

        base.OnNetworkDespawn();
    }

    [ClientRpc]
    void PlayerDisconnectedClientRpc()
    {
        if (lobbyManager != null)
        {
            lobbyManager.AddChatMessage("System", "A player disconnected!");

            // If we're in game, end it with a win for remaining player
            if (lobbyManager.gamePanel.activeSelf)
            {
                lobbyManager.EndGame("Opponent disconnected. You win!");
            }
        }
    }

    // Network manager events
    void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[NETWORK] Client {clientId} disconnected");

        // If opponent disconnected during game
        if (multiplayerUI != null && lobbyManager != null)
        {
            if (lobbyManager.gamePanel.activeSelf)
            {
                multiplayerUI.EndGame("Opponent disconnected. You win!");
            }
            else
            {
                lobbyManager.AddChatMessage("System", "Opponent disconnected!");
            }
        }
    }

    // Add network callbacks for debugging
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"[NETWORK] NetworkSync spawned - IsHost: {IsHost}, IsClient: {IsClient}");
    }

    // Add method to force lobby sync
    public void ForceLobbySync()
    {
        if (IsHost)
        {
            ForceLobbyUpdateClientRpc();
        }
    }

    [ClientRpc]
    void ForceLobbyUpdateClientRpc()
    {
        if (lobbyManager != null)
        {
            lobbyManager.UpdateLobbyRoomUI();
        }
    }

    // Resignation handling
    public void SendResign(string playerName)
    {
        if (IsClient)
        {
            SendResignServerRpc(playerName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendResignServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        // Broadcast to all clients
        ReceiveResignClientRpc(playerName, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    void ReceiveResignClientRpc(string playerName, ulong senderId)
    {
        Debug.Log($"[NETWORK] Player {playerName} resigned (sender ID: {senderId})");

        bool isLocalPlayerResigning = NetworkManager.Singleton.LocalClientId == senderId;

        // End the game for both players
        if (multiplayerUI != null && lobbyManager != null && lobbyManager.gamePanel.activeSelf)
        {
            if (isLocalPlayerResigning)
            {
                // For the resigner, show they resigned
                string opponentName = lobbyManager.remotePlayer != null ? lobbyManager.remotePlayer.playerName : "Opponent";
                multiplayerUI.EndGame($"You resigned. {opponentName} wins!");
            }
            else
            {
                // For the opponent, show the other player resigned
                multiplayerUI.EndGame($"{playerName} resigned. You win!");
            }
        }

        // Add chat message
        if (lobbyManager != null)
        {
            string winner = isLocalPlayerResigning ? "Opponent" : "You";
            string result = $"{playerName} resigned. {winner} won!";
            lobbyManager.AddChatMessage("System", result);
        }

        // Call the UI method for any additional processing
        if (multiplayerUI != null)
        {
            multiplayerUI.OnPlayerResigned(playerName, isLocalPlayerResigning);
        }
    }

    // SIMPLIFIED DRAW OFFER SYSTEM

    // Send draw offer
    public void SendDrawOffer(string playerName)
    {
        if (IsClient)
        {
            SendDrawOfferServerRpc(playerName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendDrawOfferServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        // Broadcast to all clients except sender
        ReceiveDrawOfferClientRpc(playerName, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    void ReceiveDrawOfferClientRpc(string playerName, ulong senderId)
    {
        // Don't process our own draw offers
        if (NetworkManager.Singleton.LocalClientId == senderId)
            return;

        // Show draw offer to opponent
        if (multiplayerUI != null)
        {
            multiplayerUI.OnDrawOfferReceived(playerName);
            UIButtonHoverSound.Instance.PlayNotification(); // ADD THIS LINE
        }

        if (lobbyManager != null)
        {
            lobbyManager.AddChatMessage("System", $"{playerName} offered a draw.");
        }
    }

    // Send draw acceptance
    public void SendDrawAccept(string playerName)
    {
        if (IsClient)
        {
            SendDrawAcceptServerRpc(playerName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendDrawAcceptServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        // Broadcast to all clients
        ReceiveDrawAcceptClientRpc(playerName);
    }

    [ClientRpc]
    void ReceiveDrawAcceptClientRpc(string playerName)
    {
        // Process draw acceptance for all clients - let UI handle the game ending
        if (multiplayerUI != null)
        {
            multiplayerUI.OnDrawAccepted();
        }

        if (lobbyManager != null)
        {
            lobbyManager.AddChatMessage("System", $"{playerName} accepted the draw.");
        }
    }

    public void NotifyPlayerJoined(string playerName)
    {
        if (IsClient)
        {
            NotifyPlayerJoinedServerRpc(playerName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void NotifyPlayerJoinedServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        // Only notify other clients (not the joiner)
        NotifyPlayerJoinedClientRpc(playerName, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    void NotifyPlayerJoinedClientRpc(string playerName, ulong senderId)
    {
        // Don't notify the person who just joined
        if (NetworkManager.Singleton.LocalClientId == senderId)
            return;

        // Only play sound for host when someone joins
        if (IsHost && lobbyManager != null)
        {
            UIButtonHoverSound.Instance.PlayNotification();
            Debug.Log($"[NOTIFICATION] {playerName} joined - playing sound for host");
        }
    }
    public void SendDrawDecline(string playerName)
    {
        if (IsClient)
        {
            SendDrawDeclineServerRpc(playerName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    void SendDrawDeclineServerRpc(string playerName, ServerRpcParams rpcParams = default)
    {
        // Broadcast to all clients except sender
        ReceiveDrawDeclineClientRpc(playerName, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    void ReceiveDrawDeclineClientRpc(string playerName, ulong senderId)
    {
        // Don't process our own decline
        if (NetworkManager.Singleton.LocalClientId == senderId)
            return;

        // Notify original draw offerer
        if (multiplayerUI != null)
        {
            multiplayerUI.OnDrawDeclined();
        }

        if (lobbyManager != null)
        {
            lobbyManager.AddChatMessage("System", $"{playerName} declined the draw offer.");
        }
    }
}