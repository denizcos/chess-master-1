using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu")]
    [Tooltip("The root Panel (GameObject) of the Main Menu to disable on mode start.")]
    public GameObject mainMenuPanel;

    [Header("Buttons")]
    public Button coordinateModeButton; // Mode 1
    public Button aiModeButton;         // Mode 2
    public Button colorModeButton;      // Mode 3
    public Button movingModeButton;     // Mode 4
    public Button versusModeButton;     // Mode 5

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
        // (If you set objectsToEnable manually in the Inspector, you can delete these three lines per mode.)
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

        // Wire up buttons if assigned
        TryWireButton(coordinateModeButton, () => StartMode(coordinateMode));
        TryWireButton(aiModeButton, () => StartMode(aiMode));
        TryWireButton(colorModeButton, () => StartMode(colorMode));
        TryWireButton(movingModeButton, () => StartMode(movingMode));
        TryWireButton(versusModeButton, () => StartMode(versusMode));
    }

    private void TryWireButton(Button btn, System.Action onClick)
    {
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => onClick?.Invoke());
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

    // Optional: Public methods if you prefer assigning these directly in Button OnClick
    public void OnClick_CoordinateMode() => StartMode(coordinateMode);
    public void OnClick_AIMode()         => StartMode(aiMode);
    public void OnClick_ColorMode()      => StartMode(colorMode);
    public void OnClick_MovingMode()     => StartMode(movingMode);
    public void OnClick_VersusMode()     => StartMode(versusMode);







}
