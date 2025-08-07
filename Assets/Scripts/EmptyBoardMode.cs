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
    public Color darkSquareColor  = new Color(0.7f, 0.7f, 0.7f);
    public Color correctColor     = Color.green;
    public Color incorrectColor   = Color.red;

    [Header("Game Settings")]
    public float feedbackDuration = 1f;

    [Header("Perspective Settings")]
    public bool isWhitePerspective = true;

    private Image[,] chessSquares = new Image[8, 8];
    private GridLayoutGroup gridLayout;

    private int score, totalAttempts, correctAttempts;
    private bool gameStarted = false;

    // logical target: file 0–7 for a–h, rank 1–8
    private int targetFile;
    private int targetRank;

    private Coroutine flashRoutine;

    void Start()
    {
        SetupUI();
        SetupChessBoard();
        AutoResizeBoard();

        score = totalAttempts = correctAttempts = 0;
        gameStarted = true;

        UpdateUI();
        GenerateNewTarget();
        ResetBoardColors();
    }

    void SetupUI()
    {
        if (scoreText != null)
            scoreText.text = "Score: 0";
    }

    void SetupChessBoard()
    {
        if (boardParent == null || squarePrefab == null)
        {
            Debug.LogError("Board Parent or Square Prefab not assigned!");
            return;
        }

        // get or add GridLayoutGroup
        gridLayout = boardParent.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
            gridLayout = boardParent.gameObject.AddComponent<GridLayoutGroup>();

        ConfigureGridLayout();
        ClearBoard();
        CreateChessSquares();
        ResizeBoard();
    }

    void ConfigureGridLayout()
    {
        gridLayout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 8;
        gridLayout.spacing         = new Vector2(boardSpacing, boardSpacing);
        gridLayout.childAlignment  = TextAnchor.MiddleCenter;
        gridLayout.cellSize        = new Vector2(squareSize, squareSize);
        gridLayout.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis       = GridLayoutGroup.Axis.Horizontal;
    }

    void ClearBoard()
    {
        for (int i = boardParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(boardParent.GetChild(i).gameObject);
    }

    void CreateChessSquares()
    {
        int rowStart = isWhitePerspective ? 0 : 7;
        int rowEnd   = isWhitePerspective ? 8 : -1;
        int rowStep  = isWhitePerspective ? 1 : -1;

        int colStart = isWhitePerspective ? 0 : 7;
        int colEnd   = isWhitePerspective ? 8 : -1;
        int colStep  = isWhitePerspective ? 1 : -1;

        for (int row = rowStart; row != rowEnd; row += rowStep)
        {
            for (int col = colStart; col != colEnd; col += colStep)
            {
                GameObject sq = Instantiate(squarePrefab, boardParent);
                sq.name = "Square_" + row + "_" + col;

                Image img = sq.GetComponent<Image>() ?? sq.AddComponent<Image>();
                bool isLight = ((row + col) % 2 == 0);
                img.color = isLight ? lightSquareColor : darkSquareColor;
                chessSquares[row, col] = img;

                Button btn = sq.GetComponent<Button>() ?? sq.AddComponent<Button>();
                int r = row, c = col;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnSquareClicked(r, c));

                if (sq.GetComponent<LayoutElement>() == null)
                    sq.AddComponent<LayoutElement>();
            }
        }
    }

    void AutoResizeBoard()
    {
        if (boardParent == null) return;
        float availW = boardParent.rect.width  - 20f;
        float availH = boardParent.rect.height - 20f;
        float avail  = Mathf.Min(availW, availH);
        squareSize   = Mathf.Clamp((avail - boardSpacing * 7f) / 8f, 60f, 100f);
        ResizeBoard();
    }

    void ResizeBoard()
    {
        if (gridLayout == null) return;
        gridLayout.cellSize = new Vector2(squareSize, squareSize);
        gridLayout.spacing  = new Vector2(boardSpacing, boardSpacing);
        float total = squareSize * 8f + boardSpacing * 7f;
        boardParent.sizeDelta = new Vector2(total, total);
    }

    void GenerateNewTarget()
    {
        targetFile = Random.Range(0, 8);
        targetRank = Random.Range(1, 9);

        string coord = ((char)('a' + targetFile)).ToString() + targetRank;
        if (targetSquareText != null)
            targetSquareText.text = coord;

        if (coordinateFlashText != null)
        {
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashCoordinate(coord));
        }
    }

    void OnSquareClicked(int row, int col)
    {
        if (!gameStarted) return;
        totalAttempts++;

        // simple mapping: file = col, rank = 8 - row
        int file = col;
        int rank = 8 - row;

        bool correct = (file == targetFile && rank == targetRank);
        var img = chessSquares[row, col];

        if (correct)
        {
            correctAttempts++;
            score++;
            img.color = correctColor;
            StartCoroutine(ResetSquareColor(row, col, feedbackDuration));
            UpdateUI();
            GenerateNewTarget();
        }
        else
        {
            img.color = incorrectColor;
            StartCoroutine(ResetSquareColor(row, col, feedbackDuration));
            UpdateUI();
        }
    }

    IEnumerator ResetSquareColor(int row, int col, float delay)
    {
        yield return new WaitForSeconds(delay);
        var img = chessSquares[row, col];
        bool isLight = ((row + col) % 2 == 0);
        img.color = isLight ? lightSquareColor : darkSquareColor;
    }

    void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
    }

    void ResetBoardColors()
    {
        for (int r = 0; r < 8; r++)
            for (int c = 0; c < 8; c++)
            {
                var img = chessSquares[r, c];
                bool isLight = ((r + c) % 2 == 0);
                img.color = isLight ? lightSquareColor : darkSquareColor;
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
            Invoke("AutoResizeBoard", 0.1f);
    }

    void OnValidate()
    {
        if (Application.isPlaying && gridLayout != null)
            ResizeBoard();
    }

    IEnumerator FlashCoordinate(string text)
    {
        if (coordinateFlashText == null)
            yield break;

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
