using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;

public class BlindfoldModeManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField moveInputField;
    public TMP_Text moveLogText;
    public TMP_Text errorMessageText;
    public GameObject chessBoardObject;
    public Button revealBoardButton;

    [Header("AI Settings")]
    public TMP_Dropdown difficultyDropdown;

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

    public enum PieceType { None, Pawn, Knight, Bishop, Rook, Queen, King }
    public enum PieceColor { White, Black }
    public enum AIDifficulty { Easy = 2, Medium = 4, Hard = 6 }
    public GameObject difficultyPromptObject;
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
    private AIDifficulty currentDifficulty = AIDifficulty.Easy;

    // Chess rule tracking
    private bool whiteKingMoved = false;
    private bool blackKingMoved = false;
    private bool whiteKingsideRookMoved = false;
    private bool whiteQueensideRookMoved = false;
    private bool blackKingsideRookMoved = false;
    private bool blackQueensideRookMoved = false;
    private Vector2Int enPassantTarget = new Vector2Int(-1, -1);
    private Vector2Int lastPawnDoubleMove = new Vector2Int(-1, -1);

    // Threefold repetition tracking
    private List<string> positionHistory = new List<string>();
    private Dictionary<string, int> positionCounts = new Dictionary<string, int>();

    // FIXED STOCKFISH IMPLEMENTATION
    private Process stockfishProcess;
    private Queue<string> outputQueue = new Queue<string>();
    private Queue<string> commandQueue = new Queue<string>();
    private bool isEngineReady = false;
    private bool isProcessingCommand = false;
    private bool isProcessingAIMove = false;
    private bool isDifficultySet = false; // Track if user has selected difficulty
    private string lastBestMove = "";

    void Start()
    {
        currentRevealCount = maxRevealCount;
        SetupStartingPosition();

        // Initialize Stockfish with proper implementation
        StartStockfish();

        chessBoardObject.SetActive(false);

        revealBoardButton.onClick.AddListener(OnRevealBoardClicked);
        moveLogText.text = "Welcome to Blindfold Chess!\n";
        moveInputField.text = "";

        // Setup difficulty dropdown - FORCE USER TO CHOOSE
        if (difficultyDropdown != null)
        {
            difficultyDropdown.onValueChanged.AddListener(OnDifficultyChanged);
            // DO NOT set initial difficulty - force user to choose
            isDifficultySet = false;
        }
        else
        {
            // If no dropdown, assume difficulty is set to default
            isDifficultySet = true;
        }

        // Show difficulty prompt at start
        if (difficultyPromptObject != null)
        {
            difficultyPromptObject.SetActive(true);
        }

        // Disable input initially until difficulty is chosen
        moveInputField.interactable = false;

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
        // Don't focus input field initially - wait for difficulty selection
    }

    // PROPER STOCKFISH PATH USING STREAMINGASSETS
    string GetStockfishPath()
    {
        string fileName;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        fileName = "stockfish.exe";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        fileName = "stockfish-mac";
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        fileName = "stockfish-linux";
#elif UNITY_ANDROID
        fileName = "stockfish-android";
#else
        fileName = "stockfish.exe";
#endif

        return Path.Combine(Application.streamingAssetsPath, fileName);
    }

    // PROPER STOCKFISH INITIALIZATION
    void StartStockfish()
    {
        try
        {
            string stockfishPath = GetStockfishPath();

            if (!File.Exists(stockfishPath))
            {
                ShowErrorMessage($"Stockfish not found at: {stockfishPath}");
                UnityEngine.Debug.LogError($"Stockfish not found. Please place stockfish executable in Assets/StreamingAssets/ folder");
                return;
            }

            stockfishProcess = new Process();

            stockfishProcess.StartInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(stockfishPath)
            };

            // Set up event handlers for async communication
            stockfishProcess.OutputDataReceived += OnOutputDataReceived;
            stockfishProcess.ErrorDataReceived += OnErrorDataReceived;

            stockfishProcess.Start();

            // Begin async reading
            stockfishProcess.BeginOutputReadLine();
            stockfishProcess.BeginErrorReadLine();

            // Initialize UCI
            StartCoroutine(InitializeUCI());
        }
        catch (System.Exception ex)
        {
            ShowErrorMessage($"Failed to start Stockfish: {ex.Message}");
        }
    }

    // ASYNC EVENT HANDLERS
    void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            lock (outputQueue)
            {
                outputQueue.Enqueue(e.Data);
            }
        }
    }

    void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            UnityEngine.Debug.LogError($"Stockfish Error: {e.Data}");
        }
    }

    void Update()
    {
        ProcessOutputQueue();
        ProcessCommandQueue();
    }

    // PROCESS OUTPUT ON MAIN THREAD
    void ProcessOutputQueue()
    {
        lock (outputQueue)
        {
            while (outputQueue.Count > 0)
            {
                string line = outputQueue.Dequeue();
                ProcessEngineLine(line);
            }
        }
    }

    void ProcessEngineLine(string line)
    {
        UnityEngine.Debug.Log($"Stockfish: {line}");

        if (line.StartsWith("uciok"))
        {
            isEngineReady = true;
        }
        else if (line.StartsWith("readyok"))
        {
            isProcessingCommand = false;
        }
        else if (line.StartsWith("bestmove"))
        {
            string[] parts = line.Split(' ');
            if (parts.Length >= 2)
            {
                lastBestMove = parts[1];
                if (!string.IsNullOrEmpty(lastBestMove) && lastBestMove != "(none)")
                {
                    ExecuteAIMove(lastBestMove);
                }
            }
            isProcessingCommand = false;
            isProcessingAIMove = false;

            // Hide AI thinking message
            if (errorMessageText != null)
            {
                errorMessageText.gameObject.SetActive(false);
            }
        }
        else if (line.StartsWith("info"))
        {
            // Handle analysis info if needed
        }
    }

    // PROPER UCI INITIALIZATION
    IEnumerator InitializeUCI()
    {
        // Send UCI command
        SendCommand("uci");

        // Wait for UCI OK
        float timeout = 10f;
        float elapsed = 0f;

        while (!isEngineReady && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!isEngineReady)
        {
            ShowErrorMessage("Failed to initialize UCI");
            yield break;
        }

        // Configure engine options
        SendCommand($"setoption name Skill Level value {(int)currentDifficulty}");
        yield return new WaitForSeconds(0.1f);

        SendCommand("ucinewgame");
        yield return new WaitForSeconds(0.1f);

        // Final readiness check
        yield return StartCoroutine(WaitForReady());

        UnityEngine.Debug.Log("Stockfish ready!");
        ShowErrorMessage("Stockfish engine ready!");
        StartCoroutine(HideErrorMessageAfterDelay());
    }

    IEnumerator WaitForReady()
    {
        isProcessingCommand = true;
        SendCommand("isready");

        float timeout = 5f;
        float elapsed = 0f;

        while (isProcessingCommand && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (isProcessingCommand)
        {
            ShowErrorMessage("Engine not responding to isready");
        }
    }

    // COMMAND QUEUE PROCESSING
    void ProcessCommandQueue()
    {
        if (!isProcessingCommand && commandQueue.Count > 0)
        {
            string command = commandQueue.Dequeue();
            SendCommandImmediate(command);
        }
    }

    public void SendCommand(string command)
    {
        commandQueue.Enqueue(command);
    }

    void SendCommandImmediate(string command)
    {
        try
        {
            if (stockfishProcess != null && !stockfishProcess.HasExited)
            {
                stockfishProcess.StandardInput.WriteLine(command);
                stockfishProcess.StandardInput.Flush();
                UnityEngine.Debug.Log($"Sent: {command}");

                // Mark as processing if it's a command that expects a response
                if (command.StartsWith("go") || command == "isready")
                {
                    isProcessingCommand = true;
                }
            }
        }
        catch (System.Exception ex)
        {
            ShowErrorMessage($"Error sending command: {ex.Message}");
        }
    }

    // SIMPLIFIED AI MOVE REQUEST
    public void GetAIMove()
    {
        if (!isEngineReady || isProcessingAIMove)
        {
            return;
        }

        StartCoroutine(GetAIMoveCoroutine());
    }

    IEnumerator GetAIMoveCoroutine()
    {
        isProcessingAIMove = true;

        // Show AI thinking message
        if (errorMessageText != null)
        {
            errorMessageText.text = "AI is thinking...";
            errorMessageText.gameObject.SetActive(true);
        }

        string fen = GetCurrentFEN();
        UnityEngine.Debug.Log($"Sending position: {fen}");

        // Send position command immediately
        SendCommandImmediate($"position fen {fen}");

        // Wait a bit for position to be processed
        yield return new WaitForSeconds(0.3f);

        // Send isready and wait for confirmation
        SendCommandImmediate("isready");

        // Wait for readyok response
        float timeout = 5f;
        float elapsed = 0f;
        bool engineReady = false;

        while (elapsed < timeout && !engineReady)
        {
            // Check if we received readyok (this sets isProcessingCommand to false)
            if (!isProcessingCommand)
            {
                engineReady = true;
                break;
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!engineReady)
        {
            UnityEngine.Debug.LogWarning("Engine not ready, proceeding anyway...");
        }

        // Request move calculation
        int thinkTime = currentDifficulty switch
        {
            AIDifficulty.Easy => 2000,
            AIDifficulty.Medium => 3000,
            AIDifficulty.Hard => 5000,
            _ => 3000
        };

        UnityEngine.Debug.Log($"Sending go command with {thinkTime}ms think time");
        SendCommandImmediate($"go movetime {thinkTime}");
    }

    void OnDifficultyChanged(int value)
    {
        switch (value)
        {
            case 0: currentDifficulty = AIDifficulty.Easy; break;
            case 1: currentDifficulty = AIDifficulty.Medium; break;
            case 2: currentDifficulty = AIDifficulty.Hard; break;
        }

        // Mark difficulty as set when user changes it
        isDifficultySet = true;

        // Hide the difficulty prompt
        if (difficultyPromptObject != null)
        {
            difficultyPromptObject.SetActive(false);
        }

        // Enable input field now that difficulty is chosen
        moveInputField.interactable = true;

        if (isEngineReady)
        {
            SendCommand($"setoption name Skill Level value {(int)currentDifficulty}");
        }

        // Update UI to show difficulty is set and game is ready
        ShowErrorMessage($"Difficulty set to {currentDifficulty}. Game ready! Make your first move.");

        // Focus the input field so user can start typing
        StartCoroutine(FocusInputField());
    }

    // Get current position in FEN notation for Stockfish and repetition tracking
    string GetCurrentFEN()
    {
        StringBuilder fen = new StringBuilder();

        // Board position
        for (int row = 0; row < 8; row++)
        {
            int emptyCount = 0;
            for (int col = 0; col < 8; col++)
            {
                ChessPiece piece = boardState[row, col];
                if (piece == null)
                {
                    emptyCount++;
                }
                else
                {
                    if (emptyCount > 0)
                    {
                        fen.Append(emptyCount);
                        emptyCount = 0;
                    }

                    char pieceChar = ' ';
                    switch (piece.type)
                    {
                        case PieceType.Pawn: pieceChar = 'p'; break;
                        case PieceType.Knight: pieceChar = 'n'; break;
                        case PieceType.Bishop: pieceChar = 'b'; break;
                        case PieceType.Rook: pieceChar = 'r'; break;
                        case PieceType.Queen: pieceChar = 'q'; break;
                        case PieceType.King: pieceChar = 'k'; break;
                    }

                    if (piece.color == PieceColor.White)
                        pieceChar = char.ToUpper(pieceChar);

                    fen.Append(pieceChar);
                }
            }

            if (emptyCount > 0)
                fen.Append(emptyCount);

            if (row < 7)
                fen.Append('/');
        }

        // Active color
        fen.Append(isWhiteTurn ? " w " : " b ");

        // Castling rights
        StringBuilder castling = new StringBuilder();
        if (!whiteKingMoved)
        {
            if (!whiteKingsideRookMoved) castling.Append('K');
            if (!whiteQueensideRookMoved) castling.Append('Q');
        }
        if (!blackKingMoved)
        {
            if (!blackKingsideRookMoved) castling.Append('k');
            if (!blackQueensideRookMoved) castling.Append('q');
        }
        if (castling.Length == 0) castling.Append('-');
        fen.Append(castling).Append(' ');

        // En passant
        if (enPassantTarget.x >= 0)
        {
            char file = (char)('a' + enPassantTarget.y);
            int rank = 8 - enPassantTarget.x;
            fen.Append(file).Append(rank);
        }
        else
        {
            fen.Append('-');
        }

        // Halfmove and fullmove (simplified)
        fen.Append(" 0 ").Append(moveNumber);

        return fen.ToString();
    }

    // Check for threefold repetition
    bool IsThreefoldRepetition()
    {
        string currentPosition = GetCurrentFEN().Split(' ')[0]; // Just the board position part

        if (positionCounts.ContainsKey(currentPosition))
        {
            return positionCounts[currentPosition] >= 3;
        }
        return false;
    }

    // Check for insufficient material
    bool IsInsufficientMaterial()
    {
        List<ChessPiece> whitePieces = new List<ChessPiece>();
        List<ChessPiece> blackPieces = new List<ChessPiece>();

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                ChessPiece piece = boardState[row, col];
                if (piece != null)
                {
                    if (piece.color == PieceColor.White)
                        whitePieces.Add(piece);
                    else
                        blackPieces.Add(piece);
                }
            }
        }

        // Remove kings from consideration
        whitePieces.RemoveAll(p => p.type == PieceType.King);
        blackPieces.RemoveAll(p => p.type == PieceType.King);

        // King vs King
        if (whitePieces.Count == 0 && blackPieces.Count == 0)
            return true;

        // King vs King + Knight or Bishop
        if ((whitePieces.Count == 0 && blackPieces.Count == 1) ||
            (blackPieces.Count == 0 && whitePieces.Count == 1))
        {
            ChessPiece singlePiece = whitePieces.Count == 1 ? whitePieces[0] : blackPieces[0];
            if (singlePiece.type == PieceType.Knight || singlePiece.type == PieceType.Bishop)
                return true;
        }

        // King + Bishop vs King + Bishop (same color squares) - simplified check
        if (whitePieces.Count == 1 && blackPieces.Count == 1)
        {
            if (whitePieces[0].type == PieceType.Bishop && blackPieces[0].type == PieceType.Bishop)
                return true; // Simplified - should check if bishops are on same color squares
        }

        return false;
    }

    // Update position tracking
    void UpdatePositionHistory()
    {
        string position = GetCurrentFEN().Split(' ')[0]; // Just board position
        positionHistory.Add(position);

        if (positionCounts.ContainsKey(position))
            positionCounts[position]++;
        else
            positionCounts[position] = 1;
    }

    // Execute AI move and convert to proper algebraic notation
    void ExecuteAIMove(string move)
    {
        if (move.Length < 4) return;

        int fromCol = move[0] - 'a';
        int fromRow = 8 - (move[1] - '0');
        int toCol = move[2] - 'a';
        int toRow = 8 - (move[3] - '0');

        // Store the piece before moving for notation
        ChessPiece piece = boardState[fromRow, fromCol];
        if (piece == null) return;

        // Handle promotion
        PieceType promotionPiece = PieceType.Queen;
        if (move.Length == 5)
        {
            switch (move[4])
            {
                case 'q': promotionPiece = PieceType.Queen; break;
                case 'r': promotionPiece = PieceType.Rook; break;
                case 'b': promotionPiece = PieceType.Bishop; break;
                case 'n': promotionPiece = PieceType.Knight; break;
            }
        }

        // Check for castling
        if (piece.type == PieceType.King && Mathf.Abs(toCol - fromCol) == 2)
        {
            bool kingside = toCol > fromCol;
            ExecuteCastling(piece.color, kingside);
            string castlingNotation = kingside ? "O-O" : "O-O-O";
            AddMoveToLog(castlingNotation);
        }
        else
        {
            // Convert to proper algebraic notation BEFORE executing the move
            string moveNotation = ConvertToProperAlgebraic(fromRow, fromCol, toRow, toCol, piece.color);

            ExecuteMove(fromRow, fromCol, toRow, toCol);

            // Handle AI promotion
            if (piece.type == PieceType.Pawn && (toRow == 0 || toRow == 7))
            {
                boardState[toRow, toCol] = new ChessPiece(promotionPiece, piece.color) { hasMoved = true };
                moveNotation += "=" + GetPieceSymbol(promotionPiece);

                // Update visual if board visible
                if (chessBoardObject.activeInHierarchy && pieceObjects[toRow, toCol] != null)
                {
                    Image img = pieceObjects[toRow, toCol].GetComponent<Image>();
                    img.sprite = GetSpriteForPiece(boardState[toRow, toCol]);
                }
            }

            AddMoveToLog(moveNotation);
        }

        isWhiteTurn = !isWhiteTurn;
        CheckGameState();
    }

    // Convert to proper algebraic notation like Nc3, Bb5, etc.
    string ConvertToProperAlgebraic(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        ChessPiece piece = boardState[fromRow, fromCol];
        if (piece == null) return "??";

        string notation = "";

        // Add piece symbol (except for pawns)
        if (piece.type != PieceType.Pawn)
        {
            notation += GetPieceSymbol(piece.type);
        }

        // Check if we need disambiguation for non-pawn pieces
        if (piece.type != PieceType.Pawn)
        {
            string disambiguation = GetDisambiguation(fromRow, fromCol, toRow, toCol, piece);
            notation += disambiguation;
        }

        // Check for capture
        bool isCapture = false;
        ChessPiece targetPiece = boardState[toRow, toCol];

        // Regular capture
        if (targetPiece != null && targetPiece.color != piece.color)
        {
            isCapture = true;
        }

        // En passant capture
        if (piece.type == PieceType.Pawn && targetPiece == null && fromCol != toCol)
        {
            isCapture = true;
        }

        if (isCapture)
        {
            // For pawn captures, include the file of departure
            if (piece.type == PieceType.Pawn)
            {
                notation += (char)('a' + fromCol);
            }
            notation += "x";
        }

        // Add destination square
        char file = (char)('a' + toCol);
        int rank = 8 - toRow;
        notation += file.ToString() + rank.ToString();

        return notation;
    }

    // Get disambiguation for pieces (when multiple pieces of same type can move to same square)
    string GetDisambiguation(int fromRow, int fromCol, int toRow, int toCol, ChessPiece piece)
    {
        // Find other pieces of the same type that can move to the same square
        List<(int row, int col)> candidates = new List<(int, int)>();

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (row == fromRow && col == fromCol) continue; // Skip the moving piece itself

                ChessPiece otherPiece = boardState[row, col];
                if (otherPiece != null && otherPiece.type == piece.type && otherPiece.color == piece.color)
                {
                    if (CanPieceMoveTo(row, col, toRow, toCol, otherPiece) &&
                        !WouldMoveLeaveKingInCheck(row, col, toRow, toCol, piece.color))
                    {
                        candidates.Add((row, col));
                    }
                }
            }
        }

        if (candidates.Count == 0) return ""; // No ambiguity

        // Check if file disambiguation is enough
        bool fileUnique = true;
        foreach (var candidate in candidates)
        {
            if (candidate.col == fromCol)
            {
                fileUnique = false;
                break;
            }
        }

        if (fileUnique)
        {
            return ((char)('a' + fromCol)).ToString();
        }

        // Check if rank disambiguation is enough
        bool rankUnique = true;
        foreach (var candidate in candidates)
        {
            if (candidate.row == fromRow)
            {
                rankUnique = false;
                break;
            }
        }

        if (rankUnique)
        {
            return (8 - fromRow).ToString();
        }

        // Use full square notation as last resort
        char file = (char)('a' + fromCol);
        int rank = 8 - fromRow;
        return file.ToString() + rank.ToString();
    }

    // Get piece symbol for notation
    string GetPieceSymbol(PieceType type)
    {
        switch (type)
        {
            case PieceType.Queen: return "Q";
            case PieceType.Rook: return "R";
            case PieceType.Bishop: return "B";
            case PieceType.Knight: return "N";
            default: return "";
        }
    }

    // Promotion handling - Simplified version
    void HandlePromotion(int row, int col, PieceColor color, PieceType promotionPiece = PieceType.Queen)
    {
        boardState[row, col] = new ChessPiece(promotionPiece, color) { hasMoved = true };

        // Update visual if board visible
        if (chessBoardObject.activeInHierarchy && pieceObjects[row, col] != null)
        {
            Image img = pieceObjects[row, col].GetComponent<Image>();
            img.sprite = GetSpriteForPiece(boardState[row, col]);
        }
    }

    // Enhanced move execution with promotion choice
    void ExecuteMove(int fromRow, int fromCol, int toRow, int toCol)
    {
        ChessPiece piece = boardState[fromRow, fromCol];
        ChessPiece capturedPiece = boardState[toRow, toCol];

        // Handle en passant capture
        bool isEnPassant = false;
        if (piece.type == PieceType.Pawn && capturedPiece == null && fromCol != toCol)
        {
            isEnPassant = true;
            int capturedPawnRow = piece.color == PieceColor.White ? toRow + 1 : toRow - 1;
            capturedPiece = boardState[capturedPawnRow, toCol];
            boardState[capturedPawnRow, toCol] = null;

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

        // Update castling flags
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

        // Handle pawn promotion - Auto promote to Queen or specified piece
        if (piece.type == PieceType.Pawn && (toRow == 0 || toRow == 7))
        {
            // Default to Queen promotion
            HandlePromotion(toRow, toCol, piece.color, PieceType.Queen);
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
            if (pieceObjects[toRow, toCol] != null && !isEnPassant)
            {
                DestroyImmediate(pieceObjects[toRow, toCol]);
                pieceObjects[toRow, toCol] = null;
            }

            if (pieceObjects[fromRow, fromCol] != null)
            {
                Transform newSquare = FindSquareTransform(toRow, toCol);
                if (newSquare != null)
                {
                    pieceObjects[fromRow, fromCol].transform.SetParent(newSquare, false);
                    pieceObjects[fromRow, fromCol].GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

                    pieceObjects[toRow, toCol] = pieceObjects[fromRow, fromCol];
                    pieceObjects[fromRow, fromCol] = null;

                    ChessPiece currentPiece = boardState[toRow, toCol];
                    pieceObjects[toRow, toCol].name = $"{currentPiece.color}_{currentPiece.type}_R{toRow}C{toCol}";

                    if (currentPiece.type != piece.type)
                    {
                        Image img = pieceObjects[toRow, toCol].GetComponent<Image>();
                        img.sprite = GetSpriteForPiece(currentPiece);
                    }
                }
            }
        }

        if (capturedPiece != null)
            UIButtonHoverSound.Instance?.PlayCapture();
        else
            UIButtonHoverSound.Instance?.PlayMove();

        UpdatePositionHistory();
    }

    // Enhanced move parsing with promotion notation - Auto-promotion version
    bool TryParseChessNotation(string notation, PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol, out PieceType promotionPiece)
    {
        fromRow = fromCol = toRow = toCol = -1;
        promotionPiece = PieceType.Queen; // Default promotion

        if (string.IsNullOrEmpty(notation)) return false;

        string cleanNotation = notation.ToLower().Trim().Replace("+", "").Replace("#", "");

        // Extract promotion piece if present
        if (cleanNotation.Length > 2)
        {
            string lastPart = cleanNotation.Substring(cleanNotation.Length - 1);
            if ("qrbn".Contains(lastPart))
            {
                switch (lastPart)
                {
                    case "q": promotionPiece = PieceType.Queen; break;
                    case "r": promotionPiece = PieceType.Rook; break;
                    case "b": promotionPiece = PieceType.Bishop; break;
                    case "n": promotionPiece = PieceType.Knight; break;
                }
                cleanNotation = cleanNotation.Substring(0, cleanNotation.Length - 1);
            }

            // Remove promotion symbols
            cleanNotation = cleanNotation.Replace("=", "").Replace("(", "").Replace(")", "").Replace("/", "");
        }

        bool hasCapture = cleanNotation.Contains("x");
        cleanNotation = cleanNotation.Replace("x", "");

        // Try full coordinate notation first
        if (TryParseFullCoordinateNotation(cleanNotation, color, out fromRow, out fromCol, out toRow, out toCol))
        {
            return true;
        }

        // Parse destination square
        if (cleanNotation.Length < 2) return false;

        char fileChar = cleanNotation[cleanNotation.Length - 2];
        char rankChar = cleanNotation[cleanNotation.Length - 1];

        if (fileChar < 'a' || fileChar > 'h' || rankChar < '1' || rankChar > '8') return false;

        toCol = fileChar - 'a';
        toRow = 8 - (rankChar - '0');

        // Determine piece type
        PieceType pieceType = PieceType.Pawn;
        int startIndex = 0;
        string disambiguation = "";

        if (cleanNotation.Length > 2)
        {
            char firstChar = cleanNotation[0];
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

        // Extract disambiguation if present
        if (cleanNotation.Length > startIndex + 2)
        {
            disambiguation = cleanNotation.Substring(startIndex, cleanNotation.Length - startIndex - 2);
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

    // Overload for backward compatibility
    bool TryParseChessNotation(string notation, PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol)
    {
        PieceType promotionPiece;
        return TryParseChessNotation(notation, color, out fromRow, out fromCol, out toRow, out toCol, out promotionPiece);
    }

    // Parse full coordinate notation like e2e4, a1h8, Nb1c3
    bool TryParseFullCoordinateNotation(string notation, PieceColor color, out int fromRow, out int fromCol, out int toRow, out int toCol)
    {
        fromRow = fromCol = toRow = toCol = -1;

        // Remove piece prefix if present
        string workingNotation = notation;
        if (notation.Length > 4)
        {
            char firstChar = notation[0];
            if (firstChar == 'n' || firstChar == 'b' || firstChar == 'r' || firstChar == 'q' || firstChar == 'k')
            {
                workingNotation = notation.Substring(1);
            }
        }

        // Must be exactly 4 characters for coordinate notation
        if (workingNotation.Length != 4) return false;

        // Parse squares
        char fromFile = workingNotation[0];
        char fromRank = workingNotation[1];
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

        // Verify there's a piece of the right color at the from square
        ChessPiece piece = boardState[fromRow, fromCol];
        if (piece == null || piece.color != color)
        {
            return false;
        }

        return true;
    }

    // Enhanced move processing with promotion support
    void ProcessPlayerMove(string playerMove)
    {
        if (string.IsNullOrWhiteSpace(playerMove))
        {
            RefocusInputField();
            return;
        }

        // Check if difficulty has been set
        if (!isDifficultySet)
        {
            ShowErrorMessage("Please select a difficulty level first!");
            moveInputField.text = "";
            RefocusInputField();
            return;
        }

        // Check if engine is ready
        if (!isEngineReady)
        {
            ShowErrorMessage("Chess engine is still loading. Please wait...");
            moveInputField.text = "";
            RefocusInputField();
            return;
        }

        playerMove = playerMove.Trim();
        PieceColor currentColor = isWhiteTurn ? PieceColor.White : PieceColor.Black;

        // Check for castling
        string upperMove = playerMove.ToUpper();
        if (upperMove == "O-O" || upperMove == "0-0")
        {
            if (CanCastle(currentColor, true))
            {
                ExecuteCastling(currentColor, true);
                string moveNotation = "O-O";
                AddMoveToLog(moveNotation);
                isWhiteTurn = !isWhiteTurn;
                CheckGameState();

                // AI move after player castling
                if (!isWhiteTurn && isEngineReady)
                {
                    GetAIMove();
                }
            }
            else
            {
                ShowErrorMessage("Kingside castling is not legal!");
            }
            moveInputField.text = "";
            RefocusInputField();
            return;
        }

        if (upperMove == "O-O-O" || upperMove == "0-0-0")
        {
            if (CanCastle(currentColor, false))
            {
                ExecuteCastling(currentColor, false);
                string moveNotation = "O-O-O";
                AddMoveToLog(moveNotation);
                isWhiteTurn = !isWhiteTurn;
                CheckGameState();

                // AI move after player castling
                if (!isWhiteTurn && isEngineReady)
                {
                    GetAIMove();
                }
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
        if (TryParseChessNotation(playerMove, currentColor, out int fromRow, out int fromCol, out int toRow, out int toCol, out PieceType promotionPiece))
        {
            if (IsValidMove(fromRow, fromCol, toRow, toCol, currentColor))
            {
                // Handle promotion if specified
                ChessPiece movingPiece = boardState[fromRow, fromCol];
                bool isPromotion = movingPiece.type == PieceType.Pawn && (toRow == 0 || toRow == 7);

                ExecuteMove(fromRow, fromCol, toRow, toCol);

                // Apply specific promotion piece if this is a promotion move
                if (isPromotion)
                {
                    HandlePromotion(toRow, toCol, currentColor, promotionPiece);
                }

                string moveNotation = FormatMoveNotation(playerMove, fromRow, fromCol, toRow, toCol);
                if (isPromotion)
                {
                    moveNotation += "=" + GetPieceSymbol(promotionPiece);
                }
                AddMoveToLog(moveNotation);

                isWhiteTurn = !isWhiteTurn;
                CheckGameState();

                // Get AI move if it's AI's turn and Stockfish is ready
                if (!isWhiteTurn && isEngineReady)
                {
                    GetAIMove();
                }
                else if (!isEngineReady)
                {
                    ShowErrorMessage("Waiting for Stockfish to be ready...");
                }
            }
            else
            {
                ShowErrorMessage($"Invalid move: {playerMove}");
            }
        }
        else
        {
            ShowErrorMessage($"Invalid move: {playerMove}");
        }

        moveInputField.text = "";
        RefocusInputField();
    }

    // Enhanced move log with check notation
    void AddMoveToLog(string moveNotation)
    {
        // Add check notation if opponent is in check after this move
        PieceColor opponentColor = isWhiteTurn ? PieceColor.Black : PieceColor.White;
        if (IsInCheck(opponentColor))
        {
            if (IsCheckmate(opponentColor))
                moveNotation += "#";
            else
                moveNotation += "+";
        }

        moveHistory.Add(moveNotation);

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

    // Enhanced game state checking with new draw conditions
    void CheckGameState()
    {
        PieceColor currentColor = isWhiteTurn ? PieceColor.White : PieceColor.Black;
        PieceColor opponentColor = isWhiteTurn ? PieceColor.Black : PieceColor.White;

        if (IsCheckmate(currentColor))
        {
            string winner = opponentColor == PieceColor.White ? "White" : "Black";
            moveLogText.text += $"\nCheckmate! {winner} wins!\n";
            ShowErrorMessage($"Checkmate! {winner} wins!");
            moveInputField.interactable = false;
        }
        else if (IsStalemate(currentColor))
        {
            moveLogText.text += "\nStalemate! The game is a draw.\n";
            ShowErrorMessage("Stalemate! The game is a draw.");
            moveInputField.interactable = false;
        }
        else if (IsThreefoldRepetition())
        {
            moveLogText.text += "\nDraw by threefold repetition!\n";
            ShowErrorMessage("Draw by threefold repetition!");
            moveInputField.interactable = false;
        }
        else if (IsInsufficientMaterial())
        {
            moveLogText.text += "\nDraw by insufficient material!\n";
            ShowErrorMessage("Draw by insufficient material!");
            moveInputField.interactable = false;
        }
        else if (IsInCheck(currentColor))
        {
            UIButtonHoverSound.Instance?.PlayCheck();
            string playerInCheck = currentColor == PieceColor.White ? "White" : "Black";
            ShowErrorMessage($"{playerInCheck} is in check!");
        }
    }

    // Show error message
    void ShowErrorMessage(string message)
    {
        if (errorMessageText != null)
        {
            errorMessageText.text = message;
            errorMessageText.gameObject.SetActive(true);
            StartCoroutine(HideErrorMessageAfterDelay());
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

        // Reset all flags
        whiteKingMoved = blackKingMoved = false;
        whiteKingsideRookMoved = whiteQueensideRookMoved = false;
        blackKingsideRookMoved = blackQueensideRookMoved = false;
        enPassantTarget = new Vector2Int(-1, -1);
        lastPawnDoubleMove = new Vector2Int(-1, -1);

        // Clear position history
        positionHistory.Clear();
        positionCounts.Clear();
        UpdatePositionHistory();

        UnityEngine.Debug.Log("Starting position setup complete!");
    }

    // Check if the current player's king is in check
    bool IsInCheck(PieceColor color)
    {
        Vector2Int kingPos = FindKing(color);
        if (kingPos.x == -1) return false;

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

    // Check if a piece can attack a specific square
    bool CanPieceAttackSquare(int fromRow, int fromCol, int toRow, int toCol, ChessPiece piece)
    {
        if (fromRow == toRow && fromCol == toCol) return false;

        int deltaRow = toRow - fromRow;
        int deltaCol = toCol - fromCol;

        switch (piece.type)
        {
            case PieceType.Pawn:
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
        ChessPiece movingPiece = boardState[fromRow, fromCol];
        ChessPiece capturedPiece = boardState[toRow, toCol];

        boardState[toRow, toCol] = movingPiece;
        boardState[fromRow, fromCol] = null;

        bool kingInCheck = IsInCheck(color);

        boardState[fromRow, fromCol] = movingPiece;
        boardState[toRow, toCol] = capturedPiece;

        return kingInCheck;
    }

    // Check if castling is legal
    bool CanCastle(PieceColor color, bool kingside)
    {
        int row = color == PieceColor.White ? 7 : 0;

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

        if (IsInCheck(color)) return false;

        int kingCol = 4;
        int rookCol = kingside ? 7 : 0;
        int direction = kingside ? 1 : -1;

        for (int col = kingCol + direction; col != rookCol; col += direction)
        {
            if (boardState[row, col] != null) return false;
            if (IsSquareAttacked(row, col, color == PieceColor.White ? PieceColor.Black : PieceColor.White))
                return false;
        }

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

        UpdatePositionHistory();
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
        UnityEngine.Debug.Log("=== SPAWNING ALL PIECES AS UI IMAGES ===");

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

    GameObject CreatePieceImage(ChessPiece piece, Transform parent, int row, int col)
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

    // Enhanced game reset
    public void ResetGame()
    {
        // Stop any ongoing AI processing
        isProcessingAIMove = false;
        StopAllCoroutines();

        currentRevealCount = maxRevealCount;
        isWhiteTurn = true;
        moveHistory.Clear();
        moveNumber = 1;
        moveLogText.text = "Blindfold mode started! Type your moves (e.g., e4, Nf3)\n";
        moveInputField.text = "";
        moveInputField.interactable = true;
        isDifficultySet = false;

        // Show difficulty prompt again
        if (difficultyPromptObject != null)
        {
            difficultyPromptObject.SetActive(true);
        }

        // Disable input until difficulty is chosen
        moveInputField.interactable = false;

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

        // Reset Stockfish position
        if (isEngineReady)
        {
            SendCommand("ucinewgame");
        }

        // Restart coroutines
        StartCoroutine(FocusInputField());
    }

    // Enhanced piece movement validation
    bool CanPieceMoveTo(int fromRow, int fromCol, int toRow, int toCol, ChessPiece piece)
    {
        if (fromRow == toRow && fromCol == toCol) return false;
        if (toRow < 0 || toRow >= 8 || toCol < 0 || toCol >= 8) return false;

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
                if (deltaRow == 0 && deltaCol != 0)
                {
                    return IsPathClear(fromRow, fromCol, toRow, toCol);
                }
                else if (deltaCol == 0 && deltaRow != 0)
                {
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

    // Enhanced pawn move validation with en passant
    bool IsValidPawnMove(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        int direction = color == PieceColor.White ? -1 : 1;
        int deltaRow = toRow - fromRow;
        int deltaCol = Mathf.Abs(toCol - fromCol);

        // Forward move
        if (deltaCol == 0)
        {
            if (boardState[toRow, toCol] != null) return false;

            if (deltaRow == direction) return true;

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

        if (toRow > fromRow) rowStep = 1;
        else if (toRow < fromRow) rowStep = -1;

        if (toCol > fromCol) colStep = 1;
        else if (toCol < fromCol) colStep = -1;

        int currentRow = fromRow + rowStep;
        int currentCol = fromCol + colStep;

        while (currentRow != toRow || currentCol != toCol)
        {
            if (currentRow < 0 || currentRow >= 8 || currentCol < 0 || currentCol >= 8)
            {
                return false;
            }

            if (boardState[currentRow, currentCol] != null)
            {
                return false;
            }

            currentRow += rowStep;
            currentCol += colStep;
        }

        return true;
    }

    // Complete move validation with all chess rules
    bool IsValidMove(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        if (fromRow < 0 || fromRow >= 8 || fromCol < 0 || fromCol >= 8) return false;
        if (toRow < 0 || toRow >= 8 || toCol < 0 || toCol >= 8) return false;

        ChessPiece piece = boardState[fromRow, fromCol];
        if (piece == null || piece.color != color) return false;

        if (!CanPieceMoveTo(fromRow, fromCol, toRow, toCol, piece)) return false;

        if (WouldMoveLeaveKingInCheck(fromRow, fromCol, toRow, toCol, color)) return false;

        return true;
    }

    string FormatMoveNotation(string originalNotation, int fromRow, int fromCol, int toRow, int toCol)
    {
        return originalNotation;
    }

    // Fixed destroy handling to prevent the error
    void OnDestroy()
    {
        try
        {
            if (stockfishProcess != null)
            {
                if (!stockfishProcess.HasExited)
                {
                    SendCommandImmediate("quit");

                    // Give it a moment to quit gracefully
                    if (!stockfishProcess.WaitForExit(2000))
                    {
                        stockfishProcess.Kill();
                    }
                }
                stockfishProcess.Dispose();
                stockfishProcess = null;
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogWarning($"Error disposing Stockfish: {e.Message}");
        }
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

    [ContextMenu("Test Stockfish Connection")]
    public void TestStockfishConnection()
    {
        UnityEngine.Debug.Log("=== STOCKFISH CONNECTION TEST ===");
        UnityEngine.Debug.Log($"Stockfish Path: {GetStockfishPath()}");
        UnityEngine.Debug.Log($"File Exists: {File.Exists(GetStockfishPath())}");
        UnityEngine.Debug.Log($"Engine Ready: {isEngineReady}");
        UnityEngine.Debug.Log($"Current Difficulty: {currentDifficulty}");

        if (stockfishProcess != null)
        {
            try
            {
                UnityEngine.Debug.Log($"Process Running: {!stockfishProcess.HasExited}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.Log($"Error checking process status: {e.Message}");
            }
        }
        else
        {
            UnityEngine.Debug.Log("No Stockfish process");
        }
    }

    [ContextMenu("Test Stockfish Move")]
    public void TestStockfishMove()
    {
        if (isEngineReady)
        {
            GetAIMove();
        }
        else
        {
            UnityEngine.Debug.LogWarning("Stockfish not ready!");
            ShowErrorMessage("Stockfish engine not ready!");
        }
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

}