using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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
    public GameObject drawPromptPanel;
    public Button acceptDrawButton;
    public Button declineDrawButton;

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
    public Vector2 pieceSize = new Vector2(60f, 60f);

    // Game components
    private ChessRules chessRules;
    private MultiplayerLobbyManager lobbyManager;
    private PlayerData localPlayer;
    private PlayerData remotePlayer;

    // Game state
    private GameObject[,] pieceObjects = new GameObject[8, 8];
    private Image[,] boardSquares = new Image[8, 8];
    private int currentRevealCount;
    private List<string> moveHistory = new List<string>();
    private bool isMyTurn;
    private bool gameActive;
    private bool drawOffered;
    private float gameTimer = 0f;

    // Board colors
    private Color lightSquareColor = new Color(0.9f, 0.9f, 0.8f);
    private Color darkSquareColor = new Color(0.6f, 0.5f, 0.4f);

    public void InitializeGame(ChessRules rules, PlayerData local, PlayerData remote, MultiplayerLobbyManager lobby)
    {
        chessRules = rules;
        localPlayer = local;
        remotePlayer = remote;
        lobbyManager = lobby;

        SetupUI();
        SetupBoard();
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
        drawOfferButton.onClick.AddListener(OfferDraw);

        acceptDrawButton.onClick.RemoveAllListeners();
        acceptDrawButton.onClick.AddListener(AcceptDraw);

        declineDrawButton.onClick.RemoveAllListeners();
        declineDrawButton.onClick.AddListener(DeclineDraw);

        // Hide board initially
        chessBoardObject.SetActive(false);
        drawPromptPanel.SetActive(false);

        // Initialize reveal count
        currentRevealCount = maxRevealCount;
        UpdateRevealButtonText();

        // Clear move log
        moveLogText.text = "=== Blindfold Chess Match ===\n";
        moveLogText.text += $"{localPlayer.playerName} vs {remotePlayer.playerName}\n\n";
    }

    void SetupBoard()
    {
        if (boardSquaresParent == null || squarePrefab == null) return;

        // Clear existing squares
        foreach (Transform child in boardSquaresParent)
        {
            Destroy(child.gameObject);
        }

        // Create board squares
        bool isWhitePerspective = localPlayer.color == ChessRules.PieceColor.White;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                GameObject square = Instantiate(squarePrefab, boardSquaresParent);
                square.name = $"Square_{row}_{col}";

                Image squareImage = square.GetComponent<Image>();
                if (squareImage == null)
                    squareImage = square.AddComponent<Image>();

                // Set chess board pattern
                bool isLight = (row + col) % 2 == 0;
                squareImage.color = isLight ? lightSquareColor : darkSquareColor;

                boardSquares[row, col] = squareImage;

                // Add coordinate labels if needed
                if (col == 0) // Left edge - rank numbers
                {
                    CreateRankLabel(square.transform, 8 - row);
                }
                if (row == 7) // Bottom edge - file letters
                {
                    CreateFileLabel(square.transform, (char)('a' + col));
                }
            }
        }
    }

    void CreateRankLabel(Transform parent, int rank)
    {
        GameObject labelObj = new GameObject($"Rank{rank}");
        labelObj.transform.SetParent(parent, false);

        TMP_Text label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = rank.ToString();
        label.fontSize = 12;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.TopLeft;

        RectTransform rect = labelObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(2, -2);
    }

    void CreateFileLabel(Transform parent, char file)
    {
        GameObject labelObj = new GameObject($"File{file}");
        labelObj.transform.SetParent(parent, false);

        TMP_Text label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = file.ToString();
        label.fontSize = 12;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.BottomRight;

        RectTransform rect = labelObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 0);
        rect.anchorMax = new Vector2(1, 0);
        rect.pivot = new Vector2(1, 0);
        rect.anchoredPosition = new Vector2(-2, 2);
    }

    void StartGame()
    {
        // Reset chess rules
        chessRules.ResetGame();

        // Set initial turn
        isMyTurn = localPlayer.color == ChessRules.PieceColor.White;
        gameActive = true;
        drawOffered = false;

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
        if (!gameActive || !isMyTurn || string.IsNullOrWhiteSpace(moveText))
        {
            moveInputField.text = "";
            FocusInput();
            return;
        }

        ProcessLocalMove(moveText.Trim());
    }

    void ProcessLocalMove(string moveNotation)
    {
        ChessRules.PieceColor currentColor = chessRules.GetCurrentPlayerColor();

        // Check if it's actually our turn in the chess engine
        if (currentColor != localPlayer.color)
        {
            ShowError("Not your turn!");
            moveInputField.text = "";
            FocusInput();
            return;
        }

        // Handle castling
        string upperMove = moveNotation.ToUpper();
        if (upperMove == "O-O" || upperMove == "0-0")
        {
            if (chessRules.CanCastle(currentColor, true))
            {
                ExecuteLocalMove("O-O", true);
            }
            else
            {
                ShowError("Kingside castling is not legal!");
                moveInputField.text = "";
                FocusInput();
            }
            return;
        }

        if (upperMove == "O-O-O" || upperMove == "0-0-0")
        {
            if (chessRules.CanCastle(currentColor, false))
            {
                ExecuteLocalMove("O-O-O", false);
            }
            else
            {
                ShowError("Queenside castling is not legal!");
                moveInputField.text = "";
                FocusInput();
            }
            return;
        }

        // Parse regular move
        if (TryParseAndValidateMove(moveNotation, currentColor, out string fromSquare, out string toSquare))
        {
            ExecuteLocalMove(fromSquare + toSquare, false);
        }
        else
        {
            ShowError($"Invalid move: {moveNotation}");
            moveInputField.text = "";
            FocusInput();
        }
    }

    bool TryParseAndValidateMove(string notation, ChessRules.PieceColor color,
        out string fromSquare, out string toSquare)
    {
        fromSquare = toSquare = "";

        // This is a simplified parser - you can expand it based on your needs
        // For now, expecting moves like "e2e4" or "e4"

        string cleanNotation = notation.ToLower().Replace("x", "").Replace("+", "").Replace("#", "");

        if (cleanNotation.Length == 4) // Full coordinate notation like "e2e4"
        {
            fromSquare = cleanNotation.Substring(0, 2);
            toSquare = cleanNotation.Substring(2, 2);

            int fromCol = fromSquare[0] - 'a';
            int fromRow = 8 - (fromSquare[1] - '0');
            int toCol = toSquare[0] - 'a';
            int toRow = 8 - (toSquare[1] - '0');

            return chessRules.IsValidMove(fromRow, fromCol, toRow, toCol, color);
        }

        // Add more parsing logic as needed for algebraic notation

        return false;
    }

    void ExecuteLocalMove(string move, bool isCastling)
    {
        if (isCastling)
        {
            bool kingside = move == "O-O";
            chessRules.ExecuteCastling(localPlayer.color, kingside);
        }
        else
        {
            // Parse and execute regular move
            int fromCol = move[0] - 'a';
            int fromRow = 8 - (move[1] - '0');
            int toCol = move[2] - 'a';
            int toRow = 8 - (move[3] - '0');

            chessRules.ExecuteMove(fromRow, fromCol, toRow, toCol);
        }

        // Add to move history
        AddMoveToLog(move, true);

        // Send move to opponent
        lobbyManager.SendMove(move);

        // Switch turns
        chessRules.NextTurn();
        isMyTurn = false;
        moveInputField.interactable = false;
        moveInputField.text = "";

        UpdateTurnIndicator();

        // Check game state
        CheckGameState();
    }

    public void OnRemoteMoveReceived(string move)
    {
        if (!gameActive || isMyTurn) return;

        // Execute opponent's move
        if (move == "O-O" || move == "O-O-O")
        {
            bool kingside = move == "O-O";
            chessRules.ExecuteCastling(remotePlayer.color, kingside);
        }
        else
        {
            int fromCol = move[0] - 'a';
            int fromRow = 8 - (move[1] - '0');
            int toCol = move[2] - 'a';
            int toRow = 8 - (move[3] - '0');

            chessRules.ExecuteMove(fromRow, fromCol, toRow, toCol);
        }

        // Add to move history
        AddMoveToLog(move, false);

        // Switch turns
        chessRules.NextTurn();
        isMyTurn = true;
        moveInputField.interactable = true;

        UpdateTurnIndicator();
        ShowMessage("Your turn!");
        FocusInput();

        // Check game state
        CheckGameState();
    }

    #endregion

    #region Board Reveal

    void RevealBoard()
    {
        if (currentRevealCount <= 0) return;

        currentRevealCount--;
        UpdateRevealButtonText();
        StartCoroutine(RevealBoardTemporarily());
    }

    IEnumerator RevealBoardTemporarily()
    {
        chessBoardObject.SetActive(true);
        SpawnAllPieces();

        yield return new WaitForSeconds(revealDuration);

        ClearPieces();
        chessBoardObject.SetActive(false);
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
                    Destroy(pieceObjects[row, col]);
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
            if (currentRevealCount > 0)
                buttonText.text = $"Reveal Board ({currentRevealCount})";
            else
            {
                buttonText.text = "No Reveals Left";
                revealBoardButton.interactable = false;
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

        EndGame($"{localPlayer.playerName} resigned. {remotePlayer.playerName} wins!");
        lobbyManager.SendGameMessage("I resign.");
    }

    void OfferDraw()
    {
        if (!gameActive || drawOffered) return;

        drawOffered = true;
        drawOfferButton.interactable = false;
        lobbyManager.SendGameMessage("Draw offer sent.");
        ShowMessage("Draw offer sent to opponent.");
    }

    void AcceptDraw()
    {
        drawPromptPanel.SetActive(false);
        EndGame("Game drawn by agreement.");
        lobbyManager.SendGameMessage("Draw accepted.");
    }

    void DeclineDraw()
    {
        drawPromptPanel.SetActive(false);
        lobbyManager.SendGameMessage("Draw declined.");
        ShowMessage("Draw offer declined.");
    }

    void EndGame(string result)
    {
        gameActive = false;
        moveInputField.interactable = false;

        moveLogText.text += $"\n\n=== GAME OVER ===\n{result}\n";
        ShowMessage(result);

        // Notify lobby manager
        lobbyManager.EndGame(result);
    }

    #endregion

    #region UI Helpers

    void AddMoveToLog(string move, bool isLocal)
    {
        string playerName = isLocal ? localPlayer.playerName : remotePlayer.playerName;
        ChessRules.PieceColor color = isLocal ? localPlayer.color : remotePlayer.color;

        if (color == ChessRules.PieceColor.White)
        {
            moveLogText.text += $"{chessRules.MoveNumber}. {move} ";
        }
        else
        {
            moveLogText.text += $"{move}\n";
        }

        moveHistory.Add($"{playerName}: {move}");
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

    #endregion
}