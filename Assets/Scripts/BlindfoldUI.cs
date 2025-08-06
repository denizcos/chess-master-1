using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class BlindfoldUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField moveInputField;
    public TMP_Text moveLogText;
    public TMP_Text errorMessageText;
    public GameObject chessBoardObject;
    public Button revealBoardButton;
    public TMP_Dropdown difficultyDropdown;
    public GameObject difficultyPromptObject;

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
    public float errorMessageDuration = 3f;

    // References - HIDDEN from inspector
    private ChessRules chessRules;
    private ChessAI chessAI;

    // Public methods to set references
    public void SetChessRules(ChessRules rules) { chessRules = rules; }
    public void SetChessAI(ChessAI ai)
    {
        chessAI = ai;

        // Connect AI events when AI is set
        if (chessAI != null)
        {
            chessAI.OnAIMoveReady += OnAIMoveReceived;
            chessAI.OnEngineStatus += ShowEngineStatus;
            chessAI.OnEngineError += ShowErrorMessage;
        }
    }

    // UI State
    private GameObject[,] pieceObjects = new GameObject[8, 8];
    private int currentRevealCount;
    private List<string> moveHistory = new List<string>();
    private bool isDifficultySet = false;

    void Start()
    {
        InitializeUI();
        SetupEventListeners();
    }

    void InitializeUI()
    {
        currentRevealCount = maxRevealCount;
        chessBoardObject.SetActive(false);
    //  moveInputField.text = "Choose difficulty. \n";
        moveInputField.interactable = false; // Disabled until difficulty is set

        if (errorMessageText != null)
    {
        // Prompt the player to choose a difficulty in the same UI area
        ShowErrorMessage("Choose difficulty.");
    }


        // Show difficulty prompt
        if (difficultyPromptObject != null)
        {
            difficultyPromptObject.SetActive(true);
        }

        UpdateRevealButtonText();
    }

    void SetupEventListeners()
    {
        // Input field events
        moveInputField.onSubmit.RemoveAllListeners();
        moveInputField.onSubmit.AddListener(OnPlayerMoveSubmitted);
        moveInputField.onEndEdit.RemoveAllListeners();
        moveInputField.onEndEdit.AddListener(OnPlayerMoveEndEdit);

        // Button events
        revealBoardButton.onClick.RemoveAllListeners();
        revealBoardButton.onClick.AddListener(OnRevealBoardClicked);

        // Dropdown events
        if (difficultyDropdown != null)
        {
            difficultyDropdown.onValueChanged.RemoveAllListeners();
            difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
        }

        // AI events
    }

    #region Input Handling

    void OnPlayerMoveSubmitted(string playerMove)
    {
        UnityEngine.Debug.Log($"Player submitted move: '{playerMove}'");
        ProcessPlayerMove(playerMove);
    }

    void OnPlayerMoveEndEdit(string playerMove)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            UnityEngine.Debug.Log($"Player pressed Enter with move: '{playerMove}'");
            ProcessPlayerMove(playerMove);
        }
    }

   void ProcessPlayerMove(string playerMove)
{
    if (string.IsNullOrWhiteSpace(playerMove))
    {
        RefocusInputField();
        return;
    }

    if (!isDifficultySet)
    {
        ShowErrorMessage("Please select a difficulty level first!");
        moveInputField.text = "";
        RefocusInputField();
        return;
    }

    if (!chessAI.IsReady())
    {
        ShowErrorMessage("Chess engine is still loading. Please wait...");
        moveInputField.text = "";
        RefocusInputField();
        return;
    }

    playerMove = playerMove.Trim();
    ChessRules.PieceColor currentColor = chessRules.GetCurrentPlayerColor();

    // --- Castling handling (unchanged) ---
    string upperMove = playerMove.ToUpper();
    if (upperMove == "O-O" || upperMove == "0-0")
    {
        if (chessRules.CanCastle(currentColor, true))
        {
            chessRules.ExecuteCastling(currentColor, true);
            AddMoveToLog("O-O");
            chessRules.NextTurn();
            CheckGameStateAndContinue();
        }
        else
        {
            ShowErrorMessage("Kingside castling is not legal!");
        }
        ClearInputAndRefocus();
        return;
    }

    if (upperMove == "O-O-O" || upperMove == "0-0-0")
    {
        if (chessRules.CanCastle(currentColor, false))
        {
            chessRules.ExecuteCastling(currentColor, false);
            AddMoveToLog("O-O-O");
            chessRules.NextTurn();
            CheckGameStateAndContinue();
        }
        else
        {
            ShowErrorMessage("Queenside castling is not legal!");
        }
        ClearInputAndRefocus();
        return;
    }

    // --- Regular moves ---
    if (TryParseMove(playerMove, currentColor, out int fromRow, out int fromCol, out int toRow, out int toCol))
    {
        if (chessRules.IsValidMove(fromRow, fromCol, toRow, toCol, currentColor))
        {
            // 1) Capture state before moving
            var piece = chessRules.GetPiece(fromRow, fromCol);
            bool wasCapture = chessRules.GetPiece(toRow, toCol) != null;

            // 2) Execute the move once
            chessRules.ExecuteMove(fromRow, fromCol, toRow, toCol);

            // 3) Generate standardized notation
            string moveNotation = GenerateAlgebraicNotation(
                piece.type,
                fromRow, fromCol,
                toRow,   toCol,
                wasCapture
            );

            AddMoveToLog(moveNotation);
            chessRules.NextTurn();
            CheckGameStateAndContinue();
        }
        else
        {
            ShowErrorMessage($"Invalid move: {playerMove}");
        }
    }
    else
    {
        ShowErrorMessage($"Invalid move notation: {playerMove}");
    }

    ClearInputAndRefocus();
}


    void CheckGameStateAndContinue()
    {
        ChessRules.PieceColor currentColor = chessRules.GetCurrentPlayerColor();

        if (chessRules.IsCheckmate(currentColor))
        {
            string winner = currentColor == ChessRules.PieceColor.White ? "Black" : "White";
            moveLogText.text += $"\nCheckmate! {winner} wins!\n";
            ShowErrorMessage($"Checkmate! {winner} wins!");
            moveInputField.interactable = false;
            return;
        }

        if (chessRules.IsStalemate(currentColor))
        {
            moveLogText.text += "\nStalemate! The game is a draw.\n";
            ShowErrorMessage("Stalemate! The game is a draw.");
            moveInputField.interactable = false;
            return;
        }

        if (chessRules.IsThreefoldRepetition())
        {
            moveLogText.text += "\nDraw by threefold repetition!\n";
            ShowErrorMessage("Draw by threefold repetition!");
            moveInputField.interactable = false;
            return;
        }

        if (chessRules.IsInsufficientMaterial())
        {
            moveLogText.text += "\nDraw by insufficient material!\n";
            ShowErrorMessage("Draw by insufficient material!");
            moveInputField.interactable = false;
            return;
        }

        if (chessRules.IsInCheck(currentColor))
        {
            string playerInCheck = currentColor == ChessRules.PieceColor.White ? "White" : "Black";
            ShowErrorMessage($"{playerInCheck} is in check!");
        }

        // Get AI move if it's AI's turn (assuming AI plays as Black)
        if (!chessRules.IsWhiteTurn && chessAI.IsReady())
        {
            chessAI.GetAIMove();
        }
    }

    #endregion

    #region Move Parsing

    bool TryParseMove(string notation, ChessRules.PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol)
    {
        fromRow = fromCol = toRow = toCol = -1;

        if (string.IsNullOrEmpty(notation)) return false;

        string cleanNotation = notation.ToLower().Trim().Replace("+", "").Replace("#", "");

        // Remove promotion notation for now (auto-promote to Queen)
        cleanNotation = cleanNotation.Replace("=q", "").Replace("=r", "").Replace("=b", "").Replace("=n", "");

        bool hasCapture = cleanNotation.Contains("x");
        cleanNotation = cleanNotation.Replace("x", "");

        // Try full coordinate notation first (e2e4, Nb1c3)
        if (TryParseFullCoordinate(cleanNotation, color, out fromRow, out fromCol, out toRow, out toCol))
        {
            return true;
        }

        // Parse algebraic notation (e4, Nf3, Bb5)
        return TryParseAlgebraic(cleanNotation, color, out fromRow, out fromCol, out toRow, out toCol);
    }

    bool TryParseFullCoordinate(string notation, ChessRules.PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol)
    {
        fromRow = fromCol = toRow = toCol = -1;

        string workingNotation = notation;
        if (notation.Length > 4 && "nbrqk".Contains(notation[0]))
        {
            workingNotation = notation.Substring(1);
        }

        if (workingNotation.Length != 4) return false;

        char fromFile = workingNotation[0];
        char fromRank = workingNotation[1];
        char toFile = workingNotation[2];
        char toRank = workingNotation[3];

        if (fromFile < 'a' || fromFile > 'h' || fromRank < '1' || fromRank > '8' ||
            toFile < 'a' || toFile > 'h' || toRank < '1' || toRank > '8')
        {
            return false;
        }

        fromCol = fromFile - 'a';
        fromRow = 8 - (fromRank - '0');
        toCol = toFile - 'a';
        toRow = 8 - (toRank - '0');

        ChessRules.ChessPiece piece = chessRules.GetPiece(fromRow, fromCol);
        if (piece == null || piece.color != color)
        {
            return false;
        }

        return true;
    }

    bool TryParseAlgebraic(string notation, ChessRules.PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol)
    {
        fromRow = fromCol = toRow = toCol = -1;

        if (notation.Length < 2) return false;

        // Get destination square
        char fileChar = notation[notation.Length - 2];
        char rankChar = notation[notation.Length - 1];

        if (fileChar < 'a' || fileChar > 'h' || rankChar < '1' || rankChar > '8') return false;

        toCol = fileChar - 'a';
        toRow = 8 - (rankChar - '0');

        // Determine piece type
        ChessRules.PieceType pieceType = ChessRules.PieceType.Pawn;
        string disambiguation = "";

        if (notation.Length > 2)
        {
            char firstChar = notation[0];
            if ("nbrqk".Contains(firstChar))
            {
                switch (firstChar)
                {
                    case 'n': pieceType = ChessRules.PieceType.Knight; break;
                    case 'b': pieceType = ChessRules.PieceType.Bishop; break;
                    case 'r': pieceType = ChessRules.PieceType.Rook; break;
                    case 'q': pieceType = ChessRules.PieceType.Queen; break;
                    case 'k': pieceType = ChessRules.PieceType.King; break;
                }

                if (notation.Length > 3)
                {
                    disambiguation = notation.Substring(1, notation.Length - 3);
                }
            }
            else
            {
                disambiguation = notation.Substring(0, notation.Length - 2);
            }
        }

        // Find pieces that can make this move
        List<(int row, int col)> candidates = new List<(int, int)>();

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                ChessRules.ChessPiece piece = chessRules.GetPiece(row, col);
                if (piece != null && piece.color == color && piece.type == pieceType)
                {
                    if (chessRules.CanPieceMoveTo(row, col, toRow, toCol, piece))
                    {
                        candidates.Add((row, col));
                    }
                }
            }
        }

        if (candidates.Count == 0) return false;

        if (candidates.Count == 1)
        {
            fromRow = candidates[0].row;
            fromCol = candidates[0].col;
            return true;
        }

        // Use disambiguation
        if (!string.IsNullOrEmpty(disambiguation))
        {
            foreach (var candidate in candidates)
            {
                string candidateFile = ((char)('a' + candidate.col)).ToString();
                string candidateRank = (8 - candidate.row).ToString();

                if (disambiguation == candidateFile || disambiguation == candidateRank)
                {
                    fromRow = candidate.row;
                    fromCol = candidate.col;
                    return true;
                }
            }
        }

        return false;
    }


    #endregion

    #region AI Handling

    void OnAIMoveReceived(string aiMove)
{
    // aiMove is like "d7e6" or "e2e4"
    if (aiMove.Length < 4) return;

    // 1) Parse coordinates
    int fromCol = aiMove[0] - 'a';
    int fromRow = 8 - (aiMove[1] - '0');
    int toCol   = aiMove[2] - 'a';
    int toRow   = 8 - (aiMove[3] - '0');

    // 2) Capture state before moving
    var piece     = chessRules.GetPiece(fromRow, fromCol);
    bool wasCapture = chessRules.GetPiece(toRow, toCol) != null;

    // 3) Execute the move exactly once
    chessRules.ExecuteMove(fromRow, fromCol, toRow, toCol);

    // 4) Generate standardized notation
    string moveNotation = GenerateAlgebraicNotation(
        piece.type,
        fromRow, fromCol,
        toRow,   toCol,
        wasCapture
    );

    // 5) Log it, advance turn
    AddMoveToLog(moveNotation);
    chessRules.NextTurn();
    CheckGameStateAndContinue();
}


    string ConvertToAlgebraic(int fromRow, int fromCol, int toRow, int toCol)
    {
        ChessRules.ChessPiece piece = chessRules.GetPiece(toRow, toCol);
        if (piece == null) return "??";

        string notation = "";

        if (piece.type != ChessRules.PieceType.Pawn)
        {
            notation += GetPieceSymbol(piece.type);
        }

        char file = (char)('a' + toCol);
        int rank = 8 - toRow;
        notation += file.ToString() + rank.ToString();

        return notation;
    }

    string GetPieceSymbol(ChessRules.PieceType type)
    {
        switch (type)
        {
            case ChessRules.PieceType.Queen: return "Q";
            case ChessRules.PieceType.Rook: return "R";
            case ChessRules.PieceType.Bishop: return "B";
            case ChessRules.PieceType.Knight: return "N";
            default: return "";
        }
    }

    void ShowThinkingMessage(string message)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = message;
            errorMessageText.gameObject.SetActive(true);
        }
    }

    void ShowEngineStatus(string message)
    {
        ShowErrorMessage(message);
        StartCoroutine(HideErrorMessageAfterDelay());
    }

    #endregion

    #region UI Events

    void OnDifficultyChanged(int value)
    {
        if (chessAI != null)
        {
            chessAI.SetDifficulty(value);
        }

        isDifficultySet = true;

        if (difficultyPromptObject != null)
        {
            difficultyPromptObject.SetActive(false);
        }

        moveInputField.interactable = true;

        string[] difficulties = { "Easy", "Medium", "Hard" };
        string difficultyName = value < difficulties.Length ? difficulties[value] : "Easy";

        ShowErrorMessage($"Difficulty set to {difficultyName}.");
        StartCoroutine(FocusInputField());
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

    #endregion

    #region Board Visualization

    IEnumerator RevealBoardTemporarily()
    {
        chessBoardObject.SetActive(true);
        SpawnAllPieces();

        yield return new WaitForSeconds(revealDuration);

        chessBoardObject.SetActive(false);
    }

    void SpawnAllPieces()
    {
        UnityEngine.Debug.Log("=== SPAWNING ALL PIECES AS UI IMAGES ===");

        ClearAllPieceObjects();

        int piecesCreated = 0;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                ChessRules.ChessPiece piece = chessRules.GetPiece(row, col);
                if (piece == null || piece.type == ChessRules.PieceType.None) continue;

                Transform squareTransform = FindSquareTransform(row, col);
                if (squareTransform == null)
                {
                    UnityEngine.Debug.LogWarning($"Could not find square at row {row}, col {col}");
                    continue;
                }

                GameObject pieceObj = CreatePieceImage(piece, squareTransform, row, col);
                if (pieceObj != null)
                {
                    pieceObjects[row, col] = pieceObj;
                    piecesCreated++;
                    UnityEngine.Debug.Log($"Created {piece.color} {piece.type} at {squareTransform.name}");
                }
            }
        }

        UnityEngine.Debug.Log($"=== CREATED {piecesCreated} PIECE IMAGES ===");
    }

    GameObject CreatePieceImage(ChessRules.ChessPiece piece, Transform parent, int row, int col)
    {
        Sprite pieceSprite = GetSpriteForPiece(piece);
        if (pieceSprite == null)
        {
            UnityEngine.Debug.LogError($"No sprite assigned for {piece.color} {piece.type}!");
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

        UnityEngine.Debug.Log($"Created piece image: {piece.color} {piece.type} with sprite {pieceSprite.name}");
        return pieceObj;
    }

    Sprite GetSpriteForPiece(ChessRules.ChessPiece piece)
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
                default: return null;
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
                default: return null;
            }
        }
    }

    void ClearAllPieceObjects()
    {
        UnityEngine.Debug.Log("Clearing all existing piece objects...");

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (pieceObjects[row, col] != null)
                {
                    UnityEngine.Debug.Log($"Destroying piece at [{row},{col}]: {pieceObjects[row, col].name}");
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

    #endregion

    #region Game Management

    void AddMoveToLog(string moveNotation)
    {
        // Add check notation if opponent is in check after this move
        ChessRules.PieceColor opponentColor = chessRules.IsWhiteTurn ? ChessRules.PieceColor.Black : ChessRules.PieceColor.White;
        if (chessRules.IsInCheck(opponentColor))
        {
            if (chessRules.IsCheckmate(opponentColor))
                moveNotation += "#";
            else
                moveNotation += "+";
        }

        moveHistory.Add(moveNotation);

        if (chessRules.IsWhiteTurn)
        {
            moveLogText.text += $"{chessRules.MoveNumber}. {moveNotation} ";
        }
        else
        {
            moveLogText.text += $"{moveNotation}\n";
        }
    }

    public void ResetGame()
    {
        // Reset UI state
        currentRevealCount = maxRevealCount;
        moveHistory.Clear();
        moveLogText.text = "Type your moves (e.g., e4, Nf3)\n";
        moveInputField.text = "";
        moveInputField.interactable = false;
        isDifficultySet = false;

        // Show difficulty prompt again
        if (difficultyPromptObject != null)
        {
            difficultyPromptObject.SetActive(true);
        }

        if (errorMessageText != null)
        {
            errorMessageText.gameObject.SetActive(false);
        }

        ClearAllPieceObjects();
        chessBoardObject.SetActive(false);
        UpdateRevealButtonText();

        if (revealBoardButton != null)
            revealBoardButton.interactable = true;

        // Reset game components
        if (chessRules != null)
        {
            chessRules.ResetGame();
        }

        if (chessAI != null)
        {
            chessAI.ResetAI();
        }

        StartCoroutine(FocusInputField());
    }

    public void OnSaveLogClicked()
    {
        string logText = moveLogText.text;
        string filePath = Path.Combine(Application.persistentDataPath, "blindfold_game_log.txt");

        try
        {
            File.WriteAllText(filePath, logText);
            ShowErrorMessage($"Log saved to: {filePath}");
            UnityEngine.Debug.Log($"Move log saved to {filePath}");
        }
        catch (System.Exception e)
        {
            ShowErrorMessage("Failed to save log: " + e.Message);
        }
    }

    #endregion

    #region Utility Methods

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

    void ShowErrorMessage(string message)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = message;
            errorMessageText.gameObject.SetActive(true);
        }
        UnityEngine.Debug.LogWarning($"Chess Error: {message}");
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
        if (moveInputField.interactable)
        {
            moveInputField.Select();
            moveInputField.ActivateInputField();
        }
    }

    void RefocusInputField()
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

    void ClearInputAndRefocus()
    {
        moveInputField.text = "";
        RefocusInputField();
    }

    #endregion

    #region Cleanup

    void OnDestroy()
    {
        // Unsubscribe from AI events
        if (chessAI != null)
        {
            chessAI.OnAIMoveReady -= OnAIMoveReceived;
            chessAI.OnAIThinking -= ShowThinkingMessage;
            chessAI.OnEngineStatus -= ShowEngineStatus;
            chessAI.OnEngineError -= ShowErrorMessage;
        }
    }

    #endregion

    #region Debug Methods

    [ContextMenu("Force Spawn Pieces")]
    public void ForceSpawnPieces()
    {
        chessBoardObject.SetActive(true);
        SpawnAllPieces();
    }

    [ContextMenu("Check Sprite Assignments")]
    public void CheckSpriteAssignments()
    {
        UnityEngine.Debug.Log("=== CHECKING SPRITE ASSIGNMENTS ===");
        UnityEngine.Debug.Log($"White Pawn: {(whitePawnSprite != null ? whitePawnSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"White Knight: {(whiteKnightSprite != null ? whiteKnightSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"White Bishop: {(whiteBishopSprite != null ? whiteBishopSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"White Rook: {(whiteRookSprite != null ? whiteRookSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"White Queen: {(whiteQueenSprite != null ? whiteQueenSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"White King: {(whiteKingSprite != null ? whiteKingSprite.name : "NULL")}");

        UnityEngine.Debug.Log($"Black Pawn: {(blackPawnSprite != null ? blackPawnSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"Black Knight: {(blackKnightSprite != null ? blackKnightSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"Black Bishop: {(blackBishopSprite != null ? blackBishopSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"Black Rook: {(blackRookSprite != null ? blackRookSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"Black Queen: {(blackQueenSprite != null ? blackQueenSprite.name : "NULL")}");
        UnityEngine.Debug.Log($"Black King: {(blackKingSprite != null ? blackKingSprite.name : "NULL")}");
    }
    private string GenerateAlgebraicNotation(
        ChessRules.PieceType type,
        int fromRow, int fromCol,
        int toRow,   int toCol,
        bool wasCapture)
    {
        // File/rank of origin and destination
        char fromFile = (char)('a' + fromCol);
        char toFile   = (char)('a' + toCol);
        int  toRank   = 8 - toRow;

        // Handle pawns specially
        if (type == ChessRules.PieceType.Pawn)
        {
            if (wasCapture)
                return $"{fromFile}x{toFile}{toRank}";
            else
                return $"{toFile}{toRank}";
        }

        // Map piece types to symbols
        string symbol = type switch {
            ChessRules.PieceType.Knight => "N",
            ChessRules.PieceType.Bishop => "B",
            ChessRules.PieceType.Rook   => "R",
            ChessRules.PieceType.Queen  => "Q",
            ChessRules.PieceType.King   => "K",
            _ => ""
        };

        // Quiet move vs capture
        string captureMark = wasCapture ? "x" : "";

        return $"{symbol}{captureMark}{toFile}{toRank}";
    }

    #endregion
}