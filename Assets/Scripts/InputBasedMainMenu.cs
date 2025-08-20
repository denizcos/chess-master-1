using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class InputBasedMainMenuController : MonoBehaviour
{
    [Header("Main Menu")]
    [Tooltip("The root Panel (GameObject) of the Main Menu to disable on mode start.")]
    public GameObject mainMenuPanel;

    [Header("Input Field")]
    [Tooltip("The TMP Input Field for mode selection")]
    public TMP_InputField modeInputField;

    [System.Serializable]
    public class ModeConfig
    {
        [Tooltip("Optional label for clarity only.")]
        public string modeName;

        [Tooltip("All GameObjects that must be enabled for this mode (scripts, managers, etc.).")]
        public GameObject[] objectsToEnable;

        [Tooltip("The specific UI Panel to enable for this mode.")]
        public GameObject modePanel;
    }

    [Header("Mode 1 — Coordinate Mode")]
    [Tooltip("Link your EmptyBoardMode GameObject here.")]
    public GameObject coordinate_EmptyBoardModeObject;
    public ModeConfig coordinateMode;

    [Header("Mode 2 — AI Mode")]
    [Tooltip("Link your EmptyBoardMode GameObject here.")]
    public GameObject ai_EmptyBoardModeObject;
    [Tooltip("Link your BlindfoldMode GameObject here.")]
    public GameObject ai_BlindfoldModeObject;
    public ModeConfig aiMode;

    [Header("Mode 3 — Color Mode")]
    [Tooltip("Link your Color mode GameObject here.")]
    public GameObject color_ColorModeObject;
    public ModeConfig colorMode;

    [Header("Mode 4 — Moving Mode")]
    [Tooltip("Link your Moving mode GameObject here.")]
    public GameObject moving_MovingModeObject;
    public ModeConfig movingMode;

    [Header("Mode 5 — Versus Mode")]
    [Tooltip("Link the three required GameObjects here.")]
    public GameObject versus_ObjectA;
    public GameObject versus_ObjectB;
    public GameObject versus_ObjectC;
    public ModeConfig versusMode;

    private void Awake()
    {
        // Build up each ModeConfig.objectsToEnable from the single references, if you prefer to wire them automatically.
        // (If you set objectsToEnable manually in the Inspector, you can delete these lines per mode.)
        if (coordinateMode != null && (coordinateMode.objectsToEnable == null || coordinateMode.objectsToEnable.Length == 0))
            coordinateMode.objectsToEnable = new[] { coordinate_EmptyBoardModeObject };

        if (aiMode != null && (aiMode.objectsToEnable == null || aiMode.objectsToEnable.Length == 0))
            aiMode.objectsToEnable = new[] { ai_EmptyBoardModeObject, ai_BlindfoldModeObject };

        if (colorMode != null && (colorMode.objectsToEnable == null || colorMode.objectsToEnable.Length == 0))
            colorMode.objectsToEnable = new[] { color_ColorModeObject };

        if (movingMode != null && (movingMode.objectsToEnable == null || movingMode.objectsToEnable.Length == 0))
            movingMode.objectsToEnable = new[] { moving_MovingModeObject };

        if (versusMode != null && (versusMode.objectsToEnable == null || versusMode.objectsToEnable.Length == 0))
            versusMode.objectsToEnable = new[] { versus_ObjectA, versus_ObjectB, versus_ObjectC };

        // Setup input field
        SetupInputField();
    }

    private void SetupInputField()
    {
        if (modeInputField == null) return;

        // Add input submission listener
        modeInputField.onSubmit.AddListener(OnInputSubmitted);
    }

    private void Update()
    {
        // Handle Enter key press manually
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (modeInputField != null)
            {
                ProcessModeInput(modeInputField.text);
            }
        }
    }

    private void OnInputSubmitted(string input)
    {
        ProcessModeInput(input);
    }

    private void ProcessModeInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return;

        // Convert to lowercase for case-insensitive comparison
        input = input.ToLower().Trim();

        ModeConfig selectedMode = GetModeFromInput(input);
        if (selectedMode != null)
        {
            StartMode(selectedMode);
        }

        // Clear the input field after processing
        modeInputField.text = "";
    }

    private ModeConfig GetModeFromInput(string input)
    {
        switch (input)
        {
            case "a1": return coordinateMode;  // Mode 1
            case "b2": return aiMode;          // Mode 2
            case "c3": return colorMode;       // Mode 3
            case "d4": return movingMode;      // Mode 4
            case "e5": return versusMode;      // Mode 5
            default: return null;
        }
    }

    /// <summary>
    /// Enables all linked objects + the panel for this mode, then disables the main menu panel.
    /// </summary>
    public void StartMode(ModeConfig mode)
    {
        if (mode == null)
        {
            Debug.LogError("StartMode called with null ModeConfig.");
            return;
        }

        // Enable required objects
        if (mode.objectsToEnable != null)
        {
            foreach (var go in mode.objectsToEnable)
            {
                if (go != null) go.SetActive(true);
            }
        }

        // Enable the mode's UI panel
        if (mode.modePanel != null)
            mode.modePanel.SetActive(true);

        // Disable main menu
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
    }

    // Optional: Public methods for external access (matching your original structure)
    public void OnClick_CoordinateMode() => StartMode(coordinateMode);
    public void OnClick_AIMode() => StartMode(aiMode);
    public void OnClick_ColorMode() => StartMode(colorMode);
    public void OnClick_MovingMode() => StartMode(movingMode);
    public void OnClick_VersusMode() => StartMode(versusMode);
}