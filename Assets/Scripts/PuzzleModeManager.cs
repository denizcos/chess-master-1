 /*

using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

[System.Serializable]
public class Puzzle
{
    public string id;
    public string fen;
    public List<string> solution;
    public string difficulty;
    public string theme;
}

public class PuzzleModeManager : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField inputField;
    public TextMeshProUGUI aiMoveText;

    [Header("Board Reference")]
    public GameObject boardObject; // Should be the same board used in BlindfoldMode

    [Header("Settings")]
    public float previewDuration = 5f;

    private List<Puzzle> puzzles;
    private int currentMoveIndex;
    private Puzzle currentPuzzle;
    private bool isWaitingForPlayer = false;

    void Start()
    {
        LoadPuzzles();
        SetupInputSubmission();
        StartPuzzle();
    }

    void LoadPuzzles()
    {
        TextAsset puzzleText = Resources.Load<TextAsset>("puzzles");
        if (puzzleText == null)
        {
            Debug.LogError("Puzzle file not found in Resources!");
            return;
        }

        puzzles = JsonUtilityWrapper.FromJsonArray<Puzzle>(puzzleText.text);
    }

    void StartPuzzle()
    {
        if (puzzles == null || puzzles.Count == 0)
        {
            Debug.LogError("No puzzles loaded.");
            return;
        }

        currentPuzzle = puzzles[Random.Range(0, puzzles.Count)];
        currentMoveIndex = 0;
        inputField.text = "";
        aiMoveText.text = "";

        // Load the FEN position visually
        if (boardObject != null)
        {
            boardObject.SetActive(true);
            var fenLoader = boardObject.GetComponent<BlindfoldModeManager>(); // or whatever your loader is
            if (fenLoader != null)
                fenLoader.LoadFEN(currentPuzzle.fen);
        }

        StartCoroutine(HideBoardAfterDelay(previewDuration));
    }

    IEnumerator HideBoardAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (boardObject != null)
            boardObject.SetActive(false);

        isWaitingForPlayer = true;
    }

    void SetupInputSubmission()
    {
        inputField.onSubmit.AddListener(HandlePlayerInput);

        // Optional: allow Enter key to trigger manually if not working
        EventSystem.current.SetSelectedGameObject(inputField.gameObject);
    }

    void HandlePlayerInput(string playerMove)
    {
        if (!isWaitingForPlayer || currentPuzzle == null) return;

        playerMove = playerMove.Trim();

        string expectedMove = currentPuzzle.solution[currentMoveIndex];
        if (playerMove.Equals(expectedMove, System.StringComparison.OrdinalIgnoreCase))
        {
            currentMoveIndex++;
            inputField.text = "";

            if (currentMoveIndex >= currentPuzzle.solution.Count)
            {
                aiMoveText.text = "Puzzle Solved!";
                isWaitingForPlayer = false;
            }
            else
            {
                // Show AI move (next move in the list, since puzzle is player+AI alternating)
                string aiMove = currentPuzzle.solution[currentMoveIndex];
                aiMoveText.text = $"AI played: {aiMove}";
                currentMoveIndex++; // Skip AI move
            }
        }
        else
        {
            aiMoveText.text = "Incorrect. Try again.";
        }

        // Refocus input field
        EventSystem.current.SetSelectedGameObject(inputField.gameObject);
    }
}

// Helper for JSON array parsing
public static class JsonUtilityWrapper
{
    public static List<T> FromJsonArray<T>(string json)
    {
        string wrapped = "{\"Items\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(wrapped);
        return wrapper.Items;
    }

    [System.Serializable]
    private class Wrapper<T>
    {
        public List<T> Items;
    }
}



*/