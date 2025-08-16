using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using System.Threading.Tasks;

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

    [Header("Direct Join UI")]
    public GameObject directJoinPanel;
    public TMP_InputField directJoinCodeInput;
    public Button directJoinButton;
    public Button directJoinCancelButton;

    [Header("Settings")]
    public float refreshInterval = 5f;
    public int maxChatMessages = 50;
    public Color readyColor = Color.green;
    public Color notReadyColor = Color.red;

    private Dictionary<string, List<string>> lobbyChatMessages = new Dictionary<string, List<string>>();

    // Unity Services variables
    private Lobby currentUnityLobby;
    private List<Lobby> availableLobbies = new List<Lobby>();
    private string joinCode;
    private Coroutine heartbeatCoroutine;
    private Coroutine lobbyPollCoroutine;
    private ChessNetworkSync networkSync;

    // Current session data
    private string currentLobbyId;
    private string currentPlayerId;
    public string currentPlayerName { get; private set; }
    private bool isHost;
    public PlayerData localPlayer { get; private set; }
    public PlayerData remotePlayer { get; private set; }

    // References to game components
    private ChessRules chessRules;
    private BlindfoldMultiplayerUI multiplayerUI;

    async void Start()
    {
        // Ensure NetworkManager exists and is properly configured
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[INIT] NetworkManager.Singleton is null! Make sure NetworkManager is in the scene.");
            return;
        }

        // Initialize Unity Services
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            currentPlayerId = AuthenticationService.Instance.PlayerId;
            Debug.Log($"Signed in with Player ID: {currentPlayerId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
        }

        InitializeUI();
        GenerateDefaultPlayerName();

        // Get chess components
        chessRules = FindFirstObjectByType<ChessRules>();
        if (chessRules == null)
        {
            Debug.LogError("ChessRules component not found!");
        }

        networkSync = FindFirstObjectByType<ChessNetworkSync>();

        // Ensure transport is configured
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[INIT] UnityTransport component not found on NetworkManager!");
        }
    }

    void InitializeUI()
    {
        // Set up button listeners
        createLobbyButton.onClick.AddListener(() => ShowCreateLobbyPanel());
        refreshButton.onClick.AddListener(() => RefreshLobbyList());
        backToMenuButton.onClick.AddListener(() => ShowMainMenu());

        confirmCreateButton.onClick.AddListener(() => CreateLobby());
        cancelCreateButton.onClick.AddListener(() => ShowLobbyListPanel());

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

        if (directJoinButton != null)
        {
            directJoinButton.onClick.AddListener(() => DirectJoinLobby());
            directJoinCancelButton.onClick.AddListener(() => { directJoinPanel.SetActive(false); });
        }

        passwordGroup.SetActive(false);

        // Start auto-refresh if lobby list is active
        if (lobbyListPanel.activeSelf)
        {
            StartCoroutine(AutoRefreshLobbyList());
        }
    }

    void GenerateDefaultPlayerName()
    {
        if (string.IsNullOrEmpty(currentPlayerName))
            currentPlayerName = "Player" + Random.Range(1000, 9999);
    }

    #region Panel Management

    public void ShowLobbyListPanel()
    {
        lobbyListPanel.SetActive(true);
        if (createLobbyPanel) createLobbyPanel.SetActive(false);
        if (lobbyRoomPanel) lobbyRoomPanel.SetActive(false);

        // Start auto-refresh when showing lobby list
        StartCoroutine(AutoRefreshLobbyList());
    }

    IEnumerator AutoRefreshLobbyList()
    {
        while (lobbyListPanel.activeSelf)
        {
            RefreshLobbyList();
            yield return new WaitForSecondsRealtime(3f); // Refresh every 3 seconds
        }
    }

    void ShowCreateLobbyPanel()
    {
        createLobbyPanel.SetActive(true);
        lobbyListPanel.SetActive(false);

        lobbyNameInput.text = currentPlayerName + "'s Game";
        hostNameInput.text = currentPlayerName;
        privateToggle.isOn = false;
        passwordInput.text = "";
        passwordGroup.SetActive(false);
    }

    void ShowLobbyRoomPanel()
    {
        lobbyRoomPanel.SetActive(true);
        lobbyListPanel.SetActive(false);
        createLobbyPanel.SetActive(false);
        UpdateLobbyRoomUI();
        StartLobbyHeartbeat();
        StartLobbyPolling();
    }

    public void ShowGamePanel()
    {
        gamePanel.SetActive(true);
        if (lobbyRoomPanel) lobbyRoomPanel.SetActive(false);

        // first try to grab an existing BlindfoldMultiplayerUI somewhere in the scene
        if (multiplayerUI == null)
            multiplayerUI = FindObjectOfType<BlindfoldMultiplayerUI>(true);

        // as a fallback only, attach one to gamePanel
        if (multiplayerUI == null)
            multiplayerUI = gamePanel.AddComponent<BlindfoldMultiplayerUI>();

        // make sure it's active
        multiplayerUI.gameObject.SetActive(true);

        // now initialize
        multiplayerUI.InitializeGame(chessRules, localPlayer, remotePlayer, this);

    }

    void ShowMainMenu()
    {
        StopLobbyHeartbeat();
        StopLobbyPolling();
        if (mainMenuPanel)
        {
            mainMenuPanel.SetActive(true);
            lobbyListPanel.SetActive(false);
        }
        else
        {
            lobbyListPanel.SetActive(true);
        }
    }

    #endregion

    #region Lobby Creation

    void OnPrivateToggleChanged(bool isPrivate)
    {
        passwordGroup.SetActive(isPrivate);
        if (!isPrivate) passwordInput.text = "";
    }

    async void CreateLobby()
    {
        try
        {
            string lobbyName = string.IsNullOrEmpty(lobbyNameInput.text) ?
                currentPlayerName + "'s Game" : lobbyNameInput.text;
            string hostName = string.IsNullOrEmpty(hostNameInput.text) ?
                currentPlayerName : hostNameInput.text;

            currentPlayerName = hostName;

            Debug.Log($"[CREATE] Starting lobby creation: {lobbyName} by {hostName}");

            // Create Relay allocation first
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            Debug.Log($"[RELAY] Created relay with join code: {joinCode}");

            // Create Unity Lobby
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = privateToggle.isOn,
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, hostName) },
                        { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") },
                        { "Color", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "White") }
                    }
                },
                Data = new Dictionary<string, DataObject>
                {
                    { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) },
                    { "Password", new DataObject(DataObject.VisibilityOptions.Public, passwordInput.text) }
                }
            };

            currentUnityLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 2, options);

            Debug.Log($"[SUCCESS] Created lobby: {currentUnityLobby.Name} (ID: {currentUnityLobby.Id})");
            Debug.Log($"[SUCCESS] Lobby Code: {currentUnityLobby.LobbyCode}");

            // Configure Unity Transport for Relay
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            // Start as host
            NetworkManager.Singleton.StartHost();

            currentLobbyId = currentUnityLobby.Id;
            isHost = true;

            // Create local player data
            localPlayer = new PlayerData
            {
                playerId = currentPlayerId,
                playerName = hostName,
                isHost = true,
                isReady = false,
                color = ChessRules.PieceColor.White
            };

            lobbyChatMessages[currentLobbyId] = new List<string>();

            ShowLobbyRoomPanel();
            AddChatMessage("System", $"{hostName} created the lobby.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ERROR] Failed to create lobby: {e.Message}");
            ShowLobbyListPanel();
        }
    }

    #endregion

    #region Lobby List Management

    async void RefreshLobbyList()
    {
        try
        {
            Debug.Log("=== STARTING LOBBY REFRESH ===");

            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = 25
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            availableLobbies = queryResponse.Results;

            Debug.Log($"[LOBBY QUERY] Found {availableLobbies.Count} total lobbies");

            // Clear existing items
            foreach (Transform child in lobbyListContent)
            {
                Destroy(child.gameObject);
            }

            // Create UI items for each lobby
            foreach (var lobby in availableLobbies)
            {
                CreateLobbyListItem(lobby);
            }

            // Force UI update
            Canvas.ForceUpdateCanvases();
            if (lobbyListContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyListContent.GetComponent<RectTransform>());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ERROR] Failed to refresh lobbies: {e.Message}");
        }
    }

    void CreateLobbyListItem(Lobby lobby)
    {
        if (lobbyItemPrefab == null || lobbyListContent == null)
        {
            Debug.LogError("Missing prefab or content reference!");
            return;
        }

        GameObject item = Instantiate(lobbyItemPrefab, lobbyListContent);
        item.SetActive(true);

        // Get host name
        string hostName = "Unknown";
        var hostPlayer = lobby.Players.FirstOrDefault(p => p.Id == lobby.HostId);
        if (hostPlayer != null && hostPlayer.Data != null && hostPlayer.Data.ContainsKey("PlayerName"))
        {
            hostName = hostPlayer.Data["PlayerName"].Value;
        }

        // Set up the UI elements
        TMP_Text lobbyNameText = item.transform.Find("LobbyName")?.GetComponent<TMP_Text>();
        TMP_Text hostNameText = item.transform.Find("HostName")?.GetComponent<TMP_Text>();
        TMP_Text playerCountText = item.transform.Find("PlayerCount")?.GetComponent<TMP_Text>();
        Image lockIcon = item.transform.Find("LockIcon")?.GetComponent<Image>();

        // Try to find the button in different ways
        Button joinButton = item.GetComponent<Button>();
        if (joinButton == null)
        {
            joinButton = item.transform.Find("JoinButton")?.GetComponent<Button>();
        }
        if (joinButton == null)
        {
            // If there's no specific join button, the whole item might be clickable
            joinButton = item.AddComponent<Button>();
        }

        if (lobbyNameText) lobbyNameText.text = lobby.Name;
        if (hostNameText) hostNameText.text = "Host: " + hostName;
        if (playerCountText) playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
        if (lockIcon) lockIcon.gameObject.SetActive(lobby.IsPrivate);

        // Make sure we remove all previous listeners before adding new one
        if (joinButton)
        {
            joinButton.onClick.RemoveAllListeners();

            // Create a local copy of the lobby reference for the lambda
            Lobby lobbyRef = lobby;
            joinButton.onClick.AddListener(() => {
                Debug.Log($"[UI] Join button clicked for lobby: {lobbyRef.Name}");
                AttemptJoinLobby(lobbyRef);
            });

            // Change color if lobby is full
            if (lobby.Players.Count >= lobby.MaxPlayers)
            {
                var buttonImage = joinButton.GetComponent<Image>();
                if (buttonImage) buttonImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                joinButton.interactable = false;
            }
        }
        else
        {
            Debug.LogWarning($"[UI] No button found for lobby item: {lobby.Name}");
        }
    }

    void OnSearchChanged(string searchText)
    {
        RefreshLobbyList();
    }

    #endregion

    #region Joining Lobbies

    Lobby pendingLobby;

    void AttemptJoinLobby(Lobby lobby)
    {
        Debug.Log($"Attempting to join lobby: {lobby.Name}");
        pendingLobby = lobby;

        // Check for password
        bool hasPassword = lobby.Data != null &&
                          lobby.Data.ContainsKey("Password") &&
                          !string.IsNullOrEmpty(lobby.Data["Password"].Value);

        if (hasPassword)
        {
            passwordPromptPanel.SetActive(true);
            joinPasswordInput.text = "";
            joinErrorText.text = "";
        }
        else
        {
            JoinLobby(lobby);
        }
    }

    void ConfirmJoinWithPassword()
    {
        if (pendingLobby == null) return;

        string correctPassword = pendingLobby.Data["Password"].Value;

        if (joinPasswordInput.text == correctPassword)
        {
            passwordPromptPanel.SetActive(false);
            JoinLobby(pendingLobby);
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
        pendingLobby = null;
    }

    async void JoinLobby(Lobby lobby)
    {
        try
        {
            Debug.Log($"[JOIN] Starting to join lobby: {lobby.Name}");

            // Prepare join options with opposite color
            string myColor = "Black";
            foreach (var player in lobby.Players)
            {
                if (player.Data != null && player.Data.ContainsKey("Color"))
                {
                    myColor = (player.Data["Color"].Value == "White") ? "Black" : "White";
                    break;
                }
            }

            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, currentPlayerName) },
                        { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") },
                        { "Color", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, myColor) }
                    }
                }
            };

            // Join the lobby
            currentUnityLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, options);
            Debug.Log("[JOIN] Successfully joined Unity Lobby");

            // Get relay code and join relay
            string relayJoinCode = currentUnityLobby.Data["JoinCode"].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            Debug.Log("[JOIN] Successfully joined Relay");

            // Configure transport
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            // Start client
            NetworkManager.Singleton.StartClient();
            Debug.Log("[JOIN] Started network client");

            // Set local data
            currentLobbyId = currentUnityLobby.Id;
            isHost = false;

            localPlayer = new PlayerData
            {
                playerId = currentPlayerId,
                playerName = currentPlayerName,
                isHost = false,
                isReady = false,
                color = myColor == "White" ? ChessRules.PieceColor.White : ChessRules.PieceColor.Black
            };

            if (!lobbyChatMessages.ContainsKey(currentLobbyId))
                lobbyChatMessages[currentLobbyId] = new List<string>();

            ShowLobbyRoomPanel();
            AddChatMessage("System", $"{currentPlayerName} joined the lobby.");

            Debug.Log("[JOIN] Join process completed successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[JOIN ERROR] {e.Message}");
            Debug.LogError($"[JOIN ERROR] Stack: {e.StackTrace}");
            ShowLobbyListPanel();
        }
    }

    async void DirectJoinLobby()
    {
        string code = directJoinCodeInput.text.ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogError("Please enter a lobby code");
            return;
        }

        try
        {
            // First, get the lobby by code to check existing colors
            Lobby lobbyToJoin = await LobbyService.Instance.JoinLobbyByCodeAsync(code);
            await LobbyService.Instance.RemovePlayerAsync(lobbyToJoin.Id, currentPlayerId);

            // Determine which color is available
            string myColor = "Black";
            if (lobbyToJoin.Players.Count > 1)
            {
                var otherPlayer = lobbyToJoin.Players.FirstOrDefault(p => p.Id != currentPlayerId);
                if (otherPlayer != null && otherPlayer.Data.ContainsKey("Color"))
                {
                    string existingColor = otherPlayer.Data["Color"].Value;
                    myColor = (existingColor == "White") ? "Black" : "White";
                }
            }

            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, currentPlayerName) },
                        { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") },
                        { "Color", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, myColor) }
                    }
                }
            };

            currentUnityLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);

            string relayJoinCode = currentUnityLobby.Data["JoinCode"].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();

            currentLobbyId = currentUnityLobby.Id;
            isHost = false;

            localPlayer = new PlayerData
            {
                playerId = currentPlayerId,
                playerName = currentPlayerName,
                isHost = false,
                isReady = false,
                color = myColor == "White" ? ChessRules.PieceColor.White : ChessRules.PieceColor.Black
            };

            directJoinPanel.SetActive(false);
            ShowLobbyRoomPanel();
            AddChatMessage("System", $"{currentPlayerName} joined the lobby.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to join with code: {e.Message}");
        }
    }

    async void LeaveLobby()
    {
        try
        {
            StopLobbyPolling();
            StopLobbyHeartbeat();

            if (currentUnityLobby != null)
            {
                if (isHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(currentUnityLobby.Id);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(currentUnityLobby.Id, currentPlayerId);
                }
            }

            if (NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.Shutdown();
            }
            else if (NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }

            currentUnityLobby = null;
            currentLobbyId = null;

            ShowLobbyListPanel();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error leaving lobby: {e.Message}");
        }
    }

    #endregion

    #region Lobby Room Management

    public async void UpdateLobbyRoomUI()
    {
        if (currentUnityLobby == null) return;

        try
        {
            // Get the latest lobby state
            currentUnityLobby = await LobbyService.Instance.GetLobbyAsync(currentUnityLobby.Id);

            lobbyNameText.text = currentUnityLobby.Name;
            lobbyIdText.text = "Code: " + currentUnityLobby.LobbyCode;

            // Find players by color
            Player whitePlayer = null;
            Player blackPlayer = null;

            foreach (var player in currentUnityLobby.Players)
            {
                if (player.Data != null && player.Data.ContainsKey("Color"))
                {
                    if (player.Data["Color"].Value == "White")
                        whitePlayer = player;
                    else if (player.Data["Color"].Value == "Black")
                        blackPlayer = player;
                }
            }

            // Update player 1 (White)
            if (whitePlayer != null && whitePlayer.Data.ContainsKey("PlayerName"))
            {
                player1NameText.text = whitePlayer.Data["PlayerName"].Value;
                bool isReady = whitePlayer.Data.ContainsKey("IsReady") && whitePlayer.Data["IsReady"].Value == "true";
                player1ReadyIndicator.color = isReady ? readyColor : notReadyColor;
            }
            else
            {
                player1NameText.text = "Waiting...";
                player1ReadyIndicator.color = notReadyColor;
            }

            // Update player 2 (Black)
            if (blackPlayer != null && blackPlayer.Data.ContainsKey("PlayerName"))
            {
                player2NameText.text = blackPlayer.Data["PlayerName"].Value;
                bool isReady = blackPlayer.Data.ContainsKey("IsReady") && blackPlayer.Data["IsReady"].Value == "true";
                player2ReadyIndicator.color = isReady ? readyColor : notReadyColor;
            }
            else
            {
                player2NameText.text = "Waiting...";
                player2ReadyIndicator.color = notReadyColor;
            }

            // Check if both players are ready
            bool bothReady = false;
            if (currentUnityLobby.Players.Count == 2 && whitePlayer != null && blackPlayer != null)
            {
                bool player1Ready = whitePlayer.Data.ContainsKey("IsReady") && whitePlayer.Data["IsReady"].Value == "true";
                bool player2Ready = blackPlayer.Data.ContainsKey("IsReady") && blackPlayer.Data["IsReady"].Value == "true";
                bothReady = player1Ready && player2Ready;
            }

            // Update buttons
            readyButton.interactable = currentUnityLobby.Players.Count == 2;
            startGameButton.interactable = isHost && bothReady;
            colorSwapButton.interactable = isHost && currentUnityLobby.Players.Count == 2;

            // Update ready button text
            TMP_Text readyButtonText = readyButton.GetComponentInChildren<TMP_Text>();
            if (readyButtonText != null)
            {
                var myPlayer = currentUnityLobby.Players.FirstOrDefault(p => p.Id == currentPlayerId);
                bool isReady = myPlayer != null && myPlayer.Data.ContainsKey("IsReady") && myPlayer.Data["IsReady"].Value == "true";
                readyButtonText.text = isReady ? "Not Ready" : "Ready";

                if (localPlayer != null)
                    localPlayer.isReady = isReady;
            }

            // Update remote player data
            var otherPlayer = currentUnityLobby.Players.FirstOrDefault(p => p.Id != currentPlayerId);
            if (otherPlayer != null && otherPlayer.Data != null)
            {
                remotePlayer = new PlayerData
                {
                    playerId = otherPlayer.Id,
                    playerName = otherPlayer.Data.ContainsKey("PlayerName") ? otherPlayer.Data["PlayerName"].Value : "Unknown",
                    isHost = otherPlayer.Id == currentUnityLobby.HostId,
                    isReady = otherPlayer.Data.ContainsKey("IsReady") && otherPlayer.Data["IsReady"].Value == "true",
                    color = otherPlayer.Data.ContainsKey("Color") && otherPlayer.Data["Color"].Value == "White" ?
                        ChessRules.PieceColor.White : ChessRules.PieceColor.Black
                };
            }
            else
            {
                remotePlayer = null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to update lobby UI: {e.Message}");
        }
    }

    async void ToggleReady()
    {
        if (currentUnityLobby == null || currentUnityLobby.Players.Count < 2) return;

        try
        {
            var myPlayer = currentUnityLobby.Players.FirstOrDefault(p => p.Id == currentPlayerId);
            if (myPlayer == null) return;

            bool currentReady = myPlayer.Data.ContainsKey("IsReady") && myPlayer.Data["IsReady"].Value == "true";
            string newReadyState = (!currentReady).ToString().ToLower();

            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", myPlayer.Data["PlayerName"] },
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, newReadyState) },
                    { "Color", myPlayer.Data["Color"] }
                }
            };

            currentUnityLobby = await LobbyService.Instance.UpdatePlayerAsync(currentUnityLobby.Id, currentPlayerId, options);

            if (localPlayer != null)
                localPlayer.isReady = !currentReady;

            UpdateLobbyRoomUI();

            string status = !currentReady ? "ready" : "not ready";
            AddChatMessage("System", $"{currentPlayerName} is {status}.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to toggle ready: {e.Message}");
        }
    }

    async void SwapColors()
    {
        if (!isHost || currentUnityLobby == null || currentUnityLobby.Players.Count != 2) return;

        try
        {
            // Get both players
            var player1 = currentUnityLobby.Players[0];
            var player2 = currentUnityLobby.Players[1];

            // Swap their colors
            string player1NewColor = player1.Data["Color"].Value == "White" ? "Black" : "White";
            string player2NewColor = player2.Data["Color"].Value == "White" ? "Black" : "White";

            // Update player 1
            UpdatePlayerOptions options1 = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", player1.Data["PlayerName"] },
                    { "IsReady", player1.Data["IsReady"] },
                    { "Color", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, player1NewColor) }
                }
            };

            await LobbyService.Instance.UpdatePlayerAsync(currentUnityLobby.Id, player1.Id, options1);

            // Update player 2
            UpdatePlayerOptions options2 = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", player2.Data["PlayerName"] },
                    { "IsReady", player2.Data["IsReady"] },
                    { "Color", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, player2NewColor) }
                }
            };

            await LobbyService.Instance.UpdatePlayerAsync(currentUnityLobby.Id, player2.Id, options2);

            // Update local player data if needed
            if (localPlayer != null)
            {
                localPlayer.color = localPlayer.color == ChessRules.PieceColor.White ?
                    ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
            }

            // Notify via network sync if available
            if (networkSync != null)
            {
                networkSync.RequestColorSwap();
            }

            UpdateLobbyRoomUI();
            AddChatMessage("System", "Colors swapped!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to swap colors: {e.Message}");
        }
    }

    void StartGame()
    {
        if (!isHost) return;

        if (currentUnityLobby == null || currentUnityLobby.Players.Count != 2) return;

        // Check if both players are ready
        bool allReady = true;
        foreach (var player in currentUnityLobby.Players)
        {
            if (!player.Data.ContainsKey("IsReady") || player.Data["IsReady"].Value != "true")
            {
                allReady = false;
                break;
            }
        }

        if (!allReady)
        {
            Debug.LogError("Not all players are ready!");
            return;
        }

        AddChatMessage("System", "Game starting!");

        if (networkSync != null)
        {
            networkSync.StartGameForAll();
        }

        ShowGamePanel();
    }

    #endregion

    #region Lobby Polling and Heartbeat

    void StartLobbyPolling()
    {
        if (lobbyPollCoroutine != null)
            StopCoroutine(lobbyPollCoroutine);
        lobbyPollCoroutine = StartCoroutine(LobbyPollingLoop());
    }

    void StopLobbyPolling()
    {
        if (lobbyPollCoroutine != null)
        {
            StopCoroutine(lobbyPollCoroutine);
            lobbyPollCoroutine = null;
        }
    }

    IEnumerator LobbyPollingLoop()
    {
        while (currentUnityLobby != null)
        {
            yield return new WaitForSecondsRealtime(2f); // Poll every 2 seconds
            UpdateLobbyRoomUI();
        }
    }

    void StartLobbyHeartbeat()
    {
        if (heartbeatCoroutine != null)
            StopCoroutine(heartbeatCoroutine);
        heartbeatCoroutine = StartCoroutine(LobbyHeartbeat());
    }

    void StopLobbyHeartbeat()
    {
        if (heartbeatCoroutine != null)
        {
            StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = null;
        }
    }

    IEnumerator LobbyHeartbeat()
    {
        while (currentUnityLobby != null && isHost)
        {
            yield return new WaitForSecondsRealtime(15f);
            HeartbeatLobbyAsync();
        }
    }

    async void HeartbeatLobbyAsync()
    {
        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(currentUnityLobby.Id);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Heartbeat failed: {e.Message}");
        }
    }

    #endregion

    #region Chat System

    void SendChatMessage()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        string msgText = chatInput.text;
        if (string.IsNullOrEmpty(msgText)) return;

        AddChatMessage(currentPlayerName, msgText);

        if (networkSync != null)
        {
            networkSync.SendChat(msgText, currentPlayerName);
        }

        chatInput.text = "";
        chatInput.Select();
        chatInput.ActivateInputField();
    }

    public void AddChatMessage(string sender, string message)
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        if (!lobbyChatMessages.ContainsKey(currentLobbyId))
            lobbyChatMessages[currentLobbyId] = new List<string>();

        string formattedMessage = $"<b>{sender}:</b> {message}";
        lobbyChatMessages[currentLobbyId].Add(formattedMessage);

        if (lobbyChatMessages[currentLobbyId].Count > maxChatMessages)
        {
            lobbyChatMessages[currentLobbyId].RemoveAt(0);
        }

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
        if (remotePlayer != null && networkSync != null)
        {
            Debug.Log($"Sending move {move} to {remotePlayer.playerName}");
            networkSync.SendMove(move);
        }
    }

    public void SendGameMessage(string message)
    {
        AddChatMessage(currentPlayerName, message);
    }

    public void EndGame(string result)
    {
        AddChatMessage("System", $"Game ended: {result}");

        if (currentUnityLobby != null)
        {
            // Reset ready state
            ToggleReady();
        }
    }

    public void SwapPlayerColors()
    {
        if (localPlayer != null)
        {
            var newLocalColor = localPlayer.color == ChessRules.PieceColor.White ?
                ChessRules.PieceColor.Black : ChessRules.PieceColor.White;

            localPlayer = new PlayerData
            {
                playerId = localPlayer.playerId,
                playerName = localPlayer.playerName,
                isHost = localPlayer.isHost,
                isReady = localPlayer.isReady,
                color = newLocalColor
            };
        }

        if (remotePlayer != null)
        {
            var newRemoteColor = remotePlayer.color == ChessRules.PieceColor.White ?
                ChessRules.PieceColor.Black : ChessRules.PieceColor.White;

            remotePlayer = new PlayerData
            {
                playerId = remotePlayer.playerId,
                playerName = remotePlayer.playerName,
                isHost = remotePlayer.isHost,
                isReady = remotePlayer.isReady,
                color = newRemoteColor
            };
        }

        UpdateLobbyRoomUI();
    }

    #endregion

    #region Debug Methods

    public void DebugCheckLobbyListUI()
    {
        Debug.Log("=== CHECKING LOBBY LIST UI SETUP ===");

        if (lobbyListContent == null)
        {
            Debug.LogError("lobbyListContent is NULL!");
            return;
        }

        Debug.Log($"lobbyListContent exists: {lobbyListContent.name}");
        Debug.Log($"Content has {lobbyListContent.childCount} children");

        RectTransform contentRect = lobbyListContent.GetComponent<RectTransform>();
        Debug.Log($"Content RectTransform size: {contentRect.rect.width} x {contentRect.rect.height}");

        var layoutGroup = lobbyListContent.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            Debug.Log($"Has LayoutGroup: {layoutGroup.GetType().Name}, enabled: {layoutGroup.enabled}");
        }

        var sizeFitter = lobbyListContent.GetComponent<ContentSizeFitter>();
        if (sizeFitter != null)
        {
            Debug.Log($"Has ContentSizeFitter - V: {sizeFitter.verticalFit}, H: {sizeFitter.horizontalFit}");
        }

        ScrollRect scrollRect = lobbyListContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            Debug.Log($"Found ScrollRect, content assigned: {scrollRect.content != null}");
        }

        if (lobbyItemPrefab != null)
        {
            Debug.Log($"Prefab assigned: {lobbyItemPrefab.name}");
        }

        Debug.Log("=== END UI CHECK ===");
    }

    public async void TestUnityServices()
    {
        Debug.Log("=== TESTING UNITY SERVICES ===");

        try
        {
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log($"[AUTH] Signed in as: {AuthenticationService.Instance.PlayerId}");
            }

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();
            Debug.Log($"[QUERY] Found: {response.Results.Count} lobbies");

            var testLobby = await LobbyService.Instance.CreateLobbyAsync(
                "Test Lobby " + Random.Range(1000, 9999),
                2,
                new CreateLobbyOptions { IsPrivate = false }
            );
            Debug.Log($"[CREATE] Test lobby created: {testLobby.Id}");

            await Task.Delay(500);
            response = await LobbyService.Instance.QueryLobbiesAsync();
            Debug.Log($"[QUERY] After creating: {response.Results.Count} lobbies");

            await LobbyService.Instance.DeleteLobbyAsync(testLobby.Id);
            Debug.Log("[DELETE] Test lobby deleted");

            Debug.Log("=== TESTS PASSED ===");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TEST ERROR] {e.Message}");
        }
    }

    #endregion

    async void OnDestroy()
    {
        StopLobbyHeartbeat();
        StopLobbyPolling();

        if (isHost && currentUnityLobby != null)
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(currentUnityLobby.Id);
            }
            catch { }
        }
    }
}