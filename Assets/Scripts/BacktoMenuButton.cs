using UnityEngine;
using UnityEngine.UI;

public class BackToMenuController : MonoBehaviour
{
    [Header("Back to Menu Buttons")]
    [Tooltip("All buttons that will trigger going back to main menu")]
    public Button[] backToMenuButtons;

    [Header("Main Menu Reference")]
    [Tooltip("Reference to the main menu panel to re-enable")]
    public GameObject mainMenuPanel;

    [Header("Current Mode Objects")]
    [Tooltip("All GameObjects that should be disabled when going back to menu")]
    public GameObject[] currentModeObjects;

    [Header("Current Mode Panel")]
    [Tooltip("The current mode's UI panel to disable")]
    public GameObject currentModePanel;

    private void Start()
    {
        // Wire up all buttons
        if (backToMenuButtons != null)
        {
            foreach (var button in backToMenuButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(GoBackToMenu);
                }
            }
        }
    }

    /// <summary>
    /// Goes back to the main menu by disabling current mode objects and enabling main menu
    /// </summary>
    public void GoBackToMenu()
    {
        // Disable all current mode objects
        if (currentModeObjects != null)
        {
            foreach (var go in currentModeObjects)
            {
                if (go != null) go.SetActive(false);
            }
        }

        // Disable current mode panel
        if (currentModePanel != null)
            currentModePanel.SetActive(false);

        // Enable main menu panel
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        Debug.Log("Returned to main menu");
    }

    /// <summary>
    /// Alternative method for manual setup - finds main menu automatically
    /// </summary>
    public void GoBackToMenuAuto()
    {
        // Try to find the main menu controller
        var mainMenuController = FindObjectOfType<InputBasedMainMenuController>();
        if (mainMenuController != null && mainMenuController.mainMenuPanel != null)
        {
            // Disable all current mode objects
            if (currentModeObjects != null)
            {
                foreach (var go in currentModeObjects)
                {
                    if (go != null) go.SetActive(false);
                }
            }

            // Disable current mode panel
            if (currentModePanel != null)
                currentModePanel.SetActive(false);

            // Enable main menu
            mainMenuController.mainMenuPanel.SetActive(true);

            Debug.Log("Returned to main menu (auto-detected)");
        }
        else
        {
            Debug.LogError("Could not find InputBasedMainMenuController or main menu panel!");
        }
    }

    /// <summary>
    /// Add a new button to the back-to-menu system at runtime
    /// </summary>
    public void AddBackButton(Button newButton)
    {
        if (newButton == null) return;

        // Add to array
        System.Array.Resize(ref backToMenuButtons, backToMenuButtons.Length + 1);
        backToMenuButtons[backToMenuButtons.Length - 1] = newButton;

        // Wire up the new button
        newButton.onClick.RemoveAllListeners();
        newButton.onClick.AddListener(GoBackToMenu);
    }

    /// <summary>
    /// Remove a button from the back-to-menu system
    /// </summary>
    public void RemoveBackButton(Button buttonToRemove)
    {
        if (buttonToRemove == null || backToMenuButtons == null) return;

        // Remove event listener
        buttonToRemove.onClick.RemoveListener(GoBackToMenu);

        // Remove from array
        var newArray = new Button[backToMenuButtons.Length - 1];
        int newIndex = 0;
        for (int i = 0; i < backToMenuButtons.Length; i++)
        {
            if (backToMenuButtons[i] != buttonToRemove)
            {
                if (newIndex < newArray.Length)
                    newArray[newIndex++] = backToMenuButtons[i];
            }
        }
        backToMenuButtons = newArray;
    }
}