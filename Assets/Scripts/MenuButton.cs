using UnityEngine;

public class CanvasUIManager : MonoBehaviour
{
    [Header("Assign your main Canvas here")]
    public GameObject mainCanvas;

    [Header("Assign the UI panels for Main Menu state")]
    public GameObject[] mainMenuObjects;

    // Call this from any button
    public void ReturnToMainMenu()
    {
        if (mainCanvas == null)
        {
            Debug.LogError("Main Canvas not assigned!");
            return;
        }

        // Disable all direct children of the canvas
        foreach (Transform child in mainCanvas.transform)
        {
            child.gameObject.SetActive(false);
        }

        // Re-enable the main menu objects
        foreach (GameObject menuObj in mainMenuObjects)
        {
            if (menuObj != null)
                menuObj.SetActive(true);
        }
    }
}
