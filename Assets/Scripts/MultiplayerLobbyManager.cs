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

    // Keep this for chat (will sync via Netcode later)
    private Dictionary<string, List<string>> lobbyChatMessages = new Dictionary<string, List<string>>();

    // Unity Services variables
    private Lobby currentUnityLobby;
    private List<Lobby> availableLobbies = new List<Lobby>();
    private string joinCode;
    private Coroutine heartbeatCoroutine;
    private ChessNetworkSync networkSync;

    // Current session data
    private string currentLobbyId;
    private string currentPlayerId;
    public string currentPlayerName { get; private set; }  // Made public property for network sync
    private bool isHost;
    public PlayerData localPlayer { get; private set; }    // Made public property for network sync
    public PlayerData remotePlayer { get; private set; }   // Made public property for network sync

    // References to game components
    private ChessRules chessRules;
    private BlindfoldMultiplayerUI multiplayerUI;

    async void Start()
    {
        // Initialize Unity Services
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                currentPlayerId = AuthenticationService.Instance.PlayerId;
                Debug.Log($"Signed in with Player ID: {currentPlayerId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
        }

        InitializeUI();
        GeneratePlayerId();

        // Get chess components - use new Unity method
        chessRules = FindFirstObjectByType<ChessRules>();
        if (chessRules == null)
        {
            Debug.LogError("ChessRules component not found!");
        }

        // Get network sync - use new Unity method
        networkSync = FindFirstObjectByType<ChessNetworkSync>();
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

        // Direct join button
        if (directJoinButton != null)
        {
            directJoinButton.onClick.AddListener(() => DirectJoinLobby());
            directJoinCancelButton.onClick.AddListener(() => { directJoinPanel.SetActive(false); });
        }

        // SADECE KENDI PANEL KONTROLÜ - DİĞER MODLARA BULAŞMA
        passwordGroup.SetActive(false);

        // EĞER LOBBY LIST PANEL AÇIKSA REFRESH YAP
        if (lobbyListPanel.activeSelf)
        {
            RefreshLobbyList();
        }
    }
    void GeneratePlayerId()
    {
        // Use Unity's authenticated player ID
        currentPlayerId = AuthenticationService.Instance.PlayerId;
        if (string.IsNullOrEmpty(currentPlayerName))
            currentPlayerName = "Player" + Random.Range(1000, 9999);
    }

    #region Panel Management

    public void ShowLobbyListPanel()
    {
        lobbyListPanel.SetActive(true);
        if (createLobbyPanel) createLobbyPanel.SetActive(false);
        if (lobbyRoomPanel) lobbyRoomPanel.SetActive(false);
        RefreshLobbyList();
    }

    void ShowCreateLobbyPanel()
    {
        createLobbyPanel.SetActive(true);
        lobbyListPanel.SetActive(false);

        // Reset input fields
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
    }

    public void ShowGamePanel()  // Made public for network sync
    {
        gamePanel.SetActive(true);
        if (lobbyRoomPanel) lobbyRoomPanel.SetActive(false);

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

    void ShowMainMenu()
    {
        StopLobbyHeartbeat();
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

            // Create Unity Lobby - ENSURE IT'S PUBLIC
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,  // FORCE PUBLIC for testing
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
                { "Password", new DataObject(DataObject.VisibilityOptions.Public, "") }
                // Removed GameMode for now
            }
            };

            currentUnityLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, 2, options);

            Debug.Log($"[SUCCESS] Created lobby: {currentUnityLobby.Name}");
            Debug.Log($"[SUCCESS] Lobby ID: {currentUnityLobby.Id}");
            Debug.Log($"[SUCCESS] Lobby Code: {currentUnityLobby.LobbyCode}");
            Debug.Log($"[SUCCESS] Is Private: {currentUnityLobby.IsPrivate}");
            Debug.Log($"[SUCCESS] Max Players: {currentUnityLobby.MaxPlayers}");

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

            // TEST: Query lobbies immediately after creating to see if it appears
            await Task.Delay(1000); // Wait 1 second
            Debug.Log("[TEST] Querying lobbies after creation...");
            QueryResponse testQuery = await LobbyService.Instance.QueryLobbiesAsync();
            Debug.Log($"[TEST] Found {testQuery.Results.Count} lobbies after creating");
            foreach (var lobby in testQuery.Results)
            {
                Debug.Log($"[TEST] - {lobby.Name} (ID: {lobby.Id})");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ERROR] Failed to create lobby: {e.Message}");
            Debug.LogError($"[ERROR] Stack trace: {e.StackTrace}");
            ShowLobbyListPanel();
        }
    }
    public async void TestUnityServices()
    {
        Debug.Log("=== TESTING UNITY SERVICES CONNECTION ===");

        try
        {
            // Check authentication
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log($"[AUTH] Signed in as: {AuthenticationService.Instance.PlayerId}");
            }
            else
            {
                Debug.LogError("[AUTH] Not signed in!");
                return;
            }

            // Try to query all lobbies
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync();
            Debug.Log($"[QUERY] Successfully queried lobbies. Found: {response.Results.Count}");

            // Try to create a test lobby
            var testLobby = await LobbyService.Instance.CreateLobbyAsync(
                "Test Lobby " + Random.Range(1000, 9999),
                2,
                new CreateLobbyOptions { IsPrivate = false }
            );
            Debug.Log($"[CREATE] Successfully created test lobby: {testLobby.Id}");

            // Query again
            await Task.Delay(500);
            response = await LobbyService.Instance.QueryLobbiesAsync();
            Debug.Log($"[QUERY] After creating test lobby, found: {response.Results.Count} lobbies");

            // Delete test lobby
            await LobbyService.Instance.DeleteLobbyAsync(testLobby.Id);
            Debug.Log("[DELETE] Test lobby deleted");

            Debug.Log("=== ALL TESTS PASSED ===");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TEST ERROR] {e.Message}");
        }
    }
    #endregion

    #region Lobby List Management

    async void RefreshLobbyList()
    {
        try
        {
            Debug.Log("=== STARTING LOBBY REFRESH ===");

            // SIMPLIFIED QUERY - No filters at first to see ALL lobbies
            QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
            {
                Count = 25
                // Removed ALL filters temporarily to see if lobbies appear
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            availableLobbies = queryResponse.Results;

            Debug.Log($"[LOBBY QUERY] Found {availableLobbies.Count} total lobbies from Unity");

            // Log details of each lobby found
            foreach (var lobby in availableLobbies)
            {
                Debug.Log($"[LOBBY] Name: {lobby.Name}, ID: {lobby.Id}, Players: {lobby.Players.Count}/{lobby.MaxPlayers}, Private: {lobby.IsPrivate}");
            }

            // Clear existing items
            foreach (Transform child in lobbyListContent)
            {
                Destroy(child.gameObject);
            }

            // Don't filter by search for now - show ALL lobbies
            foreach (var lobby in availableLobbies)
            {
                CreateLobbyListItem(lobby);
            }

            Debug.Log($"[UI] Created {lobbyListContent.childCount} UI items in content panel");
            Debug.Log("=== LOBBY REFRESH COMPLETE ===");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ERROR] Failed to refresh lobbies: {e.Message}");
            Debug.LogError($"[ERROR] Stack trace: {e.StackTrace}");
        }
        Debug.Log($"[UI] Created {lobbyListContent.childCount} UI items in content panel");
        Debug.Log("=== LOBBY REFRESH COMPLETE ===");

        // Force UI update
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyListContent.GetComponent<RectTransform>());

        // Debug check
        DebugCheckLobbyListUI();
    }

    void CreateLobbyListItem(Lobby lobby)
    {
        if (lobbyItemPrefab == null || lobbyListContent == null)
        {
            Debug.LogError("Missing prefab or content reference!");
            return;
        }

        GameObject item = Instantiate(lobbyItemPrefab, lobbyListContent);

        // FORCE the item to be active
        item.SetActive(true);

        // Make sure it has a proper RectTransform
        RectTransform itemRect = item.GetComponent<RectTransform>();
        if (itemRect == null)
        {
            itemRect = item.AddComponent<RectTransform>();
        }

        // Set anchoring for vertical list
        itemRect.anchorMin = new Vector2(0, 1);
        itemRect.anchorMax = new Vector2(1, 1);
        itemRect.pivot = new Vector2(0.5f, 1);

        // Set size if needed (adjust these values based on your design)
        if (itemRect.rect.height < 10) // If height is too small
        {
            itemRect.sizeDelta = new Vector2(itemRect.sizeDelta.x, 80); // Set a minimum height
        }

        Debug.Log($"Created lobby item: {item.name} at position {item.transform.localPosition}");

        // Rest of your code for setting up the item...
        string hostName = "Unknown";
        var hostPlayer = lobby.Players.FirstOrDefault(p => p.Id == lobby.HostId);
        if (hostPlayer != null && hostPlayer.Data.ContainsKey("PlayerName"))
        {
            hostName = hostPlayer.Data["PlayerName"].Value;
        }

        // Set up the UI elements...
        TMP_Text lobbyNameText = item.transform.Find("LobbyName")?.GetComponent<TMP_Text>();
        TMP_Text hostNameText = item.transform.Find("HostName")?.GetComponent<TMP_Text>();
        TMP_Text playerCountText = item.transform.Find("PlayerCount")?.GetComponent<TMP_Text>();
        Image lockIcon = item.transform.Find("LockIcon")?.GetComponent<Image>();
        Button joinButton = item.GetComponent<Button>() ?? item.transform.Find("JoinButton")?.GetComponent<Button>();

        if (lobbyNameText) lobbyNameText.text = lobby.Name;
        if (hostNameText) hostNameText.text = "Host: " + hostName;
        if (playerCountText) playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
        if (lockIcon) lockIcon.gameObject.SetActive(lobby.IsPrivate);

        if (joinButton)
        {
            joinButton.onClick.AddListener(() => AttemptJoinLobby(lobby));
        }

        // Force layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(lobbyListContent.GetComponent<RectTransform>());
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
        pendingLobby = lobby;

        if (lobby.Data.ContainsKey("Password") && !string.IsNullOrEmpty(lobby.Data["Password"].Value))
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
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, currentPlayerName) },
                        { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") },
                        { "Color", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "Black") }
                    }
                }
            };

            currentUnityLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, options);

            string relayJoinCode = currentUnityLobby.Data["JoinCode"].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            // Configure Unity Transport
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
                color = ChessRules.PieceColor.Black
            };

            if (!lobbyChatMessages.ContainsKey(currentLobbyId))
                lobbyChatMessages[currentLobbyId] = new List<string>();

            ShowLobbyRoomPanel();
            AddChatMessage("System", $"{currentPlayerName} joined the lobby.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            ShowLobbyListPanel();
        }
    }
    public void DebugCheckLobbyListUI()
    {
        Debug.Log("=== CHECKING LOBBY LIST UI SETUP ===");

        // Check if content exists
        if (lobbyListContent == null)
        {
            Debug.LogError("lobbyListContent is NULL! Assign it in the Inspector!");
            return;
        }

        Debug.Log($"lobbyListContent exists: {lobbyListContent.name}");
        Debug.Log($"Content has {lobbyListContent.childCount} children");

        // Check the RectTransform
        RectTransform contentRect = lobbyListContent.GetComponent<RectTransform>();
        Debug.Log($"Content RectTransform size: {contentRect.rect.width} x {contentRect.rect.height}");
        Debug.Log($"Content position: {contentRect.anchoredPosition}");

        // Check if there's a Layout Group
        var layoutGroup = lobbyListContent.GetComponent<LayoutGroup>();
        if (layoutGroup != null)
        {
            Debug.Log($"Has LayoutGroup: {layoutGroup.GetType().Name}");
            Debug.Log($"LayoutGroup enabled: {layoutGroup.enabled}");
        }
        else
        {
            Debug.LogWarning("No LayoutGroup on content! Add VerticalLayoutGroup or GridLayoutGroup!");
        }

        // Check if there's a Content Size Fitter
        var sizeFitter = lobbyListContent.GetComponent<ContentSizeFitter>();
        if (sizeFitter != null)
        {
            Debug.Log($"Has ContentSizeFitter - Vertical: {sizeFitter.verticalFit}, Horizontal: {sizeFitter.horizontalFit}");
        }
        else
        {
            Debug.LogWarning("No ContentSizeFitter! This might be needed for scrolling.");
        }

        // Check the scroll rect
        ScrollRect scrollRect = lobbyListContent.GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            Debug.Log($"Found ScrollRect in parent");
            Debug.Log($"ScrollRect content is assigned: {scrollRect.content != null}");
            Debug.Log($"ScrollRect viewport is assigned: {scrollRect.viewport != null}");
        }
        else
        {
            Debug.LogError("No ScrollRect found in parent! Your content needs to be inside a ScrollRect!");
        }

        // Check children visibility
        foreach (Transform child in lobbyListContent)
        {
            Debug.Log($"Child: {child.name}, Active: {child.gameObject.activeSelf}, Position: {child.localPosition}");
            RectTransform childRect = child.GetComponent<RectTransform>();
            if (childRect != null)
            {
                Debug.Log($"  - Size: {childRect.rect.width} x {childRect.rect.height}");
            }
        }

        // Check the prefab
        if (lobbyItemPrefab != null)
        {
            Debug.Log($"Prefab assigned: {lobbyItemPrefab.name}");
            RectTransform prefabRect = lobbyItemPrefab.GetComponent<RectTransform>();
            if (prefabRect != null)
            {
                Debug.Log($"Prefab size: {prefabRect.rect.width} x {prefabRect.rect.height}");
            }
        }
        else
        {
            Debug.LogError("lobbyItemPrefab is NULL!");
        }

        Debug.Log("=== END UI CHECK ===");
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
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
            {
                Player = new Player
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, currentPlayerName) },
                        { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") },
                        { "Color", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "Black") }
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
                color = ChessRules.PieceColor.Black
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

            StopLobbyHeartbeat();
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

    public async void UpdateLobbyRoomUI()  // Made public for network sync
    {
        if (currentUnityLobby == null) return;

        try
        {
            currentUnityLobby = await LobbyService.Instance.GetLobbyAsync(currentUnityLobby.Id);

            lobbyNameText.text = currentUnityLobby.Name;
            lobbyIdText.text = "Code: " + currentUnityLobby.LobbyCode;

            Player player1 = null;
            Player player2 = null;

            foreach (var player in currentUnityLobby.Players)
            {
                if (player.Data["Color"].Value == "White")
                    player1 = player;
                else if (player.Data["Color"].Value == "Black")
                    player2 = player;
            }

            player1NameText.text = player1 != null ? player1.Data["PlayerName"].Value : "Waiting...";
            player2NameText.text = player2 != null ? player2.Data["PlayerName"].Value : "Waiting...";

            bool player1Ready = player1 != null && player1.Data["IsReady"].Value == "true";
            bool player2Ready = player2 != null && player2.Data["IsReady"].Value == "true";

            player1ReadyIndicator.color = player1Ready ? readyColor : notReadyColor;
            player2ReadyIndicator.color = player2Ready ? readyColor : notReadyColor;

            readyButton.interactable = currentUnityLobby.Players.Count == 2;
            startGameButton.interactable = isHost && currentUnityLobby.Players.Count == 2 &&
                player1Ready && player2Ready;
            colorSwapButton.interactable = isHost && currentUnityLobby.Players.Count == 2;

            TMP_Text readyButtonText = readyButton.GetComponentInChildren<TMP_Text>();
            if (readyButtonText != null)
            {
                var myPlayer = currentUnityLobby.Players.FirstOrDefault(p => p.Id == currentPlayerId);
                bool isReady = myPlayer != null && myPlayer.Data["IsReady"].Value == "true";
                readyButtonText.text = isReady ? "Not Ready" : "Ready";
                localPlayer.isReady = isReady;
            }

            var otherPlayer = currentUnityLobby.Players.FirstOrDefault(p => p.Id != currentPlayerId);
            if (otherPlayer != null)
            {
                remotePlayer = new PlayerData
                {
                    playerId = otherPlayer.Id,
                    playerName = otherPlayer.Data["PlayerName"].Value,
                    isHost = otherPlayer.Id == currentUnityLobby.HostId,
                    isReady = otherPlayer.Data["IsReady"].Value == "true",
                    color = otherPlayer.Data["Color"].Value == "White" ?
                        ChessRules.PieceColor.White : ChessRules.PieceColor.Black
                };
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to update lobby UI: {e.Message}");
        }
    }

    async void ToggleReady()
    {
        if (currentUnityLobby == null) return;

        try
        {
            var myPlayer = currentUnityLobby.Players.FirstOrDefault(p => p.Id == currentPlayerId);
            bool currentReady = myPlayer != null && myPlayer.Data["IsReady"].Value == "true";

            UpdatePlayerOptions options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, currentPlayerName) },
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, (!currentReady).ToString().ToLower()) },
                    { "Color", myPlayer.Data["Color"] }
                }
            };

            currentUnityLobby = await LobbyService.Instance.UpdatePlayerAsync(currentUnityLobby.Id, currentPlayerId, options);

            localPlayer.isReady = !currentReady;
            UpdateLobbyRoomUI();

            string status = localPlayer.isReady ? "ready" : "not ready";
            AddChatMessage("System", $"{localPlayer.playerName} is {status}.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to toggle ready: {e.Message}");
        }
    }

    void SwapColors()
    {
        if (!isHost || currentUnityLobby == null || currentUnityLobby.Players.Count != 2) return;

        if (networkSync != null)
        {
            networkSync.RequestColorSwap();
        }

        AddChatMessage("System", "Colors swapped!");
    }

    void StartGame()
    {
        if (!isHost) return;

        if (currentUnityLobby == null || currentUnityLobby.Players.Count != 2) return;

        AddChatMessage("System", "Game starting!");

        if (networkSync != null)
        {
            networkSync.StartGameForAll();
        }

        ShowGamePanel();
    }

    #endregion

    #region Unity Lobby Heartbeat

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
            ToggleReady(); // This will set to not ready
        }

        // DON'T automatically go back to lobby room - let player decide
        // ShowLobbyRoomPanel();
    }

    // Add this method to handle color swapping from network sync
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
    }

    #endregion

    async void OnDestroy()
    {
        StopLobbyHeartbeat();

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