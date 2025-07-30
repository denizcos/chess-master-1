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
        // Add silver/gray borders to buttons
        AddBorderToButton(blackSquareButton);
        AddBorderToButton(whiteSquareButton);

        // Generate first random square
        GenerateRandomSquare();
        UpdateUI();
    }

    void AddBorderToButton(Button button)
    {
        Outline outline = button.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.7f, 0.7f, 0.7f, 1f); // Silver/gray color
        outline.effectDistance = new Vector2(3, 3); // Border thickness

        // Make sure button is still interactable
        button.interactable = true;
    }

    void GenerateRandomSquare()
    {
        // Generate random file (a-h) and rank (1-8)
        char randomFile = files[Random.Range(0, files.Length)];
        int randomRank = ranks[Random.Range(0, ranks.Length)];

        // Create coordinate string
        currentCoordinate = randomFile.ToString() + randomRank.ToString();

        // Determine if square is black or white
        isCurrentSquareBlack = IsSquareBlack(randomFile, randomRank);

        Debug.Log($"Generated square: {currentCoordinate}, Is Black: {isCurrentSquareBlack}");
    }

    bool IsSquareBlack(char file, int rank)
    {
        // Convert file to number (a=1, b=2, c=3, etc.)
        int fileNumber = file - 'a' + 1;

        // Chess rule: square is black if (file + rank) is odd
        // This works because a1 is a dark square in standard chess notation
        return (fileNumber + rank) % 2 == 0;
    }

    public void GuessSquareColor(bool guessedBlack)
    {
        attempts++;

        if (guessedBlack == isCurrentSquareBlack)
        {
            // Correct guess!
            score++;

            // Generate new square for next round
            GenerateRandomSquare();
        }
        // Wrong guess - stay on same square, no feedback needed

        UpdateUI();
    }

    void UpdateUI()
    {
        coordinateText.text = currentCoordinate.ToUpper();
        scoreText.text = $"Score: {score}/{attempts}";
    }

    // Optional: Reset game
    public void ResetGame()
    {
        score = 0;
        attempts = 0;
        GenerateRandomSquare();
        UpdateUI();
    }
}