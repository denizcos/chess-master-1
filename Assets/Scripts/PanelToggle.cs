using UnityEngine;

public class PanelToggle : MonoBehaviour
{
    public CanvasGroup panelCanvasGroup;

    // Call this to hide the panel
    public void HidePanel()
    {
        panelCanvasGroup.alpha = 0f;
        panelCanvasGroup.interactable = false;
        panelCanvasGroup.blocksRaycasts = false;
    }

    // Call this to show the panel
    public void ShowPanel()
    {
        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.interactable = true;
        panelCanvasGroup.blocksRaycasts = true;
    }
}
