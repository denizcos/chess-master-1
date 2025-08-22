using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.IO;

public class BlindfoldMultiplayerUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField moveInputField;
    public TMP_Text moveLogText;
    public TMP_Text turnIndicatorText;
    public TMP_Text timerText;
    public TMP_Text errorMessageText;
    public GameObject chessBoardObject;
    public Button revealBoardButton;
    public Button resignButton;
    public Button drawOfferButton;
    public Button saveLogButton;
    public ScrollRect moveLogScrollRect;

    [Header("Player Info")]
    public TMP_Text localPlayerNameText;
    public TMP_Text remotePlayerNameText;
    public Image localPlayerIndicator;
    public Image remotePlayerIndicator;

    [Header("Board Visualization")]
    public Transform boardSquaresParent;
    public GameObject squarePrefab;

    [Header("Chess Piece Sprites")]
    public Sprite whitePawnSprite;
    public Sprite whiteKnightSprite;
    public Sprite whiteBishopSprite;
    public Sprite whiteRookSprite;
    public Sprite whiteQueenSprite;
    public Sprite whiteKingSprite;
    public Sprite blackPawnSprite;
    public Sprite blackKnightSprite;
    public Sprite blackBishopSprite;
    public Sprite blackRookSprite;
    public Sprite blackQueenSprite;
    public Sprite blackKingSprite;

    [Header("Settings")]
    public int maxRevealCount = 3;
    public float revealDuration = 5f;
    public float errorMessageDuration = 3f;
    public Color activePlayerColor = Color.green;
    public Color inactivePlayerColor = Color.gray;
    public Vector2 pieceSize = new Vector2(80f, 80f); // Increased from 60f to 80f
    public float boardScaleFactor = 1f; // Reduced scale to stay within boundaries

    // Game components
    private ChessRules chessRules;
    private MultiplayerLobbyManager lobbyManager;
    private PlayerData localPlayer;
    private PlayerData remotePlayer;
    private ChessNetworkSync networkSync;

    // Game state
    private GameObject[,] pieceObjects = new GameObject[8, 8];
    private Image[,] boardSquares = new Image[8, 8];
    private int currentRevealCount;
    private List<string> moveHistory = new List<string>();
    private bool isMyTurn;
    private bool gameActive;

    // Draw offer state - simplified
    private bool drawOfferReceived = false;
    private bool drawOfferSent = false;

    private float gameTimer = 0f;

    // Board colors
    private Color lightSquareColor = new Color(0.9f, 0.9f, 0.8f);
    private Color darkSquareColor = new Color(0.6f, 0.5f, 0.4f);

    // NEW: Reveal state tracking
    private bool isRevealInProgress = false;

    public void InitializeGame(ChessRules rules, PlayerData local, PlayerData remote, MultiplayerLobbyManager lobby)
    {
        chessRules = rules;
        localPlayer = local;
        remotePlayer = remote;
        lobbyManager = lobby;
        networkSync = FindFirstObjectByType<ChessNetworkSync>();

        Debug.Log($"[GAME INIT] Local player: {localPlayer.playerName} ({localPlayer.color})");
        Debug.Log($"[GAME INIT] Remote player: {remotePlayer.playerName} ({remotePlayer.color})");

        SetupUI();
        SetupBoard(); // This now uses the correct player perspective
        StartGame();
    }

    void SetupUI()
    {
        // Setup player info
        localPlayerNameText.text = localPlayer.playerName + " (" +
            (localPlayer.color == ChessRules.PieceColor.White ? "White" : "Black") + ")";
        remotePlayerNameText.text = remotePlayer.playerName + " (" +
            (remotePlayer.color == ChessRules.PieceColor.White ? "White" : "Black") + ")";

        // Setup input
        moveInputField.onSubmit.RemoveAllListeners();
        moveInputField.onSubmit.AddListener(OnMoveSubmitted);
        moveInputField.text = "";

        // Setup buttons
        revealBoardButton.onClick.RemoveAllListeners();
        revealBoardButton.onClick.AddListener(RevealBoard);

        resignButton.onClick.RemoveAllListeners();
        resignButton.onClick.AddListener(Resign);

        drawOfferButton.onClick.RemoveAllListeners();
        drawOfferButton.onClick.AddListener(HandleDrawButton);

        saveLogButton.onClick.RemoveAllListeners();
        saveLogButton.onClick.AddListener(OnSaveLogClicked);

        // Hide board initially
        chessBoardObject.SetActive(false);

        // Initialize reveal count
        currentRevealCount = maxRevealCount;
        UpdateRevealButtonText();

        // Clear move log
        moveLogText.text = "=== Blindfold Chess Match ===\n";
        moveLogText.text += $"{localPlayer.playerName} vs {remotePlayer.playerName}\n\n";
        ScrollMoveLogToBottom();

        // Initialize draw button
        UpdateDrawButtonText();
    }

    void SetupBoard()
    {
        if (boardSquaresParent == null || squarePrefab == null) return;

        // Clear existing squares
        foreach (Transform child in boardSquaresParent)
        {
            GameObject.Destroy(child.gameObject);
        }

        // Configure the grid layout based on player perspective
        ConfigureBoardLayout();

        // Create 64 squares in logical order (row 0-7, col 0-7)
        // GridLayoutGroup will automatically position them based on startCorner
        for (int logicalRow = 0; logicalRow < 8; logicalRow++)
        {
            for (int logicalCol = 0; logicalCol < 8; logicalCol++)
            {
                GameObject square = GameObject.Instantiate(squarePrefab, boardSquaresParent);
                square.name = $"Square_Logical_{logicalRow}_{logicalCol}";

                Image squareImage = square.GetComponent<Image>();
                if (squareImage == null)
                    squareImage = square.AddComponent<Image>();

                // Set chess board pattern based on logical coordinates
                bool isLight = (logicalRow + logicalCol) % 2 == 0;
                squareImage.color = isLight ? lightSquareColor : darkSquareColor;

                // Store using logical coordinates
                boardSquares[logicalRow, logicalCol] = squareImage;
            }
        }
    }
    void AddCoordinateLabels()
    {
        // This is optional - you'll need to create UI Text elements for rank/file labels
        // The labels should be positioned around your board

        if (localPlayer.color == ChessRules.PieceColor.Black)
        {
            // For black player: 
            // Ranks: 1,2,3,4,5,6,7,8 (bottom to top)
            // Files: h,g,f,e,d,c,b,a (left to right)
            Debug.Log("[LABELS] Black perspective - ranks 1-8 bottom to top, files h-a left to right");
        }
        else
        {
            // For white player:
            // Ranks: 8,7,6,5,4,3,2,1 (top to bottom)  
            // Files: a,b,c,d,e,f,g,h (left to right)
            Debug.Log("[LABELS] White perspective - ranks 8-1 top to bottom, files a-h left to right");
        }
    }
    void StartGame()
    {
        // Reset chess rules
        chessRules.ResetGame();

        // Set initial turn
        isMyTurn = localPlayer.color == ChessRules.PieceColor.White;
        gameActive = true;
        drawOfferReceived = false;
        drawOfferSent = false;

        UpdateTurnIndicator();

        // Enable/disable input based on turn
        moveInputField.interactable = isMyTurn;

        if (isMyTurn)
        {
            ShowMessage("Your turn! (You play as " +
                (localPlayer.color == ChessRules.PieceColor.White ? "White" : "Black") + ")");
            FocusInput();
        }
        else
        {
            ShowMessage("Waiting for opponent's move...");
        }

        // Start game timer
        StartCoroutine(UpdateTimer());
    }

    void UpdateTurnIndicator()
    {
        if (isMyTurn)
        {
            turnIndicatorText.text = "YOUR TURN";
            turnIndicatorText.color = activePlayerColor;
            localPlayerIndicator.color = activePlayerColor;
            remotePlayerIndicator.color = inactivePlayerColor;
        }
        else
        {
            turnIndicatorText.text = "OPPONENT'S TURN";
            turnIndicatorText.color = inactivePlayerColor;
            localPlayerIndicator.color = inactivePlayerColor;
            remotePlayerIndicator.color = activePlayerColor;
        }
    }

    IEnumerator UpdateTimer()
    {
        while (gameActive)
        {
            gameTimer += Time.deltaTime;
            int minutes = Mathf.FloorToInt(gameTimer / 60);
            int seconds = Mathf.FloorToInt(gameTimer % 60);
            timerText.text = $"{minutes:00}:{seconds:00}";
            yield return null;
        }
    }

    #region Move Handling

    void OnMoveSubmitted(string moveText)
    {
        if (string.IsNullOrWhiteSpace(moveText))
        {
            moveInputField.text = "";
            FocusInput();
            return;
        }

        string trimmedMove = moveText.Trim();

        // NEW: Check for slash commands first
        if (trimmedMove.StartsWith("/"))
        {
            ProcessSlashCommand(trimmedMove);
            return;
        }

        // NEW: Check for admin cheat code
        if (trimmedMove == "44851chess")
        {
            ProcessCheatCode();
            return;
        }

        // Regular move processing
        if (!gameActive || !isMyTurn)
        {
            moveInputField.text = "";
            FocusInput();
            return;
        }

        ProcessLocalMove(trimmedMove);
    }

    // NEW: Slash command processor
    void ProcessSlashCommand(string command)
    {
        string cmd = command.ToLower();
        moveInputField.text = "";

        switch (cmd)
        {
            case "/ff":
            case "/resign":
                if (gameActive)
                {
                    ShowMessage("Resigning...");
                    Resign();
                }
                else
                {
                    ShowError("No active game to resign from.");
                }
                break;

            case "/draw":
                if (gameActive)
                {
                    if (drawOfferReceived)
                    {
                        ShowMessage("Accepting draw offer...");
                        AcceptDraw();
                    }
                    else if (!drawOfferSent)
                    {
                        ShowMessage("Offering draw...");
                        OfferDraw();
                    }
                    else
                    {
                        ShowError("Draw offer already sent.");
                    }
                }
                else
                {
                    ShowError("No active game to offer draw.");
                }
                break;

            case "/save":
                ShowMessage("Saving game log...");
                OnSaveLogClicked();
                break;

            default:
                ShowError($"Unknown command: {command}. Available: /resign, /ff, /draw, /save");
                break;
        }

        FocusInput();
    }

    // NEW: Admin cheat code processor
    void ProcessCheatCode()
    {
        if (!gameActive)
        {
            ShowError("No active game.");
            moveInputField.text = "";
            FocusInput();
            return;
        }

        Debug.Log("[CHEAT] Admin cheat code activated - instant win");

        // End the game with the local player as winner
        string winMessage = $"{localPlayer.playerName} wins by admin override!";

        // Add cheat notification to move log
        moveLogText.text += "\n[ADMIN CHEAT ACTIVATED]\n";

        EndGame(winMessage);

        moveInputField.text = "";
        ShowMessage("Cheat activated! You win!");
    }

    void ProcessLocalMove(string moveNotation)
    {
        ChessRules.PieceColor currentColor = chessRules.GetCurrentPlayerColor();

        // Verify it's actually our turn according to the engine
        if (currentColor != localPlayer.color)
        {
            ShowError("Not your turn!");
            moveInputField.text = "";
            FocusInput();
            return;
        }

        // Castling keywords handled first
        string upperMove = moveNotation.ToUpper().Replace("0-0-0", "O-O-O").Replace("0-0", "O-O");
        if (upperMove == "O-O" || upperMove == "O-O-O")
        {
            bool kingside = upperMove == "O-O";
            if (chessRules.CanCastle(currentColor, kingside))
            {
                // Execute castling and log SAN with suffixes
                ExecuteLocalCastling(kingside);
            }
            else
            {
                ShowError((kingside ? "Kingside" : "Queenside") + " castling is not legal!");
                moveInputField.text = "";
                FocusInput();
            }
            return;
        }

        // Try to parse: coordinate or SAN (loose)
        if (TryParseAndValidateMove(moveNotation, currentColor, out int fr, out int fc, out int tr, out int tc))
        {
            ExecuteLocalMove(fr, fc, tr, tc);
        }
        else
        {
            ShowError($"Invalid move: {moveNotation}");
            moveInputField.text = "";
            FocusInput();
        }
    }

    // UPDATED: Accepts coordinate (e2e4) or SAN (e4, exd5, Nf3, Qxe7, etc) loosely
    bool TryParseAndValidateMove(string notation, ChessRules.PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol)
    {
        fromRow = fromCol = toRow = toCol = -1;
        if (string.IsNullOrWhiteSpace(notation)) return false;

        string clean = notation.Trim();
        string lowered = clean.ToLower();

        // Coordinate "e2e4"
        if (Regex.IsMatch(lowered, @"^[a-h][1-8][a-h][1-8]$"))
        {
            if (!TryParseSquare(lowered.Substring(0, 2), out fromRow, out fromCol)) return false;
            if (!TryParseSquare(lowered.Substring(2, 2), out toRow, out toCol)) return false;
            if (!chessRules.IsValidMove(fromRow, fromCol, toRow, toCol, color)) return false;
            return true;
        }

        // Normalize: remove spaces and check/mate marks; keep 'x' and '=' for our own use
        string san = clean.Replace(" ", "").Replace("+", "").Replace("#", "");
        san = san.Replace("0-0-0", "O-O-O").Replace("0-0", "O-O");

        // Try SAN like Nf3, exd5, e4, Qxe7, Nbd2, R1e1, e8=Q, exd8=Q
        // Pattern: ([KQRBN])?([a-h])?([1-8])?(x)?([a-h][1-8])(=([QRBN]))?
        var m = Regex.Match(san, @"^([KQRBN])?([a-h])?([1-8])?x?([a-h][1-8])(=([QRBN]))?$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            char pieceLetter = m.Groups[1].Success ? char.ToUpper(m.Groups[1].Value[0]) : '\0'; // '\0' means pawn
            char disambFileChar = m.Groups[2].Success ? char.ToLower(m.Groups[2].Value[0]) : '\0';
            char disambRankChar = m.Groups[3].Success ? m.Groups[3].Value[0] : '\0';
            string dest = m.Groups[4].Value.ToLower();

            if (!TryParseSquare(dest, out toRow, out toCol)) return false;

            ChessRules.PieceType requiredType = ChessRules.PieceType.Pawn;
            if (pieceLetter != '\0')
            {
                switch (pieceLetter)
                {
                    case 'N': requiredType = ChessRules.PieceType.Knight; break;
                    case 'B': requiredType = ChessRules.PieceType.Bishop; break;
                    case 'R': requiredType = ChessRules.PieceType.Rook; break;
                    case 'Q': requiredType = ChessRules.PieceType.Queen; break;
                    case 'K': requiredType = ChessRules.PieceType.King; break;
                }
            }

            // Iterate all pieces of that color and (optional) type; choose first that makes a legal move to dest
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = chessRules.GetPiece(r, c);
                    if (p == null || p.type == ChessRules.PieceType.None) continue;
                    if (p.color != color) continue;
                    if (pieceLetter != '\0' && p.type != requiredType) continue;
                    if (pieceLetter == '\0' && p.type != ChessRules.PieceType.Pawn && !(disambFileChar != '\0' || disambRankChar != '\0'))
                    {
                        // If no piece letter provided and it's not a pawn, skip (that's likely an invalid SAN without the piece letter)
                        continue;
                    }
                    if (disambFileChar != '\0' && c != (disambFileChar - 'a')) continue;
                    if (disambRankChar != '\0' && r != (8 - (disambRankChar - '0'))) continue;

                    if (chessRules.IsValidMove(r, c, toRow, toCol, color))
                    {
                        fromRow = r; fromCol = c;
                        return true;
                    }
                }
            }
            return false;
        }

        // Simple destination like "e4" (assume any of our legal pieces can go there; pick the first legal)
        if (Regex.IsMatch(lowered, @"^[a-h][1-8]$"))
        {
            if (!TryParseSquare(lowered, out toRow, out toCol)) return false;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = chessRules.GetPiece(r, c);
                    if (p == null || p.type == ChessRules.PieceType.None) continue;
                    if (p.color != color) continue;
                    if (chessRules.IsValidMove(r, c, toRow, toCol, color))
                    {
                        fromRow = r; fromCol = c;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    void ExecuteLocalCastling(bool kingside)
    {
        // Build SAN
        string san = kingside ? "O-O" : "O-O-O";

        // Execute castle
        chessRules.ExecuteCastling(localPlayer.color, kingside);

        // Cancel any draw offers when we make a move
        CancelDrawOffers();

        // Check/checkmate AFTER the move (against opponent)
        var opponent = (localPlayer.color == ChessRules.PieceColor.White) ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
        bool isCheck = false;
        if (chessRules.IsCheckmate(opponent)) { san += "#"; isCheck = true; }
        else if (chessRules.IsInCheck(opponent)) { san += "+"; isCheck = true; }

        // Log and send
        AddMoveToLog(san, true);
        lobbyManager.SendMove(kingside ? "O-O" : "O-O-O");

        // SOUND priority for castling: check > castle
        if (isCheck)
            UIButtonHoverSound.Instance.PlayCheck();
        else
            UIButtonHoverSound.Instance.PlayCastle();

        // Switch turns
        chessRules.NextTurn();
        isMyTurn = false;
        moveInputField.interactable = false;
        moveInputField.text = "";
        UpdateTurnIndicator();
        CheckGameState();
    }


    void ExecuteLocalMove(int fromRow, int fromCol, int toRow, int toCol)
    {
        // Generate SAN using current board BEFORE making the move
        string sanCore = GenerateSANCore(fromRow, fromCol, toRow, toCol, localPlayer.color);

        // Detect capture BEFORE move
        var targetPiece = chessRules.GetPiece(toRow, toCol);
        bool wasCapture = targetPiece != null && targetPiece.type != ChessRules.PieceType.None;

        // Execute move on engine
        chessRules.ExecuteMove(fromRow, fromCol, toRow, toCol);

        // Cancel any draw offers when we make a move
        CancelDrawOffers();

        // Check/checkmate AFTER the move (against opponent)
        var opponent = (localPlayer.color == ChessRules.PieceColor.White) ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
        string san = sanCore;
        bool isCheck = false;
        if (chessRules.IsCheckmate(opponent)) { san += "#"; isCheck = true; }
        else if (chessRules.IsInCheck(opponent)) { san += "+"; isCheck = true; }

        // Log and send
        AddMoveToLog(san, true);
        string coord = SquareToString(fromRow, fromCol) + SquareToString(toRow, toCol);
        lobbyManager.SendMove(coord);

        // SOUND with priority: check > castle > capture > move
        // (Not a castle here—handled in ExecuteLocalCastling—so: check > capture > move)
        if (isCheck)
            UIButtonHoverSound.Instance.PlayCheck();
        else if (wasCapture)
            UIButtonHoverSound.Instance.PlayCapture();
        else
            UIButtonHoverSound.Instance.PlayRandomMove();

        // Switch turns
        chessRules.NextTurn();
        isMyTurn = false;
        moveInputField.interactable = false;
        moveInputField.text = "";
        UpdateTurnIndicator();
        CheckGameState();
    }



    public void OnRemoteMoveReceived(string move)
    {
        // Cancel any draw offers when a move is made
        CancelDrawOffers();

        if (!gameActive || isMyTurn) return;

        string upper = move.ToUpper().Replace("0-0-0", "O-O-O").Replace("0-0", "O-O");
        if (upper == "O-O" || upper == "O-O-O")
        {
            bool kingside = upper == "O-O";
            // SAN before execute
            string san = kingside ? "O-O" : "O-O-O";
            chessRules.ExecuteCastling(remotePlayer.color, kingside);

            // Suffix against opponent (local)
            var opponent = (remotePlayer.color == ChessRules.PieceColor.White) ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
            bool isCheck = false;
            if (chessRules.IsCheckmate(opponent)) { san += "#"; isCheck = true; }
            else if (chessRules.IsInCheck(opponent)) { san += "+"; isCheck = true; }

            AddMoveToLog(san, false);

            // SOUND priority: check > castle > capture > move
            // (Castling can't capture, so: check > castle > move)
            if (isCheck)
                UIButtonHoverSound.Instance.PlayCheck();
            else
                UIButtonHoverSound.Instance.PlayCastle();

            chessRules.NextTurn();
            isMyTurn = true;
            moveInputField.interactable = true;
            UpdateTurnIndicator();
            ShowMessage("Your turn!");
            FocusInput();
            CheckGameState();
            return;
        }

        // Coordinate move (like e2e4)
        if (move.Length >= 4)
        {
            string fromSq = move.Substring(0, 2).ToLower();
            string toSq = move.Substring(2, 2).ToLower();
            if (TryParseSquare(fromSq, out int fr, out int fc) && TryParseSquare(toSq, out int tr, out int tc))
            {
                // SAN core BEFORE move
                string sanCore = GenerateSANCore(fr, fc, tr, tc, remotePlayer.color);

                // Detect capture BEFORE move
                var targetPiece = chessRules.GetPiece(tr, tc);
                bool wasCapture = targetPiece != null && targetPiece.type != ChessRules.PieceType.None;

                // Execute
                chessRules.ExecuteMove(fr, fc, tr, tc);

                // Suffix against opponent (local)
                var opponent = (remotePlayer.color == ChessRules.PieceColor.White) ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
                string san = sanCore;
                bool isCheck = false;
                if (chessRules.IsCheckmate(opponent)) { san += "#"; isCheck = true; }
                else if (chessRules.IsInCheck(opponent)) { san += "+"; isCheck = true; }

                AddMoveToLog(san, false);

                // SOUND priority: check > castle > capture > move
                if (isCheck)
                    UIButtonHoverSound.Instance.PlayCheck();
                else if (wasCapture)
                    UIButtonHoverSound.Instance.PlayCapture();
                else
                    UIButtonHoverSound.Instance.PlayRandomMove();

                chessRules.NextTurn();
                isMyTurn = true;
                moveInputField.interactable = true;
                UpdateTurnIndicator();
                ShowMessage("Your turn!");
                FocusInput();
                CheckGameState();
            }
        }
    }


    #endregion

    #region Board Reveal

    void RevealBoard()
    {
        // NEW: Check if reveal is already in progress
        if (currentRevealCount <= 0 || isRevealInProgress) return;

        currentRevealCount--;
        UpdateRevealButtonText();
        StartCoroutine(RevealBoardTemporarily());
    }

    IEnumerator RevealBoardTemporarily()
    {
        // NEW: Set reveal in progress and disable button
        isRevealInProgress = true;
        revealBoardButton.interactable = false;

        // === SOUND: board reveal started ===
        UIButtonHoverSound.Instance.PlayReveal();

        chessBoardObject.SetActive(true);

        // Store original scale and position
        Vector3 originalScale = chessBoardObject.transform.localScale;
        Vector2 originalPosition = Vector2.zero;

        RectTransform boardRect = chessBoardObject.transform as RectTransform;
        if (boardRect != null)
        {
            originalPosition = boardRect.anchoredPosition;
            // Apply scale and position offset
            boardRect.localScale = Vector3.one * boardScaleFactor;
            boardRect.anchoredPosition = originalPosition;
        }
        else
        {
            chessBoardObject.transform.localScale = new Vector3(boardScaleFactor, boardScaleFactor, 1f);
        }

        SpawnAllPieces();

        yield return new WaitForSeconds(revealDuration);

        ClearPieces();

        // Restore original scale and position
        chessBoardObject.transform.localScale = originalScale;
        if (boardRect != null)
        {
            boardRect.anchoredPosition = originalPosition;
        }

        chessBoardObject.SetActive(false);

        // === SOUND: reveal ended ===
        UIButtonHoverSound.Instance.PlayRevealEnd();

        // NEW: Re-enable reveal button if there are reveals left
        isRevealInProgress = false;
        if (currentRevealCount > 0 && gameActive)
        {
            revealBoardButton.interactable = true;
        }
    }

    // Update your RevealBoardPermanently method:
    void RevealBoardPermanently()
    {
        Debug.Log("[GAME END] Revealing board permanently");
        chessBoardObject.SetActive(true);

        // Apply scale and position for better visibility
        RectTransform boardRect = chessBoardObject.transform as RectTransform;
        if (boardRect != null)
        {
            boardRect.localScale = Vector3.one * boardScaleFactor;
            // Keep original position
        }
        else
        {
            chessBoardObject.transform.localScale = new Vector3(boardScaleFactor, boardScaleFactor, 1f);
        }

        SpawnAllPieces();

        // Update reveal button to show board is permanently visible
        UpdateRevealButtonForGameEnd();
    }


    void SpawnAllPieces()
    {
        ClearPieces();

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                ChessRules.ChessPiece piece = chessRules.GetPiece(row, col);
                if (piece != null && piece.type != ChessRules.PieceType.None)
                {
                    CreatePieceVisual(piece, row, col);
                }
            }
        }
    }

    void CreatePieceVisual(ChessRules.ChessPiece piece, int row, int col)
    {
        if (boardSquares[row, col] == null) return;

        GameObject pieceObj = new GameObject($"Piece_{row}_{col}");
        pieceObj.transform.SetParent(boardSquares[row, col].transform, false);

        Image pieceImage = pieceObj.AddComponent<Image>();
        pieceImage.sprite = GetPieceSprite(piece);
        pieceImage.raycastTarget = false;

        RectTransform rect = pieceObj.GetComponent<RectTransform>();
        rect.sizeDelta = pieceSize;
        rect.anchoredPosition = Vector2.zero;

        pieceObjects[row, col] = pieceObj;
    }

    Sprite GetPieceSprite(ChessRules.ChessPiece piece)
    {
        if (piece.color == ChessRules.PieceColor.White)
        {
            switch (piece.type)
            {
                case ChessRules.PieceType.Pawn: return whitePawnSprite;
                case ChessRules.PieceType.Knight: return whiteKnightSprite;
                case ChessRules.PieceType.Bishop: return whiteBishopSprite;
                case ChessRules.PieceType.Rook: return whiteRookSprite;
                case ChessRules.PieceType.Queen: return whiteQueenSprite;
                case ChessRules.PieceType.King: return whiteKingSprite;
            }
        }
        else
        {
            switch (piece.type)
            {
                case ChessRules.PieceType.Pawn: return blackPawnSprite;
                case ChessRules.PieceType.Knight: return blackKnightSprite;
                case ChessRules.PieceType.Bishop: return blackBishopSprite;
                case ChessRules.PieceType.Rook: return blackRookSprite;
                case ChessRules.PieceType.Queen: return blackQueenSprite;
                case ChessRules.PieceType.King: return blackKingSprite;
            }
        }
        return null;
    }

    void ClearPieces()
    {
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (pieceObjects[row, col] != null)
                {
                    GameObject.Destroy(pieceObjects[row, col]);
                    pieceObjects[row, col] = null;
                }
            }
        }
    }

    void UpdateRevealButtonText()
    {
        TMP_Text buttonText = revealBoardButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            if (currentRevealCount > 0 && !isRevealInProgress)
            {
                buttonText.text = $"Reveal Board ({currentRevealCount})";
                revealBoardButton.interactable = true;
            }
            else if (isRevealInProgress)
            {
                buttonText.text = "Revealing...";
                revealBoardButton.interactable = false;
            }
            else
            {
                buttonText.text = "No Reveals Left";
                revealBoardButton.interactable = false;
            }
        }
    }

    #endregion

    #region Draw Offer System - Simplified

    void HandleDrawButton()
    {
        if (!gameActive) return;

        if (drawOfferReceived)
        {
            // Accept the received draw offer
            AcceptDraw();
        }
        else
        {
            // Send our own draw offer
            OfferDraw();
        }
    }

    void OfferDraw()
    {
        if (drawOfferSent) return; // Already sent

        drawOfferSent = true;
        UpdateDrawButtonText();

        // Send draw offer through network
        if (networkSync != null)
        {
            networkSync.SendDrawOffer(localPlayer.playerName);
        }

        ShowMessage("Draw offer sent to opponent.");

    }

    void AcceptDraw()
    {
        if (!drawOfferReceived) return;

        // Send acceptance through network
        if (networkSync != null)
        {
            networkSync.SendDrawAccept(localPlayer.playerName);
        }

        // Reset draw states
        drawOfferReceived = false;
        drawOfferSent = false;
        UpdateDrawButtonText();
    }

    void CancelDrawOffers()
    {
        if (drawOfferSent || drawOfferReceived)
        {
            drawOfferSent = false;
            drawOfferReceived = false;
            UpdateDrawButtonText();

            if (drawOfferSent)
            {
                ShowMessage("Draw offer canceled due to move.");
            }
        }
    }

    void UpdateDrawButtonText()
    {
        TMP_Text buttonText = drawOfferButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            if (drawOfferReceived)
            {
                buttonText.text = "Accept Draw";
                drawOfferButton.interactable = true;
            }
            else if (drawOfferSent)
            {
                buttonText.text = "Draw Offered";
                drawOfferButton.interactable = false;
            }
            else
            {
                buttonText.text = "Offer Draw";
                drawOfferButton.interactable = true;
            }
        }
    }

    #endregion

    #region Game State Management

    void CheckGameState()
    {
        ChessRules.PieceColor currentColor = chessRules.GetCurrentPlayerColor();

        if (chessRules.IsCheckmate(currentColor))
        {
            string winner = currentColor == localPlayer.color ? remotePlayer.playerName : localPlayer.playerName;
            EndGame($"Checkmate! {winner} wins!");
        }
        else if (chessRules.IsStalemate(currentColor))
        {
            EndGame("Stalemate! The game is a draw.");
        }
        else if (chessRules.IsThreefoldRepetition())
        {
            EndGame("Draw by threefold repetition!");
        }
        else if (chessRules.IsInsufficientMaterial())
        {
            EndGame("Draw by insufficient material!");
        }
        else if (chessRules.IsInCheck(currentColor))
        {
            string playerInCheck = currentColor == localPlayer.color ? "You are" : "Opponent is";
            ShowMessage($"{playerInCheck} in check!");
        }
    }

    void Resign()
    {
        if (!gameActive) return;

        Debug.Log($"[RESIGN] {localPlayer.playerName} is resigning");

        // Send resignation through network first (this will handle the game ending)
        if (networkSync != null)
        {
            networkSync.SendResign(localPlayer.playerName);
        }

        // Note: Game ending is now handled in the network response to ensure both players see it
    }

    public void EndGame(string result)
    {
        gameActive = false;
        moveInputField.interactable = false;
        drawOfferSent = false;
        drawOfferReceived = false;
        UpdateDrawButtonText();

        // Clear turn indicator when game ends
        turnIndicatorText.text = "GAME OVER";
        turnIndicatorText.color = Color.yellow;
        localPlayerIndicator.color = inactivePlayerColor;
        remotePlayerIndicator.color = inactivePlayerColor;

        // Reveal board permanently when game ends
        RevealBoardPermanently();

        moveLogText.text += $"\n\n=== GAME OVER ===\n{result}\n";
        ShowMessage(result);

        // Notify lobby manager
        lobbyManager.EndGame(result);
    }


    public void OnPlayerColorsSwapped()
    {
        Debug.Log("[COLOR SWAP] Player colors have been swapped - updating board perspective");

        // Reconfigure the board layout for the new perspective
        ConfigureBoardLayout();

        // If the board is currently visible, refresh the pieces
        if (chessBoardObject.activeSelf)
        {
            SpawnAllPieces();
        }

        // Update UI to reflect new colors
        SetupUI();
    }
    void UpdateRevealButtonForGameEnd()
    {
        TMP_Text buttonText = revealBoardButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = "Board Revealed";
            revealBoardButton.interactable = false;
        }
    }

    #endregion

    #region Network Integration Methods

    public void OnPlayerResigned(string resigningPlayerName, bool isLocalPlayer)
    {
        // Game ends for both players - EndGame call is handled by ChessNetworkSync
    }

    public void OnDrawOfferReceived(string offererName)
    {
        drawOfferReceived = true;
        UpdateDrawButtonText();
        ShowMessage($"{offererName} offers a draw.");
    }

    public void OnDrawAccepted()
    {
        drawOfferSent = false;
        drawOfferReceived = false;
        UpdateDrawButtonText();

        // Actually end the game here
        EndGame("Game ended in a draw by agreement!");
    }

    public void OnDrawDeclined()
    {
        drawOfferSent = false;
        UpdateDrawButtonText();
        ShowMessage("Your draw offer was declined.");
    }

    #endregion

    #region Save Log Functionality

    public void OnSaveLogClicked()
    {
        string logText = moveLogText.text;
        string fileName = $"blindfold_game_log_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        try
        {
            // Add additional game information to the log
            string fullLog = GenerateFullGameLog(logText);
            File.WriteAllText(filePath, fullLog);

            ShowMessage($"Log saved successfully!");
            Debug.Log($"Move log saved to {filePath}");

            // Also copy to clipboard if possible
            GUIUtility.systemCopyBuffer = fullLog;
            Debug.Log("Game log also copied to clipboard");
        }
        catch (System.Exception e)
        {
            ShowError("Failed to save log: " + e.Message);
            Debug.LogError($"Failed to save log: {e}");
        }
    }

    string GenerateFullGameLog(string moveLog)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Add game header information
        sb.AppendLine("=== BLINDFOLD CHESS GAME LOG ===");
        sb.AppendLine($"Date: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"White Player: {(localPlayer.color == ChessRules.PieceColor.White ? localPlayer.playerName : remotePlayer.playerName)}");
        sb.AppendLine($"Black Player: {(localPlayer.color == ChessRules.PieceColor.Black ? localPlayer.playerName : remotePlayer.playerName)}");
        sb.AppendLine($"Game Duration: {timerText.text}");
        sb.AppendLine($"Total Moves: {moveHistory.Count}");
        sb.AppendLine("");

        // Add the actual move log
        sb.AppendLine(moveLog);

        // Add final position information
        sb.AppendLine("");
        sb.AppendLine("=== FINAL POSITION ===");
        sb.AppendLine(GenerateFinalPositionLog());

        return sb.ToString();
    }

    string GenerateFinalPositionLog()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Generate a simple text representation of the final board position
        for (int row = 0; row < 8; row++)
        {
            sb.Append($"{8 - row} ");
            for (int col = 0; col < 8; col++)
            {
                var piece = chessRules.GetPiece(row, col);
                if (piece != null && piece.type != ChessRules.PieceType.None)
                {
                    string pieceSymbol = GetPieceSymbol(piece);
                    sb.Append(pieceSymbol + " ");
                }
                else
                {
                    sb.Append(". ");
                }
            }
            sb.AppendLine();
        }
        sb.AppendLine("  a b c d e f g h");

        return sb.ToString();
    }

    string GetPieceSymbol(ChessRules.ChessPiece piece)
    {
        string symbol = "";
        switch (piece.type)
        {
            case ChessRules.PieceType.Pawn: symbol = "P"; break;
            case ChessRules.PieceType.Knight: symbol = "N"; break;
            case ChessRules.PieceType.Bishop: symbol = "B"; break;
            case ChessRules.PieceType.Rook: symbol = "R"; break;
            case ChessRules.PieceType.Queen: symbol = "Q"; break;
            case ChessRules.PieceType.King: symbol = "K"; break;
        }

        return piece.color == ChessRules.PieceColor.White ? symbol : symbol.ToLower();
    }

    #endregion

    #region UI Helpers

    void AddMoveToLog(string san, bool isLocal)
    {
        ChessRules.PieceColor color = isLocal ? localPlayer.color : remotePlayer.color;

        if (color == ChessRules.PieceColor.White)
        {
            moveLogText.text += $"{chessRules.MoveNumber}. {san} ";
        }
        else
        {
            moveLogText.text += $"{san}\n";
        }

        moveHistory.Add(san);
        ScrollMoveLogToBottom();
    }

    void ShowMessage(string message)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = message;
            errorMessageText.color = Color.white;
            errorMessageText.gameObject.SetActive(true);
            StartCoroutine(HideMessageAfterDelay());
        }
    }

    void ShowError(string error)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = error;
            errorMessageText.color = Color.red;
            errorMessageText.gameObject.SetActive(true);
            StartCoroutine(HideMessageAfterDelay());
        }
    }

    void ScrollMoveLogToBottom()
    {
        if (moveLogScrollRect == null) return;
        StartCoroutine(ScrollMoveLogNextFrame());
    }

    IEnumerator ScrollMoveLogNextFrame()
    {
        yield return null;                 // wait for layout to resize
        Canvas.ForceUpdateCanvases();
        moveLogScrollRect.verticalNormalizedPosition = 0f; // bottom
    }

    IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(errorMessageDuration);
        if (errorMessageText != null)
        {
            errorMessageText.gameObject.SetActive(false);
        }
    }

    void FocusInput()
    {
        StartCoroutine(DelayedFocus());
    }

    IEnumerator DelayedFocus()
    {
        yield return new WaitForEndOfFrame();
        if (moveInputField.interactable)
        {
            moveInputField.Select();
            moveInputField.ActivateInputField();
        }
    }

    // ======== NEW HELPERS: SAN generation & parsing ========

    static bool TryParseSquare(string sq, out int row, out int col)
    {
        row = col = -1;
        if (string.IsNullOrEmpty(sq) || sq.Length != 2) return false;
        char file = char.ToLower(sq[0]);
        char rank = sq[1];
        if (file < 'a' || file > 'h') return false;
        if (rank < '1' || rank > '8') return false;
        col = file - 'a';
        row = 8 - (rank - '0');
        return true;
    }

    static string SquareToString(int row, int col)
    {
        char file = (char)('a' + col);
        int rank = 8 - row;
        return $"{file}{rank}";
    }

    string GenerateSANCore(int fromRow, int fromCol, int toRow, int toCol, ChessRules.PieceColor mover)
    {
        var moving = chessRules.GetPiece(fromRow, fromCol);
        var targetBefore = chessRules.GetPiece(toRow, toCol);

        // Castling by coords?
        if (moving != null && moving.type == ChessRules.PieceType.King && Mathf.Abs(toCol - fromCol) == 2)
        {
            return toCol == 6 ? "O-O" : "O-O-O";
        }

        bool isCapture = false;
        // en passant detection: pawn moves diagonally to empty square
        if (moving != null && moving.type == ChessRules.PieceType.Pawn && targetBefore == null && fromCol != toCol)
        {
            isCapture = true;
        }
        else
        {
            isCapture = targetBefore != null;
        }

        string pieceLetter = GetPieceLetter(moving?.type ?? ChessRules.PieceType.None);
        string disamb = "";
        if (moving != null && moving.type != ChessRules.PieceType.Pawn)
        {
            disamb = GetDisambiguation(moving.type, mover, fromRow, fromCol, toRow, toCol);
        }

        string dest = SquareToString(toRow, toCol);

        if (moving != null && moving.type == ChessRules.PieceType.Pawn)
        {
            if (isCapture)
            {
                char fromFile = (char)('a' + fromCol);
                string san = $"{fromFile}x{dest}";
                if (toRow == 0 || toRow == 7) san += "=Q";
                return san;
            }
            else
            {
                string san = dest;
                if (toRow == 0 || toRow == 7) san += "=Q";
                return san;
            }
        }
        else
        {
            string san = pieceLetter + disamb + (isCapture ? "x" : "") + dest;
            return san;
        }
    }

    string GetPieceLetter(ChessRules.PieceType type)
    {
        switch (type)
        {
            case ChessRules.PieceType.Knight: return "N";
            case ChessRules.PieceType.Bishop: return "B";
            case ChessRules.PieceType.Rook: return "R";
            case ChessRules.PieceType.Queen: return "Q";
            case ChessRules.PieceType.King: return "K";
            default: return ""; // pawns
        }
    }
    void ConfigureBoardLayout()
    {
        GridLayoutGroup gridLayout = boardSquaresParent.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 8;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;

            if (localPlayer.color == ChessRules.PieceColor.Black)
            {
                // Black player: rank 1 at bottom, file h at left
                gridLayout.startCorner = GridLayoutGroup.Corner.LowerRight;
                Debug.Log("[BOARD SETUP] Black perspective - starting from LowerRight");
            }
            else
            {
                // White player: rank 8 at top, file a at left  
                gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
                Debug.Log("[BOARD SETUP] White perspective - starting from UpperLeft");
            }
        }
        else
        {
            Debug.LogError("[BOARD SETUP] GridLayoutGroup not found on boardSquaresParent!");
        }
    }

    string GetDisambiguation(ChessRules.PieceType type, ChessRules.PieceColor mover, int fromRow, int fromCol, int toRow, int toCol)
    {
        // Find other same-type pieces that can also move to (toRow,toCol)
        var candidates = new List<(int r, int c)>();
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                if (r == fromRow && c == fromCol) continue;
                var p = chessRules.GetPiece(r, c);
                if (p == null || p.type != type || p.color != mover) continue;
                if (chessRules.IsValidMove(r, c, toRow, toCol, mover))
                {
                    candidates.Add((r, c));
                }
            }
        }

        if (candidates.Count == 0) return "";

        bool anySameFile = candidates.Any(x => x.c == fromCol);
        bool anySameRank = candidates.Any(x => x.r == fromRow);

        if (!anySameFile)
        {
            // file letter is sufficient
            return ((char)('a' + fromCol)).ToString();
        }
        else if (!anySameRank)
        {
            // rank number sufficient
            return (8 - fromRow).ToString();
        }
        else
        {
            // need both
            return ((char)('a' + fromCol)).ToString() + (8 - fromRow).ToString();
        }
    }

    #endregion
}