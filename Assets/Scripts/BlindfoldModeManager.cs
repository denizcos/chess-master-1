using UnityEngine;

[DefaultExecutionOrder(-100)]
public class BlindfoldModeManager : MonoBehaviour
{
    private ChessRules  chessRules;
    private ChessAI     chessAI;
    private BlindfoldUI blindfoldUI;

    void Awake()
    {
        chessRules  = GetComponent<ChessRules>();
        chessAI     = GetComponent<ChessAI>();
        blindfoldUI = GetComponent<BlindfoldUI>();

        if (chessRules != null && blindfoldUI != null)
            blindfoldUI.SetChessRules(chessRules);
        if (chessAI != null && blindfoldUI != null)
            blindfoldUI.SetChessAI(chessAI);
        if (chessRules != null && chessAI != null)
            chessAI.SetChessRules(chessRules);
    }

    void Start()
    {
        Debug.Log("Blindfold mode ready!");
    }

    public void ResetGame()
    {
        if (chessRules   != null) chessRules.ResetGame();
        if (chessAI      != null) chessAI.ResetAI();
        if (blindfoldUI  != null) blindfoldUI.ResetGame();
    }
}
