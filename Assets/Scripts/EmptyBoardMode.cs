using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class EmptyBoardMode : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI targetSquareText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI coordinateFlashText;

    [Header("Chess Board")]
    public RectTransform boardParent;
    public GameObject squarePrefab;

    [Header("Board Layout Settings")]
    public float squareSize = 92f;
    public float boardSpacing = 0f;

    [Header("Colors")]
    public Color lightSquareColor = Color.white;
    public Color darkSquareColor = new Color(0.7f, 0.7f, 0.7f);
    public Color correctColor = Color.green;
    public Color incorrectColor = Color.red;

    [Header("Game Settings")]
    public float feedbackDuration = 1f;

    [Header("Perspective Settings")]
    public bool isWhitePerspective = true;

    private Image[,] chessSquares = new Image[8, 8];
    private GridLayoutGroup gridLayout;

    private int currentTargetRow;
    private int currentTargetCol;
    private int score = 0;
    private int totalAttempts = 0;
    private int correctAttempts = 0;
    private bool gameStarted = false;

    private Coroutine flashRoutine;

    void Start()
    {
        SetupUI();
        SetupChessBoard();
        AutoResizeBoard();

        gameStarted = true;
        score = 0;
        totalAttempts = 0;
        correctAttempts = 0;

        UpdateUI();
        GenerateNewTarget();
        ResetBoardColors();
    }

    void SetupUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: 0";
    }

    void AutoResizeBoard()
    {
        if (boardParent == null) return;

        float availableWidth = boardParent.rect.width - 20f;
        float availableHeight = boardParent.rect.height - 20f;
        float availableSize = Mathf.Min(availableWidth, availableHeight);
        float calculatedSquareSize = (availableSize - (boardSpacing * 7)) / 8f;
        squareSize = Mathf.Clamp(calculatedSquareSize, 60f, 100f);

        ResizeBoard();
    }

    void SetupChessBoard()
    {
        if (boardParent == null || squarePrefab == null)
        {
            Debug.LogError("Board Parent or Square Prefab is not assigned!");
            return;
        }

        gridLayout = boardParent.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            Debug.LogError("ChessBoardPanel must have a Grid Layout Group component!");
            return;
        }

        ConfigureGridLayout();
        ClearBoard();
        CreateChessSquares();
        ResizeBoard();
    }

    void ConfigureGridLayout()
    {
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 8;
        gridLayout.spacing = new Vector2(boardSpacing, boardSpacing);
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        gridLayout.cellSize = new Vector2(squareSize, squareSize);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
    }

    void ClearBoard()
    {
        for (int i = boardParent.childCount - 1; i >= 0; i--)
        {
            Destroy(boardParent.GetChild(i).gameObject);
        }

        chessSquares = new Image[8, 8];
    }

    void CreateChessSquares()
    {
        int rowStart = isWhitePerspective ? 0 : 7;
        int rowEnd = isWhitePerspective ? 8 : -1;
        int rowStep = isWhitePerspective ? 1 : -1;

        int colStart = isWhitePerspective ? 0 : 7;
        int colEnd = isWhitePerspective ? 8 : -1;
        int colStep = isWhitePerspective ? 1 : -1;

        for (int row = rowStart; row != rowEnd; row += rowStep)
        {
            for (int col = colStart; col != colEnd; col += colStep)
            {
                GameObject square = Instantiate(squarePrefab, boardParent);
                square.name = $"Square_{row}_{col}";

                Image squareImage = square.GetComponent<Image>();
                if (squareImage == null)
                    squareImage = square.AddComponent<Image>();

                bool isLight = (row + col) % 2 == 0;
                squareImage.color = isLight ? lightSquareColor : darkSquareColor;

                chessSquares[row, col] = squareImage;

                Button squareButton = square.GetComponent<Button>();
                if (squareButton == null)
                    squareButton = square.AddComponent<Button>();

                int r = row;
                int c = col;
                squareButton.onClick.RemoveAllListeners();
                squareButton.onClick.AddListener(() => OnSquareClicked(r, c));

                LayoutElement layoutElement = square.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = square.AddComponent<LayoutElement>();
            }
        }
    }

    void ResizeBoard()
    {
        if (gridLayout == null) return;

        gridLayout.cellSize = new Vector2(squareSize, squareSize);
        gridLayout.spacing = new Vector2(boardSpacing, boardSpacing);

        float totalBoardSize = (squareSize * 8) + (boardSpacing * 7);
        if (boardParent != null)
        {
            boardParent.sizeDelta = new Vector2(totalBoardSize, totalBoardSize);
        }
    }

    void GenerateNewTarget()
    {
        if (!gameStarted) return;

        currentTargetRow = UnityEngine.Random.Range(0, 8);
        currentTargetCol = UnityEngine.Random.Range(0, 8);
        string targetSquare = GetSquareName(currentTargetRow, currentTargetCol).ToLower();

        if (targetSquareText != null)
            targetSquareText.text = $"{targetSquare}";

        if (coordinateFlashText != null)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);

            flashRoutine = StartCoroutine(FlashCoordinate(targetSquare));
        }
    }

    void OnSquareClicked(int row, int col)
    {
        if (!gameStarted) return;

        totalAttempts++;

        bool isCorrect = (row == currentTargetRow && col == currentTargetCol);
        Debug.Log($"Clicked: {GetSquareName(row, col)} - Target: {GetSquareName(currentTargetRow, currentTargetCol)} - {(isCorrect ? "CORRECT" : "WRONG")}");

        if (isCorrect)
        {
            correctAttempts++;
            score++;

            if (chessSquares[row, col] != null)
                chessSquares[row, col].color = correctColor;

            StartCoroutine(ResetSquareColor(row, col, feedbackDuration));
            UpdateUI();
            GenerateNewTarget();
        }
        else
        {
            if (chessSquares[row, col] != null)
                chessSquares[row, col].color = incorrectColor;

            StartCoroutine(ResetSquareColor(row, col, feedbackDuration));
            UpdateUI();
        }
    }

    IEnumerator ResetSquareColor(int row, int col, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (chessSquares[row, col] != null)
        {
            bool isLight = (row + col) % 2 == 0;
            chessSquares[row, col].color = isLight ? lightSquareColor : darkSquareColor;
        }
    }

    void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = $"{score}";
    }

    void ResetBoardColors()
    {
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (chessSquares[row, col] != null)
                {
                    bool isLight = (row + col) % 2 == 0;
                    chessSquares[row, col].color = isLight ? lightSquareColor : darkSquareColor;
                }
            }
        }
    }

    string GetSquareName(int row, int col)
    {
        if (!isWhitePerspective)
        {
            row = 7 - row;
            col = 7 - col;
        }

        char file = (char)('A' + col);
        int rank = 8 - row;
        return $"{file}{rank}";
    }

    public void SetSquareSize(float newSize)
    {
        squareSize = newSize;
        ResizeBoard();
    }

    public void SetBoardSpacing(float newSpacing)
    {
        boardSpacing = newSpacing;
        ResizeBoard();
    }

    public void HighlightSquare(int row, int col, Color color)
    {
        if (row >= 0 && row < 8 && col >= 0 && col < 8 && chessSquares[row, col] != null)
        {
            chessSquares[row, col].color = color;
        }
    }

    public void TogglePerspective()
    {
        isWhitePerspective = !isWhitePerspective;
        SetupChessBoard();
        GenerateNewTarget();
    }

    void OnRectTransformDimensionsChange()
    {
        if (gridLayout != null)
        {
            Invoke(nameof(AutoResizeBoard), 0.1f);
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying && gridLayout != null)
        {
            ResizeBoard();
        }
    }

    IEnumerator FlashCoordinate(string text)
    {
        if (coordinateFlashText == null)
        {
            Debug.LogWarning("coordinateFlashText is not assigned!");
            yield break;
        }

        coordinateFlashText.text = text;

        Color c = coordinateFlashText.color;
        c.a = 0.7f;
        coordinateFlashText.color = c;
        coordinateFlashText.gameObject.SetActive(true);

        yield return new WaitForSeconds(1.2f);

        c.a = 0f;
        coordinateFlashText.color = c;
        coordinateFlashText.gameObject.SetActive(false);
    }
}
