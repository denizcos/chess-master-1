using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

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
            GameObject.Destroy(child.gameObject);
        }

        // Create board squares
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                GameObject square = GameObject.Instantiate(squarePrefab, boardSquaresParent);
                square.name = $"Square_{row}_{col}";

                Image squareImage = square.GetComponent<Image>();
                if (squareImage == null)
                    squareImage = square.AddComponent<Image>();

                // Set chess board pattern
                bool isLight = (row + col) % 2 == 0;
                squareImage.color = isLight ? lightSquareColor : darkSquareColor;

                boardSquares[row, col] = squareImage;
            }
        }
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

        // Verify it's actually our turn according to the engine
        if (currentColor != localPlayer.color)
        {
            ShowError("Not your turn!");
            moveInputField.text = "";
            FocusInput();
            return;
        }

        // Castling keywords handled first
        string upperMove = moveNotation.ToUpper().Replace("0-0-0","O-O-O").Replace("0-0","O-O");
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
            if (!TryParseSquare(lowered.Substring(0,2), out fromRow, out fromCol)) return false;
            if (!TryParseSquare(lowered.Substring(2,2), out toRow, out toCol)) return false;
            if (!chessRules.IsValidMove(fromRow, fromCol, toRow, toCol, color)) return false;
            return true;
        }

        // Normalize: remove spaces and check/mate marks; keep 'x' and '=' for our own use
        string san = clean.Replace(" ", "").Replace("+", "").Replace("#", "");
        san = san.Replace("0-0-0","O-O-O").Replace("0-0","O-O");

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
                    case 'R': requiredType = ChessRules.PieceType.Rook;   break;
                    case 'Q': requiredType = ChessRules.PieceType.Queen;  break;
                    case 'K': requiredType = ChessRules.PieceType.King;   break;
                }
            }

            // Iterate all pieces of that color and (optional) type; choose first that makes a legal move to dest
            for (int r=0;r<8;r++)
            {
                for (int c=0;c<8;c++)
                {
                    var p = chessRules.GetPiece(r,c);
                    if (p == null || p.type == ChessRules.PieceType.None) continue;
                    if (p.color != color) continue;
                    if (pieceLetter != '\0' && p.type != requiredType) continue;
                    if (pieceLetter == '\0' && p.type != ChessRules.PieceType.Pawn && !(disambFileChar!= '\0' || disambRankChar!='\0'))
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
            for (int r=0;r<8;r++)
            {
                for (int c=0;c<8;c++)
                {
                    var p = chessRules.GetPiece(r,c);
                    if (p == null || p.type == ChessRules.PieceType.None) continue;
                    if (p.color != color) continue;
                    if (chessRules.IsValidMove(r,c,toRow,toCol,color))
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

        // Execute
        chessRules.ExecuteCastling(localPlayer.color, kingside);

        // Append check/mate suffix based on opponent after the move
        var opponent = (localPlayer.color == ChessRules.PieceColor.White) ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
        if (chessRules.IsCheckmate(opponent)) san += "#";
        else if (chessRules.IsInCheck(opponent)) san += "+";

        // Log and send
        AddMoveToLog(san, true);
        lobbyManager.SendMove(kingside ? "O-O" : "O-O-O");

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

        // Execute move on engine
        chessRules.ExecuteMove(fromRow, fromCol, toRow, toCol);

        // Append check/mate suffix based on opponent after the move
        var opponent = (localPlayer.color == ChessRules.PieceColor.White) ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
        string san = sanCore;
        if (chessRules.IsCheckmate(opponent)) san += "#";
        else if (chessRules.IsInCheck(opponent)) san += "+";

        // Log and send
        AddMoveToLog(san, true);
        string coord = SquareToString(fromRow,fromCol) + SquareToString(toRow,toCol);
        lobbyManager.SendMove(coord);

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
        if (!gameActive || isMyTurn) return;

        string upper = move.ToUpper().Replace("0-0-0","O-O-O").Replace("0-0","O-O");
        if (upper == "O-O" || upper == "O-O-O")
        {
            bool kingside = upper == "O-O";
            // SAN before execute
            string san = kingside ? "O-O" : "O-O-O";
            chessRules.ExecuteCastling(remotePlayer.color, kingside);

            // Suffix against opponent (local)
            var opponent = (remotePlayer.color == ChessRules.PieceColor.White) ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
            if (chessRules.IsCheckmate(opponent)) san += "#";
            else if (chessRules.IsInCheck(opponent)) san += "+";

            AddMoveToLog(san, false);

            chessRules.NextTurn();
            isMyTurn = true;
            moveInputField.interactable = true;
            UpdateTurnIndicator();
            ShowMessage("Your turn!");
            FocusInput();
            CheckGameState();
            return;
        }

        // Coordinate
        if (move.Length >= 4)
        {
            string fromSq = move.Substring(0,2).ToLower();
            string toSq = move.Substring(2,2).ToLower();
            if (TryParseSquare(fromSq, out int fr, out int fc) && TryParseSquare(toSq, out int tr, out int tc))
            {
                // SAN core from current board BEFORE move
                string sanCore = GenerateSANCore(fr, fc, tr, tc, remotePlayer.color);

                chessRules.ExecuteMove(fr, fc, tr, tc);

                // suffix against opponent (local)
                var opponent = (remotePlayer.color == ChessRules.PieceColor.White) ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
                string san = sanCore;
                if (chessRules.IsCheckmate(opponent)) san += "#";
                else if (chessRules.IsInCheck(opponent)) san += "+";

                AddMoveToLog(san, false);

                chessRules.NextTurn();
                isMyTurn = true;
                moveInputField.interactable = true;

                UpdateTurnIndicator();
                ShowMessage("Your turn!");
                FocusInput();
                CheckGameState();
                return;
            }
        }

        Debug.LogWarning("Received invalid move from opponent: " + move);
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
            case ChessRules.PieceType.Rook:   return "R";
            case ChessRules.PieceType.Queen:  return "Q";
            case ChessRules.PieceType.King:   return "K";
            default: return ""; // pawns
        }
    }

    string GetDisambiguation(ChessRules.PieceType type, ChessRules.PieceColor mover, int fromRow, int fromCol, int toRow, int toCol)
    {
        // Find other same-type pieces that can also move to (toRow,toCol)
        var candidates = new List<(int r,int c)>();
        for (int r=0;r<8;r++)
        {
            for (int c=0;c<8;c++)
            {
                if (r==fromRow && c==fromCol) continue;
                var p = chessRules.GetPiece(r,c);
                if (p == null || p.type != type || p.color != mover) continue;
                if (chessRules.IsValidMove(r,c,toRow,toCol,mover))
                {
                    candidates.Add((r,c));
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
