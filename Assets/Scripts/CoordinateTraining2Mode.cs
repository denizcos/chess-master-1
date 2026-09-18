using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoordinateTraining2Mode : MonoBehaviour
{
    public RectTransform boardParent;
    public GameObject squarePrefab;
    public TMP_InputField answerInput;
    public TMP_Text scoreText;
    public TMP_Text instructionText;
    public Button perspectiveButton;
    public Sprite whitePerspectiveSprite;
    public Sprite blackPerspectiveSprite;
    public Color lightSquareColor = new Color(0.94f, 0.85f, 0.71f);
    public Color darkSquareColor = new Color(0.71f, 0.53f, 0.39f);
    public Color highlightColor = new Color(1f, 0.85f, 0.2f);
    public float blinkInterval = 0.55f;
    public float feedbackDuration = 0.45f;
    public bool isWhitePerspective = true;

    private readonly Image[] squares = new Image[64];
    private GridLayoutGroup grid;
    private int target = -1;
    private int score;
    private bool ready;
    private bool showingFeedback;
    private Coroutine round;
    private const string Prompt = "Type the flashing square + Enter";

    private void Awake() => answerInput.onSubmit.AddListener(SubmitAnswer);
    private void OnEnable() => StartCoroutine(BeginSession());

    private IEnumerator BeginSession()
    {
        // The menu enables the panel after its required objects.
        yield return null;
        if (grid == null) BuildBoard();
        score = 0;
        scoreText.text = "0";
        ready = true;
        UpdatePerspective();
        NextTarget();
        FocusInput();
    }

    private void BuildBoard()
    {
        grid = boardParent.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 8;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.spacing = Vector2.zero;
        for (int i = 0; i < squares.Length; i++)
        {
            var square = Instantiate(squarePrefab, boardParent);
            square.name = Coordinate(i);
            var button = square.GetComponent<Button>();
            if (button != null) button.enabled = false;
            squares[i] = square.GetComponent<Image>();
            squares[i].raycastTarget = false;
        }
        ResizeBoard();
    }

    private void LateUpdate()
    {
        if (!ready) return;
        ResizeBoard();
        if (SettingsOpen) return;
        if (!answerInput.isFocused) FocusInput();
    }

    private void ResizeBoard()
    {
        var parent = (RectTransform)boardParent.parent;
        float side = Mathf.Max(80f, Mathf.Min(800f, parent.rect.width - 120f, parent.rect.height - 280f));
        boardParent.sizeDelta = new Vector2(side, side);
        grid.cellSize = Vector2.one * (side / 8f);
        ((RectTransform)perspectiveButton.transform).anchoredPosition = new Vector2(side / 2f + 39f, side / 2f - 24f);
    }

    public static string Coordinate(int index) => ((char)('a' + index % 8)).ToString() + (8 - index / 8);

    public static bool TryParseCoordinate(string answer, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(answer)) return false;
        answer = answer.Trim().ToLowerInvariant();
        if (answer.Length != 2 || answer[0] < 'a' || answer[0] > 'h' || answer[1] < '1' || answer[1] > '8') return false;
        index = (8 - (answer[1] - '0')) * 8 + answer[0] - 'a';
        return true;
    }

    private void SubmitAnswer(string answer)
    {
        if (!ready || showingFeedback || SettingsOpen) return;
        UIButtonHoverSound.Instance?.PlayEnter();
        if (string.IsNullOrWhiteSpace(answer)) { FocusInput(); return; }
        bool correct = TryParseCoordinate(answer, out int index) && index == target;
        if (round != null) StopCoroutine(round);
        ClearInput();
        round = StartCoroutine(ShowFeedback(correct));
        FocusInput();
    }

    private IEnumerator ShowFeedback(bool correct)
    {
        showingFeedback = true;
        squares[target].color = correct ? Color.green : Color.red;
        instructionText.text = correct ? "Correct!" : "Try again";
        if (correct)
        {
            scoreText.text = (++score).ToString();
            UIButtonHoverSound.Instance?.PlayCorrect();
        }
        else UIButtonHoverSound.Instance?.PlayWrong();
        yield return new WaitForSeconds(feedbackDuration);
        showingFeedback = false;
        if (correct) NextTarget();
        else
        {
            instructionText.text = Prompt;
            round = StartCoroutine(BlinkTarget());
        }
    }

    private void NextTarget()
    {
        ResetColors();
        // Uniformly choose a different square so every round is visibly new.
        int next = Random.Range(0, target < 0 ? 64 : 63);
        if (target >= 0 && next >= target) next++;
        target = next;
        instructionText.text = Prompt;
        round = StartCoroutine(BlinkTarget());
    }

    private IEnumerator BlinkTarget()
    {
        while (true)
        {
            squares[target].color = highlightColor;
            yield return new WaitForSeconds(blinkInterval);
            squares[target].color = BaseColor(target);
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    private Color BaseColor(int index) => (index / 8 + index % 8) % 2 == 0 ? lightSquareColor : darkSquareColor;

    private void ResetColors()
    {
        for (int i = 0; i < squares.Length; i++)
            if (squares[i] != null) squares[i].color = BaseColor(i);
    }

    public void TogglePerspective()
    {
        if (!ready) return;
        isWhitePerspective = !isWhitePerspective;
        UpdatePerspective();
        FocusInput();
    }

    private void UpdatePerspective()
    {
        // Logical coordinates stay fixed when the visual order is reversed.
        for (int visual = 0; visual < 64; visual++)
            squares[isWhitePerspective ? visual : 63 - visual].transform.SetSiblingIndex(visual);
        perspectiveButton.image.sprite = isWhitePerspective ? whitePerspectiveSprite : blackPerspectiveSprite;
    }

    private bool SettingsOpen => SettingsManager.Instance != null
        && SettingsManager.Instance.settingsPanel != null
        && SettingsManager.Instance.settingsPanel.activeInHierarchy;

    private void FocusInput()
    {
        if (!answerInput.gameObject.activeInHierarchy) return;
        answerInput.Select();
        answerInput.ActivateInputField();
    }

    private void ClearInput()
    {
        answerInput.SetTextWithoutNotify(string.Empty);
        answerInput.GetComponent<KeyboardClackSoundRandom>()?.ResetTextTracking();
    }

    private void OnDisable()
    {
        ready = false;
        showingFeedback = false;
        StopAllCoroutines();
        round = null;
        ResetColors();
        if (answerInput != null) ClearInput();
    }

    private void OnDestroy()
    {
        if (answerInput != null) answerInput.onSubmit.RemoveListener(SubmitAnswer);
    }
}
