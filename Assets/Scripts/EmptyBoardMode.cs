using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class EmptyBoardMode : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI targetSquareText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI accuracyText;
    public Button startButton;
    public Button resetButton;
    public RectTransform leftPanel;

    [Header("Chess Board")]
    public RectTransform boardParent; // This is your ChessBoardPanel with Grid Layout Group
    public GameObject squarePrefab;

    [Header("Board Layout Settings")]
    public float squareSize = 92f; // 92px squares
    public float boardSpacing = 0f; // No spacing

    [Header("Colors")]
    public Color lightSquareColor = Color.white;
    public Color darkSquareColor = new Color(0.7f, 0.7f, 0.7f);
    public Color correctColor = Color.green;
    public Color incorrectColor = Color.red;

    [Header("Game Settings")]
    public float feedbackDuration = 1f;

    private Image[,] chessSquares = new Image[8, 8];
    private GridLayoutGroup gridLayout;
    private bool gameStarted = false;

    // Game state
    private int currentTargetRow;
    private int currentTargetCol;
    private int score = 0;
    private int totalAttempts = 0;
    private int correctAttempts = 0;

    void Start()
    {
        SetupUI();
        SetupChessBoard();
        SetupButtons();
        AutoResizeBoard();
    }

    void SetupUI()
    {
        // Initialize UI text
        if (targetSquareText != null)
            targetSquareText.text = "Click Start to Begin";

        if (scoreText != null)
            scoreText.text = "Score: 0";

        if (accuracyText != null)
            accuracyText.text = "Accuracy: 0%";
    }

    void AutoResizeBoard()
    {
        if (boardParent == null) return;

        // Get available space in the board parent
        float availableWidth = boardParent.rect.width - 20f; // 10px margin on each side
        float availableHeight = boardParent.rect.height - 20f; // 10px margin on each side

        // Use the smaller dimension to keep board square
        float availableSize = Mathf.Min(availableWidth, availableHeight);

        // Calculate square size (8 squares + 7 spaces between them)
        float calculatedSquareSize = (availableSize - (boardSpacing * 7)) / 8f;

        // Set limits: minimum 60px, maximum 100px
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

        // Get Grid Layout Group
        gridLayout = boardParent.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            Debug.LogError("ChessBoardPanel must have a Grid Layout Group component!");
            return;
        }

        // Configure Grid Layout Group
        ConfigureGridLayout();

        // Clear existing squares
        ClearBoard();

        // Create chess squares
        CreateChessSquares();

        // Ensure proper sizing
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
        // Clear existing squares
        for (int i = boardParent.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(boardParent.GetChild(i).gameObject);
            else
                DestroyImmediate(boardParent.GetChild(i).gameObject);
        }

        // Clear array
        chessSquares = new Image[8, 8];
    }

    void CreateChessSquares()
    {
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                // Create square
                GameObject square = Instantiate(squarePrefab, boardParent);
                square.name = $"Square_{row}_{col}";

                // Get or add Image component
                Image squareImage = square.GetComponent<Image>();
                if (squareImage == null)
                    squareImage = square.AddComponent<Image>();

                // Set square color (checkerboard pattern)
                bool isLightSquare = (row + col) % 2 == 0;
                squareImage.color = isLightSquare ? lightSquareColor : darkSquareColor;

                // Store reference
                chessSquares[row, col] = squareImage;

                // Get or add Button component
                Button squareButton = square.GetComponent<Button>();
                if (squareButton == null)
                    squareButton = square.AddComponent<Button>();

                // Add click handler
                int capturedRow = row;
                int capturedCol = col;
                squareButton.onClick.RemoveAllListeners();
                squareButton.onClick.AddListener(() => OnSquareClicked(capturedRow, capturedCol));

                // Ensure proper layout element
                LayoutElement layoutElement = square.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = square.AddComponent<LayoutElement>();
            }
        }
    }

    void ResizeBoard()
    {
        if (gridLayout == null) return;

        // Update grid cell size
        gridLayout.cellSize = new Vector2(squareSize, squareSize);
        gridLayout.spacing = new Vector2(boardSpacing, boardSpacing);

        // Calculate total board size
        float totalBoardSize = (squareSize * 8) + (boardSpacing * 7);

        // Adjust board parent size if needed
        if (boardParent != null)
        {
            boardParent.sizeDelta = new Vector2(totalBoardSize, totalBoardSize);
        }
    }

    void SetupButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartGame);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(OnResetGame);
        }
    }

    // PUBLIC FUNCTIONS FOR BUTTON CLICKS
    public void OnStartGame()
    {
        gameStarted = true;
        score = 0;
        totalAttempts = 0;
        correctAttempts = 0;

        Debug.Log("Game Started!");

        UpdateUI();
        GenerateNewTarget();
        ResetBoardColors();
    }

    public void OnResetGame()
    {
        gameStarted = false;
        score = 0;
        totalAttempts = 0;
        correctAttempts = 0;

        Debug.Log("Game Reset!");

        if (targetSquareText != null)
            targetSquareText.text = "Click Start to Begin";

        UpdateUI();
        ResetBoardColors();
    }

    void GenerateNewTarget()
    {
        if (!gameStarted) return;

        // Generate random target square
        currentTargetRow = UnityEngine.Random.Range(0, 8);
        currentTargetCol = UnityEngine.Random.Range(0, 8);

        // Update UI to show the target
        if (targetSquareText != null)
        {
            string targetSquare = GetSquareName(currentTargetRow, currentTargetCol);
            targetSquareText.text = $"Find: {targetSquare}";
        }

        Debug.Log($"New target: {GetSquareName(currentTargetRow, currentTargetCol)} (Row: {currentTargetRow}, Col: {currentTargetCol})");
    }

    void OnSquareClicked(int row, int col)
    {
        if (!gameStarted) return;

        totalAttempts++;

        bool isCorrect = (row == currentTargetRow && col == currentTargetCol);

        Debug.Log($"Clicked: {GetSquareName(row, col)} - Target: {GetSquareName(currentTargetRow, currentTargetCol)} - {(isCorrect ? "CORRECT" : "WRONG")}");

        if (isCorrect)
        {
            // Correct answer!
            correctAttempts++;
            score += 10;

            // Show green feedback
            if (chessSquares[row, col] != null)
            {
                chessSquares[row, col].color = correctColor;
            }

            // Update UI
            UpdateUI();

            // Generate new target after delay
            StartCoroutine(GenerateNewTargetAfterDelay());
        }
        else
        {
            // Wrong answer!
            // Show red feedback
            if (chessSquares[row, col] != null)
            {
                chessSquares[row, col].color = incorrectColor;
                // Reset color after delay
                StartCoroutine(ResetSquareColor(row, col, feedbackDuration));
            }

            // Update UI (accuracy will change)
            UpdateUI();
        }
    }

    IEnumerator GenerateNewTargetAfterDelay()
    {
        yield return new WaitForSeconds(feedbackDuration);

        // Reset board colors
        ResetBoardColors();

        // Generate new target
        GenerateNewTarget();
    }

    IEnumerator ResetSquareColor(int row, int col, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (chessSquares[row, col] != null)
        {
            bool isLightSquare = (row + col) % 2 == 0;
            chessSquares[row, col].color = isLightSquare ? lightSquareColor : darkSquareColor;
        }
    }

    void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";

        if (accuracyText != null)
        {
            float accuracy = totalAttempts > 0 ? (correctAttempts * 100f / totalAttempts) : 100f;
            accuracyText.text = $"Accuracy: {accuracy:F1}%";
        }
    }

    void ResetBoardColors()
    {
        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 8; col++)
            {
                if (chessSquares[row, col] != null)
                {
                    bool isLightSquare = (row + col) % 2 == 0;
                    chessSquares[row, col].color = isLightSquare ? lightSquareColor : darkSquareColor;
                }
            }
        }
    }

    string GetSquareName(int row, int col)
    {
        char file = (char)('A' + col);
        int rank = 8 - row;
        return $"{file}{rank}";
    }

    // Public methods for external control
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

    // Handle screen resize
    void OnRectTransformDimensionsChange()
    {
        if (gridLayout != null)
        {
            Invoke(nameof(AutoResizeBoard), 0.1f);
        }
    }

    // Handle inspector changes in editor
    void OnValidate()
    {
        if (Application.isPlaying && gridLayout != null)
        {
            ResizeBoard();
        }
    }
}