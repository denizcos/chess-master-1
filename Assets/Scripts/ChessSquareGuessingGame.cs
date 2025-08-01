using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChessSquareGuessingGame : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI coordinateText;
    public Button blackSquareButton;
    public Button whiteSquareButton;
    public TextMeshProUGUI scoreText;

    private string currentCoordinate;
    private bool isCurrentSquareBlack;
    private int score = 0;
    private int attempts = 0;

    // Chess board files and ranks
    private char[] files = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h' };
    private int[] ranks = { 1, 2, 3, 4, 5, 6, 7, 8 };

    void Start()
    {
        AddBorderToButton(blackSquareButton);
        AddBorderToButton(whiteSquareButton);

        GenerateRandomSquare();
        UpdateUI();
    }

    void AddBorderToButton(Button button)
    {
        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Silver/gray color
        outline.effectDistance = new Vector2(3, 3);
        button.interactable = true;
    }

    void GenerateRandomSquare()
    {
        char randomFile = files[Random.Range(0, files.Length)];
        int randomRank = ranks[Random.Range(0, ranks.Length)];
        currentCoordinate = randomFile.ToString() + randomRank.ToString();
        isCurrentSquareBlack = IsSquareBlack(randomFile, randomRank);
        Debug.Log($"Generated square: {currentCoordinate}, Is Black: {isCurrentSquareBlack}");
    }

    bool IsSquareBlack(char file, int rank)
    {
        int fileNumber = file - 'a' + 1;
        return (fileNumber + rank) % 2 == 0;
    }

    public void GuessSquareColor(bool guessedBlack)
    {
        attempts++;

        if (guessedBlack == isCurrentSquareBlack)
        {
            score++;

            // Play correct sound
            if (UIButtonHoverSound.Instance != null)
                UIButtonHoverSound.Instance.PlayCorrectSound();

            GenerateRandomSquare();
        }
        else
        {
            // Play wrong sound
            if (UIButtonHoverSound.Instance != null)
                UIButtonHoverSound.Instance.PlayWrongSound();
        }

        UpdateUI();
    }

    void UpdateUI()
    {
        coordinateText.text = currentCoordinate.ToLower(); // Always lowercase
        scoreText.text = $"{score}/{attempts}"; // No "Score:" prefix
    }

    public void ResetGame()
    {
        score = 0;
        attempts = 0;
        GenerateRandomSquare();
        UpdateUI();
    }
}
