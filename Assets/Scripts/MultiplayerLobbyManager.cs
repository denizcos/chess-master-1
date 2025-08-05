using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using System.Collections;

[System.Serializable]
public class LobbyData
{
    public string lobbyId;
    public string hostName;
    public string lobbyName;
    public bool isPrivate;
    public string password;
    public bool isFull;
    public int playerCount;
    public long createdTime;
    public string gameMode = "Blindfold";
}

[System.Serializable]
public class PlayerData
{
    public string playerId;
    public string playerName;
    public bool isHost;
    public bool isReady;
    public ChessRules.PieceColor color;
}

public class MultiplayerLobbyManager : MonoBehaviour
{
    [Header("Panel References")]
    public GameObject mainMenuPanel;
    public GameObject lobbyListPanel;
    public GameObject createLobbyPanel;
    public GameObject lobbyRoomPanel;
    public GameObject gamePanel;

    [Header("Lobby List UI")]
    public Transform lobbyListContent;
    public GameObject lobbyItemPrefab;
    public Button createLobbyButton;
    public Button refreshButton;
    public Button backToMenuButton;
    public TMP_InputField searchInput;

    [Header("Create Lobby UI")]
    public TMP_InputField lobbyNameInput;
    public TMP_InputField hostNameInput;
    public Toggle privateToggle;
    public TMP_InputField passwordInput;
    public Button confirmCreateButton;
    public Button cancelCreateButton;
    public GameObject passwordGroup;

    [Header("Lobby Room UI")]
    public TMP_Text lobbyNameText;
    public TMP_Text lobbyIdText;
    public TMP_Text player1NameText;
    public TMP_Text player2NameText;
    public Image player1ReadyIndicator;
    public Image player2ReadyIndicator;
    public Button readyButton;
    public Button startGameButton;
    public Button leaveLobbyButton;
    public Button colorSwapButton;
    public TMP_Text chatText;
    public TMP_InputField chatInput;
    public Button sendChatButton;

    [Header("Join Lobby UI")]
    public GameObject passwordPromptPanel;
    public TMP_InputField joinPasswordInput;
    public Button joinConfirmButton;
    public Button joinCancelButton;
    public TMP_Text joinErrorText;

    [Header("Settings")]
    public float refreshInterval = 5f;
    public int maxChatMessages = 50;
    public Color readyColor = Color.green;
    public Color notReadyColor = Color.red;

    // Network simulation (replace with actual networking)
    private Dictionary<string, LobbyData> activeLobbies = new Dictionary<string, LobbyData>();
    private Dictionary<string, List<PlayerData>> lobbyPlayers = new Dictionary<string, List<PlayerData>>();
    private Dictionary<string, List<string>> lobbyChatMessages = new Dictionary<string, List<string>>();

    // Current session data
    private string currentLobbyId;
    private string currentPlayerId;
    private string currentPlayerName;
    private bool isHost;
    private PlayerData localPlayer;
    private PlayerData remotePlayer;

    // References to game components
    private ChessRules chessRules;
    private BlindfoldMultiplayerUI multiplayerUI;

    private Coroutine refreshCoroutine;

    void Start()
    {
        InitializeUI();
        GeneratePlayerId();

        // Get chess components
        chessRules = FindObjectOfType<ChessRules>();
        if (chessRules == null)
        {
            Debug.LogError("ChessRules component not found!");
        }
    }

    void InitializeUI()
    {
        // Set up button listeners
        createLobbyButton.onClick.AddListener(() => ShowCreateLobbyPanel());
        refreshButton.onClick.AddListener(() => RefreshLobbyList());
        backToMenuButton.onClick.AddListener(() => BackToMainMenu());

        confirmCreateButton.onClick.AddListener(() => CreateLobby());
        cancelCreateButton.onClick.AddListener(() => CancelCreateLobby());

        readyButton.onClick.AddListener(() => ToggleReady());
        startGameButton.onClick.AddListener(() => StartGame());
        leaveLobbyButton.onClick.AddListener(() => LeaveLobby());
        colorSwapButton.onClick.AddListener(() => SwapColors());

        sendChatButton.onClick.AddListener(() => SendChatMessage());
        chatInput.onSubmit.AddListener((text) => SendChatMessage());

        joinConfirmButton.onClick.AddListener(() => ConfirmJoinWithPassword());
        joinCancelButton.onClick.AddListener(() => CancelJoinWithPassword());

        privateToggle.onValueChanged.AddListener((value) => OnPrivateToggleChanged(value));
        searchInput.onValueChanged.AddListener((text) => OnSearchChanged(text));

        // Initialize panels
        ShowLobbyListPanel();
        passwordGroup.SetActive(false);
    }

    void GeneratePlayerId()
    {
        currentPlayerId = System.Guid.NewGuid().ToString();
        currentPlayerName = "Player" + Random.Range(1000, 9999);
    }

    #region Panel Management

    void HideAllPanels()
    {
        if (mainMenuPanel) mainMenuPanel.SetActive(false);
        if (lobbyListPanel) lobbyListPanel.SetActive(false);
        if (createLobbyPanel) createLobbyPanel.SetActive(false);
        if (lobbyRoomPanel) lobbyRoomPanel.SetActive(false);
        if (gamePanel) gamePanel.SetActive(false);
        if (passwordPromptPanel) passwordPromptPanel.SetActive(false);
    }

    public void ShowLobbyListPanel()
    {
        HideAllPanels();
        lobbyListPanel.SetActive(true);
        RefreshLobbyList();

        if (refreshCoroutine != null) StopCoroutine(refreshCoroutine);
        refreshCoroutine = StartCoroutine(AutoRefreshLobbies());
    }

    void ShowCreateLobbyPanel()
    {
        HideAllPanels();
        createLobbyPanel.SetActive(true);

        // Reset input fields
        lobbyNameInput.text = currentPlayerName + "'s Game";
        hostNameInput.text = currentPlayerName;
        privateToggle.isOn = false;
        passwordInput.text = "";
        passwordGroup.SetActive(false);
    }

    void ShowLobbyRoomPanel()
    {
        HideAllPanels();
        lobbyRoomPanel.SetActive(true);
        UpdateLobbyRoomUI();
    }

    void ShowGamePanel()
    {
        HideAllPanels();
        gamePanel.SetActive(true);

        if (multiplayerUI == null)
        {
            multiplayerUI = gamePanel.GetComponent<BlindfoldMultiplayerUI>();
            if (multiplayerUI == null)
            {
                multiplayerUI = gamePanel.AddComponent<BlindfoldMultiplayerUI>();
            }
        }

        multiplayerUI.InitializeGame(chessRules, localPlayer, remotePlayer, this);
    }

    void BackToMainMenu()
    {
        if (refreshCoroutine != null) StopCoroutine(refreshCoroutine);

        HideAllPanels();
        if (mainMenuPanel) mainMenuPanel.SetActive(true);
    }

    #endregion

    #region Lobby Creation

    void OnPrivateToggleChanged(bool isPrivate)
    {
        passwordGroup.SetActive(isPrivate);
        if (!isPrivate) passwordInput.text = "";
    }

    void CreateLobby()
    {
        string lobbyName = string.IsNullOrEmpty(lobbyNameInput.text) ?
            currentPlayerName + "'s Game" : lobbyNameInput.text;
        string hostName = string.IsNullOrEmpty(hostNameInput.text) ?
            currentPlayerName : hostNameInput.text;

        currentPlayerName = hostName;

        // Generate lobby ID
        string lobbyId = "LOBBY_" + System.Guid.NewGuid().ToString().Substring(0, 8).ToUpper();

        // Create lobby data
        LobbyData newLobby = new LobbyData
        {
            lobbyId = lobbyId,
            hostName = hostName,
            lobbyName = lobbyName,
            isPrivate = privateToggle.isOn,
            password = privateToggle.isOn ? passwordInput.text : "",
            isFull = false,
            playerCount = 1,
            createdTime = System.DateTimeOffset.Now.ToUnixTimeSeconds()
        };

        // Create host player
        localPlayer = new PlayerData
        {
            playerId = currentPlayerId,
            playerName = hostName,
            isHost = true,
            isReady = false,
            color = ChessRules.PieceColor.White
        };

        // Add to active lobbies
        activeLobbies[lobbyId] = newLobby;
        lobbyPlayers[lobbyId] = new List<PlayerData> { localPlayer };
        lobbyChatMessages[lobbyId] = new List<string>();

        currentLobbyId = lobbyId;
        isHost = true;

        ShowLobbyRoomPanel();
        AddChatMessage("System", $"{hostName} created the lobby.");
    }

    void CancelCreateLobby()
    {
        ShowLobbyListPanel();
    }

    #endregion

    #region Lobby List Management

    void RefreshLobbyList()
    {
        // Clear existing items
        foreach (Transform child in lobbyListContent)
        {
            Destroy(child.gameObject);
        }

        // Filter lobbies based on search
        string searchTerm = searchInput.text.ToLower();
        var filteredLobbies = activeLobbies.Values
            .Where(l => !l.isFull && (string.IsNullOrEmpty(searchTerm) ||
                l.lobbyName.ToLower().Contains(searchTerm) ||
                l.hostName.ToLower().Contains(searchTerm)))
            .OrderByDescending(l => l.createdTime);

        // Create lobby items
        foreach (var lobby in filteredLobbies)
        {
            CreateLobbyListItem(lobby);
        }
    }

    void CreateLobbyListItem(LobbyData lobby)
    {
        GameObject item = Instantiate(lobbyItemPrefab, lobbyListContent);

        // Set up the lobby item UI (assumes prefab has these components)
        TMP_Text lobbyNameText = item.transform.Find("LobbyName")?.GetComponent<TMP_Text>();
        TMP_Text hostNameText = item.transform.Find("HostName")?.GetComponent<TMP_Text>();
        TMP_Text playerCountText = item.transform.Find("PlayerCount")?.GetComponent<TMP_Text>();
        Image lockIcon = item.transform.Find("LockIcon")?.GetComponent<Image>();
        Button joinButton = item.GetComponent<Button>() ?? item.transform.Find("JoinButton")?.GetComponent<Button>();

        if (lobbyNameText) lobbyNameText.text = lobby.lobbyName;
        if (hostNameText) hostNameText.text = "Host: " + lobby.hostName;
        if (playerCountText) playerCountText.text = $"{lobby.playerCount}/2";
        if (lockIcon) lockIcon.gameObject.SetActive(lobby.isPrivate);

        if (joinButton)
        {
            joinButton.onClick.AddListener(() => AttemptJoinLobby(lobby.lobbyId));
        }
    }

    void OnSearchChanged(string searchText)
    {
        RefreshLobbyList();
    }

    IEnumerator AutoRefreshLobbies()
    {
        while (lobbyListPanel.activeSelf)
        {
            yield return new WaitForSeconds(refreshInterval);
            RefreshLobbyList();
        }
    }

    #endregion

    #region Joining Lobbies

    void AttemptJoinLobby(string lobbyId)
    {
        if (!activeLobbies.ContainsKey(lobbyId))
        {
            Debug.LogError("Lobby not found!");
            return;
        }

        LobbyData lobby = activeLobbies[lobbyId];

        if (lobby.isPrivate)
        {
            // Show password prompt
            passwordPromptPanel.SetActive(true);
            joinPasswordInput.text = "";
            joinErrorText.text = "";
            currentLobbyId = lobbyId; // Store for password confirmation
        }
        else
        {
            JoinLobby(lobbyId);
        }
    }

    void ConfirmJoinWithPassword()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        LobbyData lobby = activeLobbies[currentLobbyId];

        if (joinPasswordInput.text == lobby.password)
        {
            passwordPromptPanel.SetActive(false);
            JoinLobby(currentLobbyId);
        }
        else
        {
            joinErrorText.text = "Incorrect password!";
            joinPasswordInput.text = "";
        }
    }

    void CancelJoinWithPassword()
    {
        passwordPromptPanel.SetActive(false);
        currentLobbyId = null;
    }

    void JoinLobby(string lobbyId)
    {
        if (!activeLobbies.ContainsKey(lobbyId)) return;

        LobbyData lobby = activeLobbies[lobbyId];

        // Create joining player
        localPlayer = new PlayerData
        {
            playerId = currentPlayerId,
            playerName = currentPlayerName,
            isHost = false,
            isReady = false,
            color = ChessRules.PieceColor.Black
        };

        // Add to lobby
        if (!lobbyPlayers.ContainsKey(lobbyId))
            lobbyPlayers[lobbyId] = new List<PlayerData>();

        lobbyPlayers[lobbyId].Add(localPlayer);

        // Update lobby info
        lobby.playerCount = lobbyPlayers[lobbyId].Count;
        lobby.isFull = lobby.playerCount >= 2;
        activeLobbies[lobbyId] = lobby;

        currentLobbyId = lobbyId;
        isHost = false;

        // Get remote player (host)
        remotePlayer = lobbyPlayers[lobbyId].FirstOrDefault(p => p.playerId != currentPlayerId);

        ShowLobbyRoomPanel();
        AddChatMessage("System", $"{currentPlayerName} joined the lobby.");
    }

    void LeaveLobby()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        if (lobbyPlayers.ContainsKey(currentLobbyId))
        {
            lobbyPlayers[currentLobbyId].RemoveAll(p => p.playerId == currentPlayerId);

            if (isHost || lobbyPlayers[currentLobbyId].Count == 0)
            {
                // Close lobby if host leaves or empty
                activeLobbies.Remove(currentLobbyId);
                lobbyPlayers.Remove(currentLobbyId);
                lobbyChatMessages.Remove(currentLobbyId);
            }
            else
            {
                // Update lobby info
                var lobby = activeLobbies[currentLobbyId];
                lobby.playerCount = lobbyPlayers[currentLobbyId].Count;
                lobby.isFull = false;
                activeLobbies[currentLobbyId] = lobby;

                AddChatMessage("System", $"{currentPlayerName} left the lobby.");
            }
        }

        currentLobbyId = null;
        ShowLobbyListPanel();
    }

    #endregion

    #region Lobby Room Management

    void UpdateLobbyRoomUI()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        LobbyData lobby = activeLobbies[currentLobbyId];
        List<PlayerData> players = lobbyPlayers[currentLobbyId];

        lobbyNameText.text = lobby.lobbyName;
        lobbyIdText.text = "ID: " + lobby.lobbyId;

        // Update player displays
        PlayerData player1 = players.FirstOrDefault(p => p.color == ChessRules.PieceColor.White);
        PlayerData player2 = players.FirstOrDefault(p => p.color == ChessRules.PieceColor.Black);

        player1NameText.text = player1 != null ? player1.playerName : "Waiting...";
        player2NameText.text = player2 != null ? player2.playerName : "Waiting...";

        player1ReadyIndicator.color = player1 != null && player1.isReady ? readyColor : notReadyColor;
        player2ReadyIndicator.color = player2 != null && player2.isReady ? readyColor : notReadyColor;

        // Update buttons
        readyButton.interactable = players.Count == 2;
        startGameButton.interactable = isHost && players.Count == 2 &&
            players.All(p => p.isReady);
        colorSwapButton.interactable = isHost && players.Count == 2;

        // Update ready button text
        TMP_Text readyButtonText = readyButton.GetComponentInChildren<TMP_Text>();
        if (readyButtonText != null)
        {
            readyButtonText.text = localPlayer != null && localPlayer.isReady ? "Not Ready" : "Ready";
        }

        // Store remote player reference
        remotePlayer = players.FirstOrDefault(p => p.playerId != currentPlayerId);
    }

    void ToggleReady()
    {
        if (localPlayer == null) return;

        localPlayer.isReady = !localPlayer.isReady;

        // Update in lobby players list
        var players = lobbyPlayers[currentLobbyId];
        var playerIndex = players.FindIndex(p => p.playerId == currentPlayerId);
        if (playerIndex >= 0)
        {
            players[playerIndex] = localPlayer;
        }

        UpdateLobbyRoomUI();

        string status = localPlayer.isReady ? "ready" : "not ready";
        AddChatMessage("System", $"{localPlayer.playerName} is {status}.");
    }

    void SwapColors()
    {
        if (!isHost || lobbyPlayers[currentLobbyId].Count != 2) return;

        var players = lobbyPlayers[currentLobbyId];
        foreach (var player in players)
        {
            player.color = player.color == ChessRules.PieceColor.White ?
                ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
        }

        // Update local references
        localPlayer = players.FirstOrDefault(p => p.playerId == currentPlayerId);
        remotePlayer = players.FirstOrDefault(p => p.playerId != currentPlayerId);

        UpdateLobbyRoomUI();
        AddChatMessage("System", "Colors swapped!");
    }

    void StartGame()
    {
        if (!isHost) return;

        var players = lobbyPlayers[currentLobbyId];
        if (players.Count != 2 || !players.All(p => p.isReady)) return;

        AddChatMessage("System", "Game starting!");

        // Transition to game
        ShowGamePanel();
    }

    #endregion

    #region Chat System

    void SendChatMessage()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        string msgText = chatInput.text;
        if (string.IsNullOrEmpty(msgText)) return;

        AddChatMessage(currentPlayerName, msgText);
        chatInput.text = "";
        chatInput.Select();
        chatInput.ActivateInputField();
    }

    void AddChatMessage(string sender, string message)
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        if (!lobbyChatMessages.ContainsKey(currentLobbyId))
            lobbyChatMessages[currentLobbyId] = new List<string>();

        string formattedMessage = $"<b>{sender}:</b> {message}";
        lobbyChatMessages[currentLobbyId].Add(formattedMessage);

        // Limit chat history
        if (lobbyChatMessages[currentLobbyId].Count > maxChatMessages)
        {
            lobbyChatMessages[currentLobbyId].RemoveAt(0);
        }

        // Update chat display
        UpdateChatDisplay();
    }

    void UpdateChatDisplay()
    {
        if (chatText == null || string.IsNullOrEmpty(currentLobbyId)) return;

        if (lobbyChatMessages.ContainsKey(currentLobbyId))
        {
            chatText.text = string.Join("\n", lobbyChatMessages[currentLobbyId]);
        }
    }

    #endregion

    #region Public Methods for Game Integration

    public void SendMove(string move)
    {
        // This would send the move to the remote player
        // In a real implementation, this would use networking
        if (remotePlayer != null)
        {
            Debug.Log($"Sending move {move} to {remotePlayer.playerName}");
            // Simulate receiving the move on the other end
            StartCoroutine(SimulateReceiveMove(move));
        }
    }

    public void SendGameMessage(string message)
    {
        AddChatMessage(currentPlayerName, message);
    }

    public void EndGame(string result)
    {
        AddChatMessage("System", $"Game ended: {result}");

        // Reset ready states
        if (lobbyPlayers.ContainsKey(currentLobbyId))
        {
            foreach (var player in lobbyPlayers[currentLobbyId])
            {
                player.isReady = false;
            }
        }

        // Return to lobby room
        ShowLobbyRoomPanel();
    }

    IEnumerator SimulateReceiveMove(string move)
    {
        // Simulate network delay
        yield return new WaitForSeconds(0.5f);

        if (multiplayerUI != null)
        {
            multiplayerUI.OnRemoteMoveReceived(move);
        }
    }

    #endregion

    void OnDestroy()
    {
        if (refreshCoroutine != null)
        {
            StopCoroutine(refreshCoroutine);
        }

        // Clean up lobby if host
        if (isHost && !string.IsNullOrEmpty(currentLobbyId))
        {
            activeLobbies.Remove(currentLobbyId);
            lobbyPlayers.Remove(currentLobbyId);
            lobbyChatMessages.Remove(currentLobbyId);
        }
    }
}