using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BlindfoldModeManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField moveInputField;    // Input where player types moves
    public TMP_Text moveLogText;             // Text showing all moves played
    public GameObject chessBoardObject;      // The chessboard GameObject (to hide/show)
    public Button revealBoardButton;         // Button to reveal board temporarily
    
    public enum PieceType { None, Pawn, Knight, Bishop, Rook, Queen, King }
    public enum PieceColor { White, Black }

    public class ChessPiece
    {
        public PieceType type;
        public PieceColor color;
        public ChessPiece(PieceType type, PieceColor color)
        {
        this.type = type;
        this.color = color;
        }
    }
    [Header("Chess Piece Prefabs")]
        public GameObject whitePawnPrefab;
        public GameObject whiteKnightPrefab;
        public GameObject whiteBishopPrefab;
        public GameObject whiteRookPrefab;
        public GameObject whiteQueenPrefab;
        public GameObject whiteKingPrefab;

        public GameObject blackPawnPrefab;
        public GameObject blackKnightPrefab;
        public GameObject blackBishopPrefab;
        public GameObject blackRookPrefab;
        public GameObject blackQueenPrefab;
        public GameObject blackKingPrefab;

    [Header("Settings")]
    public int maxRevealCount = 3;           

    private int currentRevealCount;

    private ChessPiece[,] boardState = new ChessPiece[8, 8];


    private void SpawnPiecesFromBoardState()
{
    // Loop over all board squares
    for (int row = 0; row < 8; row++)
    {
        for (int col = 0; col < 8; col++)
        {
            // Find the square GameObject by name inside chessBoardObject
            string squareName = $"Square_{row}_{col}";
            Transform squareTransform = chessBoardObject.transform.Find(squareName);

            if (squareTransform == null)
            {
                Debug.LogWarning($"Square GameObject {squareName} not found!");
                continue;
            }

            // Clear previous piece objects inside the square (if any)
            for (int i = squareTransform.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(squareTransform.GetChild(i).gameObject);
            }

            // Get the piece on this square from boardState
            ChessPiece piece = boardState[row, col];
            if (piece == null || piece.type == PieceType.None)
                continue;

            // Select the right prefab to instantiate
            GameObject prefabToSpawn = GetPrefabForPiece(piece);

            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"Prefab for piece {piece.color} {piece.type} not assigned!");
                continue;
            }

            // Instantiate the piece prefab as a child of the square
            GameObject pieceObj = Instantiate(prefabToSpawn, squareTransform);

            // Reset local position and scale so it fits nicely inside the square
            pieceObj.transform.localPosition = Vector3.zero;
            pieceObj.transform.localRotation = Quaternion.identity;
            pieceObj.transform.localScale = new Vector3(50f, 50f, 50f);
        }
    }
}

private GameObject GetPrefabForPiece(ChessPiece piece)
{
    if (piece.color == PieceColor.White)
    {
        switch (piece.type)
        {
            case PieceType.Pawn: return whitePawnPrefab;
            case PieceType.Knight: return whiteKnightPrefab;
            case PieceType.Bishop: return whiteBishopPrefab;
            case PieceType.Rook: return whiteRookPrefab;
            case PieceType.Queen: return whiteQueenPrefab;
            case PieceType.King: return whiteKingPrefab;
            default: return null;
        }
    }
    else // Black
    {
        switch (piece.type)
        {
            case PieceType.Pawn: return blackPawnPrefab;
            case PieceType.Knight: return blackKnightPrefab;
            case PieceType.Bishop: return blackBishopPrefab;
            case PieceType.Rook: return blackRookPrefab;
            case PieceType.Queen: return blackQueenPrefab;
            case PieceType.King: return blackKingPrefab;
            default: return null;
        }
    }
}

    void SetupStartingPosition()
{
    // Pawns
    for (int col = 0; col < 8; col++)
    {
        boardState[6, col] = new ChessPiece(PieceType.Pawn, PieceColor.White); // White pawns on rank 2 (row 6)
        boardState[1, col] = new ChessPiece(PieceType.Pawn, PieceColor.Black); // Black pawns on rank 7 (row 1)
    }

    // White pieces (row 7)
    boardState[7, 0] = new ChessPiece(PieceType.Rook, PieceColor.White);
    boardState[7, 1] = new ChessPiece(PieceType.Knight, PieceColor.White);
    boardState[7, 2] = new ChessPiece(PieceType.Bishop, PieceColor.White);
    boardState[7, 3] = new ChessPiece(PieceType.Queen, PieceColor.White);
    boardState[7, 4] = new ChessPiece(PieceType.King, PieceColor.White);
    boardState[7, 5] = new ChessPiece(PieceType.Bishop, PieceColor.White);
    boardState[7, 6] = new ChessPiece(PieceType.Knight, PieceColor.White);
    boardState[7, 7] = new ChessPiece(PieceType.Rook, PieceColor.White);

    // Black pieces (row 0)
    boardState[0, 0] = new ChessPiece(PieceType.Rook, PieceColor.Black);
    boardState[0, 1] = new ChessPiece(PieceType.Knight, PieceColor.Black);
    boardState[0, 2] = new ChessPiece(PieceType.Bishop, PieceColor.Black);
    boardState[0, 3] = new ChessPiece(PieceType.Queen, PieceColor.Black);
    boardState[0, 4] = new ChessPiece(PieceType.King, PieceColor.Black);
    boardState[0, 5] = new ChessPiece(PieceType.Bishop, PieceColor.Black);
    boardState[0, 6] = new ChessPiece(PieceType.Knight, PieceColor.Black);
    boardState[0, 7] = new ChessPiece(PieceType.Rook, PieceColor.Black);

    // Empty squares

    for (int row = 2; row <= 5; row++)
    {
        for (int col = 0; col < 8; col++)
        {
            boardState[row, col] = null;  // No piece
        }
    }
}
    

    void Start()
    {
        currentRevealCount = maxRevealCount;
        chessBoardObject.SetActive(false);    // Hide board at start
        revealBoardButton.onClick.AddListener(OnRevealBoardClicked);

        moveLogText.text = "";                 // Clear moves log
        moveInputField.text = "";
        moveInputField.onSubmit.AddListener(OnPlayerMoveSubmitted);

        SetupStartingPosition();     
        SpawnPiecesFromBoardState();

    }

    void OnPlayerMoveSubmitted(string playerMove)
    {
        if (string.IsNullOrWhiteSpace(playerMove)) return;

        // Validate move format here (optional)

        // Add player move to log
        moveLogText.text += $"White: {playerMove}\n";

        moveInputField.text = "";  // Clear input field

        // Here you would process the move on your chess logic
        // Then calculate AI move and show it:
        string aiMove = GetAIMove(); // Placeholder function

        moveLogText.text += $"Black: {aiMove}\n";

        // Optionally scroll or update UI to show latest moves
    }

    string GetAIMove()
    {
        // TODO: Replace this with your actual AI move generator
        // For now, just return a dummy move:
        return "e5";
    }

    void OnRevealBoardClicked()
    {
        if (currentRevealCount > 0)
        {
            currentRevealCount--;
            StartCoroutine(RevealBoardTemporarily());
        }
        else
        {
            // Optionally disable button or show message "No reveals left"
            revealBoardButton.interactable = false;
        }
    }

    IEnumerator RevealBoardTemporarily()
    {
        chessBoardObject.SetActive(true);

        // Wait for 3 seconds (or your desired time)
        yield return new WaitForSeconds(3f);

        chessBoardObject.SetActive(false);
    }
}
