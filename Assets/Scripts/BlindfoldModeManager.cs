using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BlindfoldModeManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField moveInputField;
    public TMP_Text moveLogText;
    public TMP_Text errorMessageText; // NEW: Separate error message panel
    public GameObject chessBoardObject;
    public Button revealBoardButton;

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
    public float revealDuration = 3f;
    public Vector2 pieceSize = new Vector2(60f, 60f);
    public float errorMessageDuration = 3f; // How long error messages stay visible

    public enum PieceType { None, Pawn, Knight, Bishop, Rook, Queen, King }
    public enum PieceColor { White, Black }

    public class ChessPiece
    {
        public PieceType type;
        public PieceColor color;
        public bool hasMoved;

        public ChessPiece(PieceType type, PieceColor color)
        {
            this.type = type;
            this.color = color;
            this.hasMoved = false;
        }

        public ChessPiece Clone()
        {
            return new ChessPiece(type, color) { hasMoved = this.hasMoved };
        }
    }

    private ChessPiece[,] boardState = new ChessPiece[8, 8];
    private GameObject[,] pieceObjects = new GameObject[8, 8];
    private int currentRevealCount;
    private bool isWhiteTurn = true;
    private List<string> moveHistory = new List<string>();
    private int moveNumber = 1;

    // Chess rule tracking
    private bool whiteKingMoved = false;
    private bool blackKingMoved = false;
    private bool whiteKingsideRookMoved = false;
    private bool whiteQueensideRookMoved = false;
    private bool blackKingsideRookMoved = false;
    private bool blackQueensideRookMoved = false;
    private Vector2Int enPassantTarget = new Vector2Int(-1, -1); // En passant target square
    private Vector2Int lastPawnDoubleMove = new Vector2Int(-1, -1); // Last pawn that moved two squares

    void Start()
    {
        currentRevealCount = maxRevealCount;
        SetupStartingPosition();

        chessBoardObject.SetActive(false);

        revealBoardButton.onClick.AddListener(OnRevealBoardClicked);
        moveLogText.text = "Blindfold mode started! Type your moves (e.g., e4, Nf3)\n";
        moveInputField.text = "";

        // Initialize error message text
        if (errorMessageText != null)
        {
            errorMessageText.text = "";
            errorMessageText.gameObject.SetActive(false);
        }

        moveInputField.onSubmit.RemoveAllListeners();
        moveInputField.onSubmit.AddListener(OnPlayerMoveSubmitted);
        moveInputField.onEndEdit.RemoveAllListeners();
        moveInputField.onEndEdit.AddListener(OnPlayerMoveEndEdit);

        UpdateRevealButtonText();
        StartCoroutine(FocusInputField());
    }

    void ShowErrorMessage(string message)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = message;
            errorMessageText.gameObject.SetActive(true);
            StartCoroutine(HideErrorMessageAfterDelay());
        }
        Debug.LogWarning($"Chess Error: {message}");
    }

    IEnumerator HideErrorMessageAfterDelay()
    {
        yield return new WaitForSeconds(errorMessageDuration);
        if (errorMessageText != null)
        {
            errorMessageText.gameObject.SetActive(false);
        }
    }

    IEnumerator FocusInputField()
    {
        yield return new WaitForEndOfFrame();
        moveInputField.Select();
        moveInputField.ActivateInputField();
    }

    void SetupStartingPosition()
    {
        // Clear board state
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                boardState[row, col] = null;
                pieceObjects[row, col] = null;
            }
        }

        // White pawns on rank 2 (row 6)
        for (int col = 0; col < 8; col++)
        {
            boardState[6, col] = new ChessPiece(PieceType.Pawn, PieceColor.White);
        }

        // Black pawns on rank 7 (row 1)  
        for (int col = 0; col < 8; col++)
        {
            boardState[1, col] = new ChessPiece(PieceType.Pawn, PieceColor.Black);
        }

        // White back rank (rank 1, row 7)
        boardState[7, 0] = new ChessPiece(PieceType.Rook, PieceColor.White);
        boardState[7, 1] = new ChessPiece(PieceType.Knight, PieceColor.White);
        boardState[7, 2] = new ChessPiece(PieceType.Bishop, PieceColor.White);
        boardState[7, 3] = new ChessPiece(PieceType.Queen, PieceColor.White);
        boardState[7, 4] = new ChessPiece(PieceType.King, PieceColor.White);
        boardState[7, 5] = new ChessPiece(PieceType.Bishop, PieceColor.White);
        boardState[7, 6] = new ChessPiece(PieceType.Knight, PieceColor.White);
        boardState[7, 7] = new ChessPiece(PieceType.Rook, PieceColor.White);

        // Black back rank (rank 8, row 0)
        boardState[0, 0] = new ChessPiece(PieceType.Rook, PieceColor.Black);
        boardState[0, 1] = new ChessPiece(PieceType.Knight, PieceColor.Black);
        boardState[0, 2] = new ChessPiece(PieceType.Bishop, PieceColor.Black);
        boardState[0, 3] = new ChessPiece(PieceType.Queen, PieceColor.Black);
        boardState[0, 4] = new ChessPiece(PieceType.King, PieceColor.Black);
        boardState[0, 5] = new ChessPiece(PieceType.Bishop, PieceColor.Black);
        boardState[0, 6] = new ChessPiece(PieceType.Knight, PieceColor.Black);
        boardState[0, 7] = new ChessPiece(PieceType.Rook, PieceColor.Black);

        // Reset castling and en passant flags
        whiteKingMoved = blackKingMoved = false;
        whiteKingsideRookMoved = whiteQueensideRookMoved = false;
        blackKingsideRookMoved = blackQueensideRookMoved = false;
        enPassantTarget = new Vector2Int(-1, -1);
        lastPawnDoubleMove = new Vector2Int(-1, -1);

        Debug.Log("Starting position setup complete!");
    }

    // Check if the current player's king is in check
    bool IsInCheck(PieceColor color)
    {
        Vector2Int kingPos = FindKing(color);
        if (kingPos.x == -1) return false; // No king found

        return IsSquareAttacked(kingPos.x, kingPos.y, color == PieceColor.White ? PieceColor.Black : PieceColor.White);
    }

    // Find the king of the specified color
    Vector2Int FindKing(PieceColor color)
    {
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                ChessPiece piece = boardState[row, col];
                if (piece != null && piece.type == PieceType.King && piece.color == color)
                {
                    return new Vector2Int(row, col);
                }
            }
        }
        return new Vector2Int(-1, -1);
    }

    // Check if a square is attacked by the specified color
    bool IsSquareAttacked(int row, int col, PieceColor attackingColor)
    {
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                ChessPiece piece = boardState[r, c];
                if (piece != null && piece.color == attackingColor)
                {
                    if (CanPieceAttackSquare(r, c, row, col, piece))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    // Check if a piece can attack a specific square (different from normal move for pawns)
    bool CanPieceAttackSquare(int fromRow, int fromCol, int toRow, int toCol, ChessPiece piece)
    {
        if (fromRow == toRow && fromCol == toCol) return false;

        int deltaRow = toRow - fromRow;
        int deltaCol = toCol - fromCol;

        switch (piece.type)
        {
            case PieceType.Pawn:
                // Pawns attack diagonally only
                int direction = piece.color == PieceColor.White ? -1 : 1;
                return deltaRow == direction && Mathf.Abs(deltaCol) == 1;

            case PieceType.Knight:
                return (Mathf.Abs(deltaRow) == 2 && Mathf.Abs(deltaCol) == 1) ||
                       (Mathf.Abs(deltaRow) == 1 && Mathf.Abs(deltaCol) == 2);

            case PieceType.Bishop:
                return Mathf.Abs(deltaRow) == Mathf.Abs(deltaCol) && IsPathClear(fromRow, fromCol, toRow, toCol);

            case PieceType.Rook:
                return (deltaRow == 0 || deltaCol == 0) && IsPathClear(fromRow, fromCol, toRow, toCol);

            case PieceType.Queen:
                return (Mathf.Abs(deltaRow) == Mathf.Abs(deltaCol) || deltaRow == 0 || deltaCol == 0) &&
                       IsPathClear(fromRow, fromCol, toRow, toCol);

            case PieceType.King:
                return Mathf.Abs(deltaRow) <= 1 && Mathf.Abs(deltaCol) <= 1;
        }

        return false;
    }

    // Check if moving a piece would leave the king in check
    bool WouldMoveLeaveKingInCheck(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        // Make a temporary move
        ChessPiece movingPiece = boardState[fromRow, fromCol];
        ChessPiece capturedPiece = boardState[toRow, toCol];

        boardState[toRow, toCol] = movingPiece;
        boardState[fromRow, fromCol] = null;

        bool kingInCheck = IsInCheck(color);

        // Restore the board
        boardState[fromRow, fromCol] = movingPiece;
        boardState[toRow, toCol] = capturedPiece;

        return kingInCheck;
    }

    // Check if castling is legal
    bool CanCastle(PieceColor color, bool kingside)
    {
        int row = color == PieceColor.White ? 7 : 0;

        // Check if king or rook has moved
        if (color == PieceColor.White)
        {
            if (whiteKingMoved) return false;
            if (kingside && whiteKingsideRookMoved) return false;
            if (!kingside && whiteQueensideRookMoved) return false;
        }
        else
        {
            if (blackKingMoved) return false;
            if (kingside && blackKingsideRookMoved) return false;
            if (!kingside && blackQueensideRookMoved) return false;
        }

        // Check if king is in check
        if (IsInCheck(color)) return false;

        // Check if squares between king and rook are empty and not attacked
        int kingCol = 4;
        int rookCol = kingside ? 7 : 0;
        int direction = kingside ? 1 : -1;

        for (int col = kingCol + direction; col != rookCol; col += direction)
        {
            if (boardState[row, col] != null) return false; // Square occupied
            if (IsSquareAttacked(row, col, color == PieceColor.White ? PieceColor.Black : PieceColor.White))
                return false; // Square attacked
        }

        // Check if final king position is attacked
        int finalKingCol = kingside ? 6 : 2;
        if (IsSquareAttacked(row, finalKingCol, color == PieceColor.White ? PieceColor.Black : PieceColor.White))
            return false;

        return true;
    }

    // Execute castling move
    void ExecuteCastling(PieceColor color, bool kingside)
    {
        int row = color == PieceColor.White ? 7 : 0;
        int kingFromCol = 4;
        int kingToCol = kingside ? 6 : 2;
        int rookFromCol = kingside ? 7 : 0;
        int rookToCol = kingside ? 5 : 3;

        // Move king
        ChessPiece king = boardState[row, kingFromCol];
        boardState[row, kingToCol] = king;
        boardState[row, kingFromCol] = null;
        king.hasMoved = true;

        // Move rook
        ChessPiece rook = boardState[row, rookFromCol];
        boardState[row, rookToCol] = rook;
        boardState[row, rookFromCol] = null;
        rook.hasMoved = true;

        // Update castling flags
        if (color == PieceColor.White)
        {
            whiteKingMoved = true;
        }
        else
        {
            blackKingMoved = true;
        }

        // Update visual pieces if board is visible
        if (chessBoardObject.activeInHierarchy)
        {
            // Move king visually
            if (pieceObjects[row, kingFromCol] != null)
            {
                Transform newKingSquare = FindSquareTransform(row, kingToCol);
                if (newKingSquare != null)
                {
                    pieceObjects[row, kingFromCol].transform.SetParent(newKingSquare, false);
                    pieceObjects[row, kingFromCol].GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                    pieceObjects[row, kingToCol] = pieceObjects[row, kingFromCol];
                    pieceObjects[row, kingFromCol] = null;
                }
            }

            // Move rook visually
            if (pieceObjects[row, rookFromCol] != null)
            {
                Transform newRookSquare = FindSquareTransform(row, rookToCol);
                if (newRookSquare != null)
                {
                    pieceObjects[row, rookFromCol].transform.SetParent(newRookSquare, false);
                    pieceObjects[row, rookFromCol].GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
                    pieceObjects[row, rookToCol] = pieceObjects[row, rookFromCol];
                    pieceObjects[row, rookFromCol] = null;
                }
            }
        }
    }

    // Check for checkmate or stalemate
    bool IsCheckmate(PieceColor color)
    {
        if (!IsInCheck(color)) return false;
        return !HasLegalMoves(color);
    }

    bool IsStalemate(PieceColor color)
    {
        if (IsInCheck(color)) return false;
        return !HasLegalMoves(color);
    }

    bool HasLegalMoves(PieceColor color)
    {
        for (int fromRow = 0; fromRow < 8; fromRow++)
        {
            for (int fromCol = 0; fromCol < 8; fromCol++)
            {
                ChessPiece piece = boardState[fromRow, fromCol];
                if (piece == null || piece.color != color) continue;

                for (int toRow = 0; toRow < 8; toRow++)
                {
                    for (int toCol = 0; toCol < 8; toCol++)
                    {
                        if (IsValidMove(fromRow, fromCol, toRow, toCol, color))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    void SpawnAllPieces()
    {
        Debug.Log("=== SPAWNING ALL PIECES AS UI IMAGES ===");

        ClearAllPieceObjects();

        int piecesCreated = 0;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                ChessPiece piece = boardState[row, col];
                if (piece == null || piece.type == PieceType.None) continue;

                Transform squareTransform = FindSquareTransform(row, col);
                if (squareTransform == null)
                {
                    Debug.LogWarning($"Could not find square at row {row}, col {col}");
                    continue;
                }

                GameObject pieceObj = CreatePieceImage(piece, squareTransform, row, col);
                if (pieceObj != null)
                {
                    pieceObjects[row, col] = pieceObj;
                    piecesCreated++;
                    Debug.Log($"Created {piece.color} {piece.type} at {squareTransform.name}");
                }
            }
        }

        Debug.Log($"=== CREATED {piecesCreated} PIECE IMAGES ===");
    }

    GameObject CreatePieceImage(ChessPiece piece, Transform parent, int row, int col)
    {
        Sprite pieceSprite = GetSpriteForPiece(piece);
        if (pieceSprite == null)
        {
            Debug.LogError($"No sprite assigned for {piece.color} {piece.type}!");
            return null;
        }

        GameObject pieceObj = new GameObject($"{piece.color}_{piece.type}_R{row}C{col}");
        pieceObj.transform.SetParent(parent, false);

        Image imageComp = pieceObj.AddComponent<Image>();
        imageComp.sprite = pieceSprite;
        imageComp.color = Color.white;
        imageComp.raycastTarget = false;

        RectTransform rectTransform = pieceObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta = pieceSize;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        pieceObj.SetActive(true);

        Debug.Log($"Created piece image: {piece.color} {piece.type} with sprite {pieceSprite.name}");
        return pieceObj;
    }

    Sprite GetSpriteForPiece(ChessPiece piece)
    {
        if (piece.color == PieceColor.White)
        {
            switch (piece.type)
            {
                case PieceType.Pawn: return whitePawnSprite;
                case PieceType.Knight: return whiteKnightSprite;
                case PieceType.Bishop: return whiteBishopSprite;
                case PieceType.Rook: return whiteRookSprite;
                case PieceType.Queen: return whiteQueenSprite;
                case PieceType.King: return whiteKingSprite;
                default: return null;
            }
        }
        else
        {
            switch (piece.type)
            {
                case PieceType.Pawn: return blackPawnSprite;
                case PieceType.Knight: return blackKnightSprite;
                case PieceType.Bishop: return blackBishopSprite;
                case PieceType.Rook: return blackRookSprite;
                case PieceType.Queen: return blackQueenSprite;
                case PieceType.King: return blackKingSprite;
                default: return null;
            }
        }
    }

    void ClearAllPieceObjects()
    {
        Debug.Log("Clearing all existing piece objects...");

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (pieceObjects[row, col] != null)
                {
                    Debug.Log($"Destroying piece at [{row},{col}]: {pieceObjects[row, col].name}");
                    DestroyImmediate(pieceObjects[row, col]);
                    pieceObjects[row, col] = null;
                }
            }
        }
    }

    Transform FindSquareTransform(int row, int col)
    {
        string[] possibleNames = {
            $"Square_{row}_{col}",
            $"Square_{row}.{col}",
            $"square_{row}_{col}",
            $"Square{row}{col}",
            $"Square({row},{col})",
            $"Tile_{row}_{col}",
            $"Cell_{row}_{col}",
        };

        foreach (string name in possibleNames)
        {
            Transform found = chessBoardObject.transform.Find(name);
            if (found != null)
            {
                return found;
            }
        }

        Transform chessPanel = chessBoardObject.transform.Find("ChessBoardPanel");
        if (chessPanel != null)
        {
            foreach (string name in possibleNames)
            {
                Transform found = chessPanel.Find(name);
                if (found != null)
                {
                    return found;
                }
            }
        }

        int index = row * 8 + col;
        if (index < chessBoardObject.transform.childCount)
        {
            return chessBoardObject.transform.GetChild(index);
        }

        if (chessPanel != null && index < chessPanel.childCount)
        {
            return chessPanel.GetChild(index);
        }

        return null;
    }

    void OnRevealBoardClicked()
    {
        if (currentRevealCount > 0)
        {
            currentRevealCount--;
            StartCoroutine(RevealBoardTemporarily());
            UpdateRevealButtonText();
        }
    }

    IEnumerator RevealBoardTemporarily()
    {
        chessBoardObject.SetActive(true);
        SpawnAllPieces();

        yield return new WaitForSeconds(revealDuration);

        chessBoardObject.SetActive(false);
    }

    void UpdateRevealButtonText()
    {
        if (revealBoardButton != null)
        {
            TMP_Text buttonText = revealBoardButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                if (currentRevealCount > 0)
                    buttonText.text = $"Reveal Board ({currentRevealCount} left)";
                else
                {
                    buttonText.text = "No Reveals Left";
                    revealBoardButton.interactable = false;
                }
            }
        }
    }

    // Enhanced move execution with proper chess rules
    void ExecuteMove(int fromRow, int fromCol, int toRow, int toCol)
    {
        ChessPiece piece = boardState[fromRow, fromCol];
        ChessPiece capturedPiece = boardState[toRow, toCol];

        // Handle en passant capture
        bool isEnPassant = false;
        if (piece.type == PieceType.Pawn && capturedPiece == null && fromCol != toCol)
        {
            // En passant capture
            isEnPassant = true;
            int capturedPawnRow = piece.color == PieceColor.White ? toRow + 1 : toRow - 1;
            capturedPiece = boardState[capturedPawnRow, toCol];
            boardState[capturedPawnRow, toCol] = null;

            // Remove captured pawn visually
            if (chessBoardObject.activeInHierarchy && pieceObjects[capturedPawnRow, toCol] != null)
            {
                DestroyImmediate(pieceObjects[capturedPawnRow, toCol]);
                pieceObjects[capturedPawnRow, toCol] = null;
            }
        }

        // Update piece position
        piece.hasMoved = true;
        boardState[toRow, toCol] = piece;
        boardState[fromRow, fromCol] = null;

        // Update castling flags based on piece movement
        if (piece.type == PieceType.King)
        {
            if (piece.color == PieceColor.White) whiteKingMoved = true;
            else blackKingMoved = true;
        }
        else if (piece.type == PieceType.Rook)
        {
            if (piece.color == PieceColor.White)
            {
                if (fromCol == 0) whiteQueensideRookMoved = true;
                if (fromCol == 7) whiteKingsideRookMoved = true;
            }
            else
            {
                if (fromCol == 0) blackQueensideRookMoved = true;
                if (fromCol == 7) blackKingsideRookMoved = true;
            }
        }

        // Handle pawn promotion
        if (piece.type == PieceType.Pawn && (toRow == 0 || toRow == 7))
        {
            // Auto-promote to queen for now
            boardState[toRow, toCol] = new ChessPiece(PieceType.Queen, piece.color) { hasMoved = true };
        }

        // Update en passant target
        enPassantTarget = new Vector2Int(-1, -1);
        if (piece.type == PieceType.Pawn && Mathf.Abs(toRow - fromRow) == 2)
        {
            enPassantTarget = new Vector2Int((fromRow + toRow) / 2, fromCol);
            lastPawnDoubleMove = new Vector2Int(toRow, toCol);
        }

        // Update visual pieces if board is visible
        if (chessBoardObject.activeInHierarchy)
        {
            // Remove captured piece at destination if exists
            if (pieceObjects[toRow, toCol] != null && !isEnPassant)
            {
                Debug.Log($"Capturing piece at [{toRow},{toCol}]");
                DestroyImmediate(pieceObjects[toRow, toCol]);
                pieceObjects[toRow, toCol] = null;
            }

            // Move the piece object
            if (pieceObjects[fromRow, fromCol] != null)
            {
                Transform newSquare = FindSquareTransform(toRow, toCol);
                if (newSquare != null)
                {
                    pieceObjects[fromRow, fromCol].transform.SetParent(newSquare, false);
                    pieceObjects[fromRow, fromCol].GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

                    pieceObjects[toRow, toCol] = pieceObjects[fromRow, fromCol];
                    pieceObjects[fromRow, fromCol] = null;

                    // Update piece name and sprite if promoted
                    ChessPiece currentPiece = boardState[toRow, toCol];
                    pieceObjects[toRow, toCol].name = $"{currentPiece.color}_{currentPiece.type}_R{toRow}C{toCol}";

                    if (currentPiece.type == PieceType.Queen && piece.type == PieceType.Pawn)
                    {
                        // Update sprite for promotion
                        Image img = pieceObjects[toRow, toCol].GetComponent<Image>();
                        img.sprite = GetSpriteForPiece(currentPiece);
                    }

                    Debug.Log($"Moved {currentPiece.color} {currentPiece.type} from [{fromRow},{fromCol}] to [{toRow},{toCol}]");
                }
            }
        }

        Debug.Log($"Board state updated: {piece.color} {piece.type} moved to [{toRow},{toCol}]");

        if (isEnPassant)
        {
            Debug.Log("En passant capture executed!");
        }
    }

    void OnPlayerMoveSubmitted(string playerMove)
    {
        Debug.Log($"Player submitted move: '{playerMove}'");
        ProcessPlayerMove(playerMove);
    }

    void OnPlayerMoveEndEdit(string playerMove)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            Debug.Log($"Player pressed Enter with move: '{playerMove}'");
            ProcessPlayerMove(playerMove);
        }
    }

    void ProcessPlayerMove(string playerMove)
    {
        if (string.IsNullOrWhiteSpace(playerMove))
        {
            Debug.Log("Empty move, refocusing input");
            RefocusInputField();
            return;
        }

        playerMove = playerMove.Trim();
        Debug.Log($"Processing move: '{playerMove}'");

        PieceColor currentColor = isWhiteTurn ? PieceColor.White : PieceColor.Black;

        // Check for castling first (case insensitive)
        string upperMove = playerMove.ToUpper();
        if (upperMove == "O-O" || upperMove == "0-0") // Kingside castling
        {
            if (CanCastle(currentColor, true))
            {
                ExecuteCastling(currentColor, true);
                string moveNotation = "O-O";
                AddMoveToLog(moveNotation);
                isWhiteTurn = !isWhiteTurn;
                CheckGameState();
            }
            else
            {
                ShowErrorMessage("Kingside castling is not legal!");
            }
            moveInputField.text = "";
            RefocusInputField();
            return;
        }

        if (upperMove == "O-O-O" || upperMove == "0-0-0") // Queenside castling
        {
            if (CanCastle(currentColor, false))
            {
                ExecuteCastling(currentColor, false);
                string moveNotation = "O-O-O";
                AddMoveToLog(moveNotation);
                isWhiteTurn = !isWhiteTurn;
                CheckGameState();
            }
            else
            {
                ShowErrorMessage("Queenside castling is not legal!");
            }
            moveInputField.text = "";
            RefocusInputField();
            return;
        }

        // Parse regular moves
        if (TryParseChessNotation(playerMove, currentColor, out int fromRow, out int fromCol, out int toRow, out int toCol))
        {
            Debug.Log($"Successfully parsed move: from [{fromRow},{fromCol}] to [{toRow},{toCol}]");

            // Validate the move with all chess rules
            if (IsValidMove(fromRow, fromCol, toRow, toCol, currentColor))
            {
                Debug.Log("Move is valid, executing...");

                // Execute the move
                ExecuteMove(fromRow, fromCol, toRow, toCol);

                // Add to move history
                string moveNotation = FormatMoveNotation(playerMove, fromRow, fromCol, toRow, toCol);
                AddMoveToLog(moveNotation);

                // Switch turns
                isWhiteTurn = !isWhiteTurn;

                // Check game state (check, checkmate, stalemate)
                CheckGameState();

                Debug.Log($"Move executed successfully: {moveNotation}");
            }
            else
            {
                ShowErrorMessage($"Invalid move: {playerMove}");
                Debug.Log($"Move validation failed for: {playerMove}");
            }
        }
        else
        {
            ShowErrorMessage($"Invalid move: {playerMove}");
            Debug.Log($"Failed to parse move: {playerMove}");
        }

        // Clear the input and refocus
        moveInputField.text = "";
        RefocusInputField();
    }

    void AddMoveToLog(string moveNotation)
    {
        if (isWhiteTurn)
        {
            moveLogText.text += $"{moveNumber}. {moveNotation} ";
        }
        else
        {
            moveLogText.text += $"{moveNotation}\n";
            moveNumber++;
        }
    }

    void CheckGameState()
    {
        PieceColor currentColor = isWhiteTurn ? PieceColor.White : PieceColor.Black;
        PieceColor opponentColor = isWhiteTurn ? PieceColor.Black : PieceColor.White;

        if (IsCheckmate(currentColor))
        {
            string winner = opponentColor == PieceColor.White ? "White" : "Black";
            moveLogText.text += $"\nCheckmate! {winner} wins!\n";
            ShowErrorMessage($"Checkmate! {winner} wins!");
            // Disable further input
            moveInputField.interactable = false;
        }
        else if (IsStalemate(currentColor))
        {
            moveLogText.text += "\nStalemate! The game is a draw.\n";
            ShowErrorMessage("Stalemate! The game is a draw.");
            // Disable further input
            moveInputField.interactable = false;
        }
        else if (IsInCheck(currentColor))
        {
            string playerInCheck = currentColor == PieceColor.White ? "White" : "Black";
            moveLogText.text += $"{playerInCheck} is in check! ";
            ShowErrorMessage($"{playerInCheck} is in check!");
        }
    }

    void RefocusInputField()
    {
        StartCoroutine(DelayedFocus());
    }

    IEnumerator DelayedFocus()
    {
        yield return new WaitForEndOfFrame();
        moveInputField.Select();
        moveInputField.ActivateInputField();
    }

    public void ResetGame()
    {
        currentRevealCount = maxRevealCount;
        isWhiteTurn = true;
        moveHistory.Clear();
        moveNumber = 1;
        moveLogText.text = "Blindfold mode started! Type your moves (e.g., e4, Nf3)\n";
        moveInputField.text = "";
        moveInputField.interactable = true;

        if (errorMessageText != null)
        {
            errorMessageText.gameObject.SetActive(false);
        }

        ClearAllPieceObjects();
        chessBoardObject.SetActive(false);
        SetupStartingPosition();
        UpdateRevealButtonText();

        if (revealBoardButton != null)
            revealBoardButton.interactable = true;
    }

    // ENHANCED Chess notation parsing with complete rule validation
    bool TryParseChessNotation(string notation, PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol)
    {
        fromRow = fromCol = toRow = toCol = -1;

        if (string.IsNullOrEmpty(notation)) return false;

        // Make notation lowercase for consistent parsing
        notation = notation.ToLower().Trim().Replace("+", "").Replace("#", "");
        bool hasCapture = notation.Contains("x");
        notation = notation.Replace("x", "");

        // Check for full coordinate notation first (e.g., e2e4, a1h8, Nb1c3)
        if (TryParseFullCoordinateNotation(notation, color, out fromRow, out fromCol, out toRow, out toCol))
        {
            return true;
        }

        // Parse destination square (always at the end for standard notation)
        if (notation.Length < 2) return false;

        char fileChar = notation[notation.Length - 2];
        char rankChar = notation[notation.Length - 1];

        if (fileChar < 'a' || fileChar > 'h' || rankChar < '1' || rankChar > '8') return false;

        toCol = fileChar - 'a';
        toRow = 8 - (rankChar - '0'); // Convert chess rank to array index

        // Determine piece type (now case-insensitive)
        PieceType pieceType = PieceType.Pawn;
        int startIndex = 0;
        string disambiguation = "";

        if (notation.Length > 2)
        {
            char firstChar = notation[0]; // Already lowercase
            // Check if first character is a piece letter (not the destination file)
            if (char.IsLetter(firstChar) && "nbrqk".Contains(firstChar))
            {
                switch (firstChar)
                {
                    case 'n': pieceType = PieceType.Knight; startIndex = 1; break;
                    case 'b': pieceType = PieceType.Bishop; startIndex = 1; break;
                    case 'r': pieceType = PieceType.Rook; startIndex = 1; break;
                    case 'q': pieceType = PieceType.Queen; startIndex = 1; break;
                    case 'k': pieceType = PieceType.King; startIndex = 1; break;
                }
            }
        }

        // Extract disambiguation if present (everything between piece type and destination)
        if (notation.Length > startIndex + 2)
        {
            disambiguation = notation.Substring(startIndex, notation.Length - startIndex - 2);
        }

        // Find the piece that can make this move
        List<(int row, int col)> candidates = new List<(int, int)>();

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                ChessPiece piece = boardState[row, col];
                if (piece != null && piece.color == color && piece.type == pieceType)
                {
                    if (CanPieceMoveTo(row, col, toRow, toCol, piece))
                    {
                        candidates.Add((row, col));
                    }
                }
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        // If multiple candidates, use disambiguation
        if (candidates.Count > 1)
        {
            if (string.IsNullOrEmpty(disambiguation))
            {
                return false;
            }

            foreach (var candidate in candidates)
            {
                string candidateFile = ((char)('a' + candidate.col)).ToString();
                string candidateRank = (8 - candidate.row).ToString();
                string candidateSquare = candidateFile + candidateRank;

                if (disambiguation == candidateFile || disambiguation == candidateRank || disambiguation == candidateSquare)
                {
                    fromRow = candidate.row;
                    fromCol = candidate.col;
                    return true;
                }
            }
            return false;
        }

        // Single candidate found
        if (candidates.Count == 1)
        {
            fromRow = candidates[0].row;
            fromCol = candidates[0].col;
            return true;
        }

        return false;
    }

    // Parse full coordinate notation like e2e4, a1h8, Nb1c3
    bool TryParseFullCoordinateNotation(string notation, PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol)
    {
        fromRow = fromCol = toRow = toCol = -1;

        // Remove piece prefix if present (already lowercase)
        string workingNotation = notation;
        if (notation.Length > 4)
        {
            char firstChar = notation[0];
            if (firstChar == 'n' || firstChar == 'b' || firstChar == 'r' || firstChar == 'q' || firstChar == 'k')
            {
                workingNotation = notation.Substring(1);
            }
        }

        // Must be exactly 4 characters for coordinate notation (e.g., e2e4)
        if (workingNotation.Length != 4) return false;

        // Parse from square
        char fromFile = workingNotation[0];
        char fromRank = workingNotation[1];

        // Parse to square  
        char toFile = workingNotation[2];
        char toRank = workingNotation[3];

        // Validate coordinates
        if (fromFile < 'a' || fromFile > 'h' || fromRank < '1' || fromRank > '8' ||
            toFile < 'a' || toFile > 'h' || toRank < '1' || toRank > '8')
        {
            return false;
        }

        // Convert to array coordinates
        fromCol = fromFile - 'a';
        fromRow = 8 - (fromRank - '0');
        toCol = toFile - 'a';
        toRow = 8 - (toRank - '0');

        // Verify there's actually a piece of the right color at the from square
        ChessPiece piece = boardState[fromRow, fromCol];
        if (piece == null || piece.color != color)
        {
            return false;
        }

        return true;
    }

    // ENHANCED piece movement validation
    bool CanPieceMoveTo(int fromRow, int fromCol, int toRow, int toCol, ChessPiece piece)
    {
        if (fromRow == toRow && fromCol == toCol) return false;
        if (toRow < 0 || toRow >= 8 || toCol < 0 || toCol >= 8) return false;

        // Can't capture own pieces
        ChessPiece targetPiece = boardState[toRow, toCol];
        if (targetPiece != null && targetPiece.color == piece.color) return false;

        int deltaRow = toRow - fromRow;
        int deltaCol = toCol - fromCol;

        switch (piece.type)
        {
            case PieceType.Pawn:
                return IsValidPawnMove(fromRow, fromCol, toRow, toCol, piece.color);

            case PieceType.Knight:
                return (Mathf.Abs(deltaRow) == 2 && Mathf.Abs(deltaCol) == 1) ||
                       (Mathf.Abs(deltaRow) == 1 && Mathf.Abs(deltaCol) == 2);

            case PieceType.Bishop:
                return Mathf.Abs(deltaRow) == Mathf.Abs(deltaCol) && IsPathClear(fromRow, fromCol, toRow, toCol);

            case PieceType.Rook:
                // Rook moves horizontally or vertically only
                if (deltaRow == 0 && deltaCol != 0)
                {
                    // Horizontal move - check path is clear
                    return IsPathClear(fromRow, fromCol, toRow, toCol);
                }
                else if (deltaCol == 0 && deltaRow != 0)
                {
                    // Vertical move - check path is clear  
                    return IsPathClear(fromRow, fromCol, toRow, toCol);
                }
                return false;

            case PieceType.Queen:
                return (Mathf.Abs(deltaRow) == Mathf.Abs(deltaCol) || deltaRow == 0 || deltaCol == 0) &&
                       IsPathClear(fromRow, fromCol, toRow, toCol);

            case PieceType.King:
                return Mathf.Abs(deltaRow) <= 1 && Mathf.Abs(deltaCol) <= 1;
        }

        return false;
    }

    // ENHANCED pawn move validation with en passant
    bool IsValidPawnMove(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        int direction = color == PieceColor.White ? -1 : 1; // White moves up (-1), Black moves down (+1)
        int deltaRow = toRow - fromRow;
        int deltaCol = Mathf.Abs(toCol - fromCol);

        // Forward move
        if (deltaCol == 0)
        {
            if (boardState[toRow, toCol] != null) return false; // Can't move forward if blocked

            if (deltaRow == direction) return true; // Single step

            // Double step from starting position
            if (deltaRow == 2 * direction)
            {
                int startingRank = color == PieceColor.White ? 6 : 1;
                return fromRow == startingRank;
            }
        }
        // Diagonal capture
        else if (deltaCol == 1 && deltaRow == direction)
        {
            // Regular capture
            if (boardState[toRow, toCol] != null && boardState[toRow, toCol].color != color)
            {
                return true;
            }

            // En passant capture
            if (enPassantTarget.x == toRow && enPassantTarget.y == toCol)
            {
                return true;
            }
        }

        return false;
    }

    bool IsPathClear(int fromRow, int fromCol, int toRow, int toCol)
    {
        int rowStep = 0;
        int colStep = 0;

        // Calculate step direction
        if (toRow > fromRow) rowStep = 1;
        else if (toRow < fromRow) rowStep = -1;

        if (toCol > fromCol) colStep = 1;
        else if (toCol < fromCol) colStep = -1;

        // Start checking from the next square after the starting position
        int currentRow = fromRow + rowStep;
        int currentCol = fromCol + colStep;

        // Check each square until we reach the destination (but don't check destination)
        while (currentRow != toRow || currentCol != toCol)
        {
            if (currentRow < 0 || currentRow >= 8 || currentCol < 0 || currentCol >= 8)
            {
                return false;
            }

            // If there's a piece blocking the path
            if (boardState[currentRow, currentCol] != null)
            {
                return false;
            }

            // Move to next square
            currentRow += rowStep;
            currentCol += colStep;
        }

        return true;
    }

    // COMPLETE move validation with all chess rules
    bool IsValidMove(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        if (fromRow < 0 || fromRow >= 8 || fromCol < 0 || fromCol >= 8) return false;
        if (toRow < 0 || toRow >= 8 || toCol < 0 || toCol >= 8) return false;

        ChessPiece piece = boardState[fromRow, fromCol];
        if (piece == null || piece.color != color) return false;

        // Check if the basic move is valid for this piece type
        if (!CanPieceMoveTo(fromRow, fromCol, toRow, toCol, piece)) return false;

        // Check if this move would leave the king in check
        if (WouldMoveLeaveKingInCheck(fromRow, fromCol, toRow, toCol, color)) return false;

        return true;
    }

    string FormatMoveNotation(string originalNotation, int fromRow, int fromCol, int toRow, int toCol)
    {
        // For now, just return the original notation
        // Later you can implement proper algebraic notation formatting
        return originalNotation;
    }

    // Debug methods
    [ContextMenu("Force Spawn Pieces")]
    public void ForceSpawnPieces()
    {
        chessBoardObject.SetActive(true);
        SpawnAllPieces();
    }

    [ContextMenu("Check Sprite Assignments")]
    public void CheckSpriteAssignments()
    {
        Debug.Log("=== CHECKING SPRITE ASSIGNMENTS ===");
        Debug.Log($"White Pawn: {(whitePawnSprite != null ? whitePawnSprite.name : "NULL")}");
        Debug.Log($"White Knight: {(whiteKnightSprite != null ? whiteKnightSprite.name : "NULL")}");
        Debug.Log($"White Bishop: {(whiteBishopSprite != null ? whiteBishopSprite.name : "NULL")}");
        Debug.Log($"White Rook: {(whiteRookSprite != null ? whiteRookSprite.name : "NULL")}");
        Debug.Log($"White Queen: {(whiteQueenSprite != null ? whiteQueenSprite.name : "NULL")}");
        Debug.Log($"White King: {(whiteKingSprite != null ? whiteKingSprite.name : "NULL")}");

        Debug.Log($"Black Pawn: {(blackPawnSprite != null ? blackPawnSprite.name : "NULL")}");
        Debug.Log($"Black Knight: {(blackKnightSprite != null ? blackKnightSprite.name : "NULL")}");
        Debug.Log($"Black Bishop: {(blackBishopSprite != null ? blackBishopSprite.name : "NULL")}");
        Debug.Log($"Black Rook: {(blackRookSprite != null ? blackRookSprite.name : "NULL")}");
        Debug.Log($"Black Queen: {(blackQueenSprite != null ? blackQueenSprite.name : "NULL")}");
        Debug.Log($"Black King: {(blackKingSprite != null ? blackKingSprite.name : "NULL")}");
    }

    [ContextMenu("List All Squares")]
    public void ListAllSquares()
    {
        Debug.Log("=== LISTING ALL CHILD OBJECTS ===");
        for (int i = 0; i < chessBoardObject.transform.childCount; i++)
        {
            Transform child = chessBoardObject.transform.GetChild(i);
            Debug.Log($"Child {i}: {child.name}");

            for (int j = 0; j < child.childCount; j++)
            {
                Transform grandchild = child.GetChild(j);
                Debug.Log($"  Grandchild {j}: {grandchild.name}");
            }
        }
    }

    [ContextMenu("Test Check Detection")]
    public void TestCheckDetection()
    {
        bool whiteInCheck = IsInCheck(PieceColor.White);
        bool blackInCheck = IsInCheck(PieceColor.Black);

        Debug.Log($"White in check: {whiteInCheck}");
        Debug.Log($"Black in check: {blackInCheck}");

        if (whiteInCheck)
        {
            Vector2Int whiteKing = FindKing(PieceColor.White);
            Debug.Log($"White king at: [{whiteKing.x}, {whiteKing.y}]");
        }

        if (blackInCheck)
        {
            Vector2Int blackKing = FindKing(PieceColor.Black);
            Debug.Log($"Black king at: [{blackKing.x}, {blackKing.y}]");
        }
    }

    [ContextMenu("Test Castling Rights")]
    public void TestCastlingRights()
    {
        Debug.Log("=== CASTLING RIGHTS ===");
        Debug.Log($"White king moved: {whiteKingMoved}");
        Debug.Log($"White kingside rook moved: {whiteKingsideRookMoved}");
        Debug.Log($"White queenside rook moved: {whiteQueensideRookMoved}");
        Debug.Log($"Black king moved: {blackKingMoved}");
        Debug.Log($"Black kingside rook moved: {blackKingsideRookMoved}");
        Debug.Log($"Black queenside rook moved: {blackQueensideRookMoved}");

        Debug.Log($"White can castle kingside: {CanCastle(PieceColor.White, true)}");
        Debug.Log($"White can castle queenside: {CanCastle(PieceColor.White, false)}");
        Debug.Log($"Black can castle kingside: {CanCastle(PieceColor.Black, true)}");
        Debug.Log($"Black can castle queenside: {CanCastle(PieceColor.Black, false)}");
    }
}