using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MoveChallengeManager : MonoBehaviour
{
    public enum Mode { Knight, Bishop }

    [Header("UI References")]
    public TextMeshProUGUI promptText;
    public TMP_InputField moveInput;
    public TextMeshProUGUI feedbackText;

    private Vector2Int currentPos;
    private Vector2Int targetPos;
    private bool gameActive = false;
    private Mode currentMode;

    private readonly HashSet<Vector2Int> knightMoves = new()
    {
        new Vector2Int(1, 2), new Vector2Int(2, 1),
        new Vector2Int(2, -1), new Vector2Int(1, -2),
        new Vector2Int(-1, -2), new Vector2Int(-2, -1),
        new Vector2Int(-2, 1), new Vector2Int(-1, 2)
    };

    void Start()
    {
        StartNewChallenge();
        feedbackText.text = "";
        moveInput.onSubmit.AddListener(OnPlayerMoveEntered);
    }

    void StartNewChallenge()
    {
        currentMode = Random.value < 0.5f ? Mode.Knight : Mode.Bishop;

        currentPos = GetRandomSquare();
        do
        {
            targetPos = GetRandomSquare();
        } while (targetPos == currentPos || (currentMode == Mode.Bishop && !SameColor(currentPos, targetPos)));

        string from = SquareName(currentPos);
        string to = SquareName(targetPos);
        promptText.text = $"Move the {currentMode.ToString().ToLower()} from {from} to {to}";

        feedbackText.text = "";
        moveInput.text = "";
        moveInput.ActivateInputField();
        gameActive = true;
    }

    void OnPlayerMoveEntered(string input)
    {
        if (!gameActive) return;

        input = input.Trim().ToLower();
        if (!TryParseSquare(input, out Vector2Int move))
        {
            feedbackText.text = "Invalid format. Use e.g. 'c3'.";
            moveInput.text = "";
            moveInput.ActivateInputField();
            return;
        }

        if (!IsLegalMove(currentPos, move))
        {
            feedbackText.text = $"Illegal {currentMode.ToString().ToLower()} move from {SquareName(currentPos)} to {SquareName(move)}.";
            moveInput.text = "";
            moveInput.ActivateInputField();
            return;
        }

        currentPos = move;

        if (currentPos == targetPos)
        {
            feedbackText.text = $"Success!";
            gameActive = false;
            StartCoroutine(RestartChallengeAfterDelay());
        }
        else
        {
            feedbackText.text = $"Moved to {SquareName(currentPos)}. Keep going.";
        }

        moveInput.text = "";
        moveInput.ActivateInputField();
    }

    IEnumerator RestartChallengeAfterDelay()
    {
        yield return new WaitForSeconds(1.2f);
        StartNewChallenge();
    }

    bool IsLegalMove(Vector2Int from, Vector2Int to)
    {
        if (currentMode == Mode.Knight)
        {
            Vector2Int delta = to - from;
            return knightMoves.Contains(delta);
        }
        else if (currentMode == Mode.Bishop)
        {
            return Mathf.Abs(to.x - from.x) == Mathf.Abs(to.y - from.y);
        }
        return false;
    }

    Vector2Int GetRandomSquare()
    {
        return new Vector2Int(Random.Range(0, 8), Random.Range(0, 8));
    }

    string SquareName(Vector2Int pos)
    {
        char file = (char)('a' + pos.x);
        int rank = pos.y + 1;
        return $"{file}{rank}";
    }

    bool TryParseSquare(string input, out Vector2Int result)
    {
        result = new Vector2Int();
        if (input.Length != 2) return false;

        char file = input[0];
        char rank = input[1];

        if (file < 'a' || file > 'h') return false;
        if (rank < '1' || rank > '8') return false;

        result.x = file - 'a';
        result.y = rank - '1';
        return true;
    }

    bool SameColor(Vector2Int a, Vector2Int b)
    {
        return (a.x + a.y) % 2 == (b.x + b.y) % 2;
    }
}
