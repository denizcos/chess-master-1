using UnityEngine;
using System.Collections.Generic;
using System.Text;

public class ChessRules : MonoBehaviour
{
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

    // Game State - HIDDEN from inspector
    private ChessPiece[,] boardState = new ChessPiece[8, 8];
    private bool isWhiteTurn = true;
    private int moveNumber = 1;

    // Castling Flags - HIDDEN from inspector
    private bool whiteKingMoved = false;
    private bool blackKingMoved = false;
    private bool whiteKingsideRookMoved = false;
    private bool whiteQueensideRookMoved = false;
    private bool blackKingsideRookMoved = false;
    private bool blackQueensideRookMoved = false;

    // En Passant - HIDDEN from inspector
    private Vector2Int enPassantTarget = new Vector2Int(-1, -1);
    private Vector2Int lastPawnDoubleMove = new Vector2Int(-1, -1);

    // Position History - HIDDEN from inspector
    private List<string> positionHistory = new List<string>();
    private Dictionary<string, int> positionCounts = new Dictionary<string, int>();

    // Public accessors for other scripts
    public bool IsWhiteTurn => isWhiteTurn;
    public int MoveNumber => moveNumber;

    void Start()
    {
        SetupStartingPosition();
    }

    // BOARD SETUP
    public void SetupStartingPosition()
    {
        // Clear board state
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                boardState[row, col] = null;
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
        isWhiteTurn = true;
        moveNumber = 1;

        // Clear position history
        positionHistory.Clear();
        positionCounts.Clear();
        UpdatePositionHistory();

        Debug.Log("Starting position setup complete!");
    }

    // MOVE VALIDATION
    public bool IsValidMove(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        // Bounds check
        if (fromRow < 0 || fromRow >= 8 || fromCol < 0 || fromCol >= 8) return false;
        if (toRow < 0 || toRow >= 8 || toCol < 0 || toCol >= 8) return false;

        // Check if there's a piece at the from position
        ChessPiece piece = boardState[fromRow, fromCol];
        if (piece == null || piece.color != color) return false;

        // Check if the piece can move to the destination
        if (!CanPieceMoveTo(fromRow, fromCol, toRow, toCol, piece)) return false;

        // Check if this move would leave the king in check
        if (WouldMoveLeaveKingInCheck(fromRow, fromCol, toRow, toCol, color)) return false;

        return true;
    }

    public bool CanPieceMoveTo(int fromRow, int fromCol, int toRow, int toCol, ChessPiece piece)
    {
        // Can't move to same square
        if (fromRow == toRow && fromCol == toCol) return false;

        // Can't move outside board
        if (toRow < 0 || toRow >= 8 || toCol < 0 || toCol >= 8) return false;

        // Can't capture own piece
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
                return (deltaRow == 0 || deltaCol == 0) && IsPathClear(fromRow, fromCol, toRow, toCol);

            case PieceType.Queen:
                return (Mathf.Abs(deltaRow) == Mathf.Abs(deltaCol) || deltaRow == 0 || deltaCol == 0) &&
                       IsPathClear(fromRow, fromCol, toRow, toCol);

            case PieceType.King:
                return Mathf.Abs(deltaRow) <= 1 && Mathf.Abs(deltaCol) <= 1;
        }

        return false;
    }

    // FIXED PAWN MOVE VALIDATION - No jumping over pieces
    bool IsValidPawnMove(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        int direction = color == PieceColor.White ? -1 : 1;
        int deltaRow = toRow - fromRow;
        int deltaCol = Mathf.Abs(toCol - fromCol);

        // Forward move
        if (deltaCol == 0)
        {
            // Can't move forward if square is occupied
            if (boardState[toRow, toCol] != null) return false;

            // Single step forward
            if (deltaRow == direction) return true;

            // Double step from starting position
            if (deltaRow == 2 * direction)
            {
                int startingRank = color == PieceColor.White ? 6 : 1;
                if (fromRow == startingRank)
                {
                    // FIXED: Check if the square in front is also clear (no jumping)
                    int middleRow = fromRow + direction;
                    if (boardState[middleRow, fromCol] == null)
                    {
                        return true;
                    }
                }
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

    public bool IsPathClear(int fromRow, int fromCol, int toRow, int toCol)
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

    // MOVE EXECUTION
    public void ExecuteMove(int fromRow, int fromCol, int toRow, int toCol)
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
        }

        // Update piece position
        piece.hasMoved = true;
        boardState[toRow, toCol] = piece;
        boardState[fromRow, fromCol] = null;

        // Update castling flags
        UpdateCastlingFlags(piece, fromRow, fromCol);

        // Handle pawn promotion (auto-promote to Queen)
        if (piece.type == PieceType.Pawn && (toRow == 0 || toRow == 7))
        {
            boardState[toRow, toCol] = new ChessPiece(PieceType.Queen, piece.color) { hasMoved = true };
        }

        // Update en passant target
        UpdateEnPassantTarget(piece, fromRow, fromCol, toRow, toCol);

        UpdatePositionHistory();
    }

    void UpdateCastlingFlags(ChessPiece piece, int fromRow, int fromCol)
    {
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
    }

    void UpdateEnPassantTarget(ChessPiece piece, int fromRow, int fromCol, int toRow, int toCol)
    {
        enPassantTarget = new Vector2Int(-1, -1);
        if (piece.type == PieceType.Pawn && Mathf.Abs(toRow - fromRow) == 2)
        {
            enPassantTarget = new Vector2Int((fromRow + toRow) / 2, fromCol);
            lastPawnDoubleMove = new Vector2Int(toRow, toCol);
        }
    }

    // CASTLING
    public bool CanCastle(PieceColor color, bool kingside)
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

        // Can't castle while in check
        if (IsInCheck(color)) return false;

        int kingCol = 4;
        int rookCol = kingside ? 7 : 0;
        int direction = kingside ? 1 : -1;

        // Check if squares between king and rook are empty and not attacked
        for (int col = kingCol + direction; col != rookCol; col += direction)
        {
            if (boardState[row, col] != null) return false;
            if (IsSquareAttacked(row, col, color == PieceColor.White ? PieceColor.Black : PieceColor.White))
                return false;
        }

        // Check if king's destination square is not attacked
        int finalKingCol = kingside ? 6 : 2;
        if (IsSquareAttacked(row, finalKingCol, color == PieceColor.White ? PieceColor.Black : PieceColor.White))
            return false;

        return true;
    }

    public void ExecuteCastling(PieceColor color, bool kingside)
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

        UpdatePositionHistory();
    }

    // CHECK AND CHECKMATE
    public bool IsInCheck(PieceColor color)
    {
        Vector2Int kingPos = FindKing(color);
        if (kingPos.x == -1) return false;

        return IsSquareAttacked(kingPos.x, kingPos.y, color == PieceColor.White ? PieceColor.Black : PieceColor.White);
    }

    public Vector2Int FindKing(PieceColor color)
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

    public bool IsSquareAttacked(int row, int col, PieceColor attackingColor)
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

    bool WouldMoveLeaveKingInCheck(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
    {
        ChessPiece movingPiece = boardState[fromRow, fromCol];
        ChessPiece capturedPiece = boardState[toRow, toCol];

        // Temporarily make the move
        boardState[toRow, toCol] = movingPiece;
        boardState[fromRow, fromCol] = null;

        bool kingInCheck = IsInCheck(color);

        // Undo the move
        boardState[fromRow, fromCol] = movingPiece;
        boardState[toRow, toCol] = capturedPiece;

        return kingInCheck;
    }

    // GAME STATE CHECKING
    public bool IsCheckmate(PieceColor color)
    {
        if (!IsInCheck(color)) return false;
        return !HasLegalMoves(color);
    }

    public bool IsStalemate(PieceColor color)
    {
        if (IsInCheck(color)) return false;
        return !HasLegalMoves(color);
    }

    public bool HasLegalMoves(PieceColor color)
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

    // DRAW CONDITIONS
    public bool IsThreefoldRepetition()
    {
        string currentPosition = GetCurrentFEN().Split(' ')[0]; // Just the board position part

        if (positionCounts.ContainsKey(currentPosition))
        {
            return positionCounts[currentPosition] >= 3;
        }
        return false;
    }

    public bool IsInsufficientMaterial()
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

    // FEN NOTATION
    public string GetCurrentFEN()
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

    // POSITION HISTORY
    void UpdatePositionHistory()
    {
        string position = GetCurrentFEN().Split(' ')[0]; // Just board position
        positionHistory.Add(position);

        if (positionCounts.ContainsKey(position))
            positionCounts[position]++;
        else
            positionCounts[position] = 1;
    }

    // UTILITY METHODS
    public void NextTurn()
    {
        isWhiteTurn = !isWhiteTurn;
        if (isWhiteTurn)
        {
            moveNumber++;
        }
    }

    public void ResetGame()
    {
        SetupStartingPosition();
    }

    // Get piece at position
    public ChessPiece GetPiece(int row, int col)
    {
        if (row < 0 || row >= 8 || col < 0 || col >= 8) return null;
        return boardState[row, col];
    }

    // Check if square is empty
    public bool IsSquareEmpty(int row, int col)
    {
        return GetPiece(row, col) == null;
    }

    // Get current player color
    public PieceColor GetCurrentPlayerColor()
    {
        return isWhiteTurn ? PieceColor.White : PieceColor.Black;
    }

    // ======================================================
    // === Notation & Input Parsing (UCI + SAN) =============
    // ======================================================

    // Convert 0-based board coords (row 0 = rank 8) to algebraic like "e4"
    public static string SquareToString(int row, int col)
    {
        char file = (char)('a' + col);
        int rank = 8 - row;
        return $"{file}{rank}";
    }

    // Convert algebraic like "e4" to 0-based (row, col)
    public static Vector2Int StringToSquare(string sq)
    {
        if (string.IsNullOrEmpty(sq) || sq.Length != 2) return new Vector2Int(-1, -1);
        int col = sq[0] - 'a';
        if (col < 0 || col > 7) return new Vector2Int(-1, -1);
        int rank;
        if (!int.TryParse(sq[1].ToString(), out rank)) return new Vector2Int(-1, -1);
        if (rank < 1 || rank > 8) return new Vector2Int(-1, -1);
        int row = 8 - rank;
        return new Vector2Int(row, col);
    }

    // UCI: e2e4, e7e8q (promotion char is ignored since we auto-queen)
    public bool TryParseUCIMove(string input, out (int fromRow,int fromCol,int toRow,int toCol) move)
    {
        move = (0,0,0,0);
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim().ToLower();
        if (input.Length < 4) return false;
        Vector2Int from = StringToSquare(input.Substring(0,2));
        Vector2Int to = StringToSquare(input.Substring(2,2));
        if (from.x < 0 || to.x < 0) return false;
        move = (from.x, from.y, to.x, to.y);
        return true;
    }

    // Try to parse SAN like e4, Nf3, exd5, O-O, O-O-O, e8=Q, exd8=Q
    // Strategy: enumerate all legal moves and compare against generated SAN
    public bool TryParseSANMove(string input, PieceColor sideToMove, out (int fromRow,int fromCol,int toRow,int toCol) move)
    {
        move = (0,0,0,0);
        if (string.IsNullOrWhiteSpace(input)) return false;
        input = input.Trim();
        // normalize zero vs letter O, remove check/mate marks, spaces
        input = input.Replace("0-0-0", "O-O-O").Replace("0-0", "O-O");
        input = input.Replace("+","" ).Replace("#","" ).Replace(" ","" );

        // Quick handle castle strings
        if (input == "O-O" || input == "o-o")
        {
            int row = sideToMove == PieceColor.White ? 7 : 0;
            var cand = (row,4,row,6);
            if (IsValidMove(cand.Item1,cand.Item2,cand.Item3,cand.Item4, sideToMove)) { move = cand; return true; }
            return false;
        }
        if (input == "O-O-O" || input == "o-o-o")
        {
            int row = sideToMove == PieceColor.White ? 7 : 0;
            var cand = (row,4,row,2);
            if (IsValidMove(cand.Item1,cand.Item2,cand.Item3,cand.Item4, sideToMove)) { move = cand; return true; }
            return false;
        }

        // Brute-force: test all legal moves and compare SAN
        for (int fr=0; fr<8; fr++)
        {
            for (int fc=0; fc<8; fc++)
            {
                ChessPiece p = boardState[fr,fc];
                if (p == null || p.color != sideToMove) continue;

                for (int tr=0; tr<8; tr++)
                {
                    for (int tc=0; tc<8; tc++)
                    {
                        if (!IsValidMove(fr,fc,tr,tc, sideToMove)) continue;
                        string san = MoveToSAN(fr,fc,tr,tc,p,false);
                        if (string.Equals(san, input, System.StringComparison.OrdinalIgnoreCase))
                        {
                            move = (fr,fc,tr,tc);
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    // Build SAN for a given (already legal) move.
    // - Handles: piece letters, captures (incl. en passant), promotions (=Q only), castling
    // - Adds '+' / '#' suffixes when the move gives check or checkmate (approx).
    // - Does NOT implement disambiguation (e.g., Nbd2) – can be added later if needed.
    public string MoveToSAN(int fromRow,int fromCol,int toRow,int toCol, ChessPiece piece, bool includeSuffix=true)
    {
        // Castling
        if (piece.type == PieceType.King && fromCol == 4 && (toCol == 6 || toCol == 2))
        {
            string castle = (toCol == 6) ? "O-O" : "O-O-O";
            if (!includeSuffix) return castle;
            SuffixForCheckMate(fromRow,fromCol,toRow,toCol,piece, ref castle);
            return castle;
        }

        bool isCapture = false;
        // Detect en passant capture case
        if (piece.type == PieceType.Pawn && boardState[toRow,toCol] == null && fromCol != toCol)
        {
            if (enPassantTarget.x == toRow && enPassantTarget.y == toCol)
                isCapture = true;
        }
        else
        {
            isCapture = boardState[toRow, toCol] != null;
        }

        string sanCore = "";
        switch (piece.type)
        {
            case PieceType.Pawn:
                if (isCapture)
                    sanCore = $"{(char)('a'+fromCol)}x{SquareToString(toRow,toCol)}";
                else
                    sanCore = SquareToString(toRow,toCol);
                // Promotion (auto-queen)
                if ((toRow == 0 || toRow == 7))
                    sanCore += "=Q";
                break;

            case PieceType.Knight: sanCore = "N" + SquareToString(toRow,toCol); break;
            case PieceType.Bishop: sanCore = "B" + SquareToString(toRow,toCol); break;
            case PieceType.Rook:   sanCore = "R" + SquareToString(toRow,toCol); break;
            case PieceType.Queen:  sanCore = "Q" + SquareToString(toRow,toCol); break;
            case PieceType.King:   sanCore = "K" + SquareToString(toRow,toCol); break;
        }

        if (piece.type != PieceType.Pawn && isCapture)
        {
            // insert 'x' before destination part (last two chars are square)
            if (sanCore.Length >= 3)
                sanCore = sanCore.Insert(sanCore.Length - 2, "x");
            else
                sanCore += "x";
        }

        if (!includeSuffix) return sanCore;

        SuffixForCheckMate(fromRow,fromCol,toRow,toCol,piece, ref sanCore);
        return sanCore;
    }

    private PieceColor OpponentColor(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    private void SuffixForCheckMate(int fromRow,int fromCol,int toRow,int toCol, ChessPiece piece, ref string san)
    {
        PieceColor opponent = OpponentColor(piece.color);

        // Save state to undo
        ChessPiece captured = boardState[toRow,toCol];
        ChessPiece moving = boardState[fromRow,fromCol];
        Vector2Int prevEP = enPassantTarget;

        // En passant removal if needed
        ChessPiece epCaptured = null;
        int epCapturedRow = -1;
        bool isEnPassant = false;
        if (piece.type == PieceType.Pawn && captured == null && fromCol != toCol && enPassantTarget.x == toRow && enPassantTarget.y == toCol)
        {
            isEnPassant = true;
            epCapturedRow = (piece.color == PieceColor.White) ? toRow + 1 : toRow - 1;
            epCaptured = boardState[epCapturedRow, toCol];
            boardState[epCapturedRow, toCol] = null;
        }

        // Make the move on the board (temporary)
        boardState[toRow,toCol] = moving;
        boardState[fromRow,fromCol] = null;

        // Handle promotion (just to improve check detection)
        bool promoted = false;
        PieceType originalType = moving.type;
        if (moving.type == PieceType.Pawn && (toRow == 0 || toRow == 7))
        {
            moving.type = PieceType.Queen;
            promoted = true;
        }

        // Update EP target temporarily if it was a double pawn push
        enPassantTarget = new Vector2Int(-1,-1);
        if (originalType == PieceType.Pawn && System.Math.Abs(toRow - fromRow) == 2)
        {
            enPassantTarget = new Vector2Int((fromRow + toRow)/2, fromCol);
        }

        // Evaluate check / mate
        bool givesCheck = IsInCheck(opponent);
        bool isMate = false;
        if (givesCheck)
        {
            // Rough mate test: if opponent has no legal moves and is in check
            isMate = !HasLegalMoves(opponent);
        }

        // Undo
        if (promoted) moving.type = originalType;
        boardState[fromRow,fromCol] = moving;
        boardState[toRow,toCol] = captured;
        if (isEnPassant && epCaptured != null)
            boardState[epCapturedRow, toCol] = epCaptured;
        enPassantTarget = prevEP;

        if (isMate) san += "#";
        else if (givesCheck) san += "+";
    }
}
