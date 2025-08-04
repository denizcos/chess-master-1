using UnityEngine;

public class BlindfoldModeManager : MonoBehaviour
{
    private ChessRules chessRules;
    private ChessAI chessAI;
    private BlindfoldUI blindfoldUI;

    void Start()
    {
        // Get components on this same GameObject
        chessRules = GetComponent<ChessRules>();
        chessAI = GetComponent<ChessAI>();
        blindfoldUI = GetComponent<BlindfoldUI>();

        // Connect them
        if (chessAI != null && chessRules != null)
            chessAI.SetChessRules(chessRules);

        if (blindfoldUI != null)
        {
            if (chessRules != null)
                blindfoldUI.SetChessRules(chessRules);
            if (chessAI != null)
                blindfoldUI.SetChessAI(chessAI);
        }

        Debug.Log("Blindfold mode ready!");
    }

    public void ResetGame()
    {
        if (chessRules != null) chessRules.ResetGame();
        if (chessAI != null) chessAI.ResetAI();
        if (blindfoldUI != null) blindfoldUI.ResetGame();
    }
}