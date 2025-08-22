using UnityEngine.UI;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public ScrollRect settingsScrollRect;

    [Header("Audio Settings")]
    public Toggle generalSoundToggle;
    public Toggle musicToggle;
    public Slider masterVolumeSlider;

    [Header("Other Settings")]
    public Button quitGameButton;

    [Header("Navigation")]
    public Button closeSettingsButton;

    private bool isSettingsOpen = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSettings();
    }

    void InitializeSettings()
    {
        // Hide settings panel initially
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // Setup button listeners
        if (closeSettingsButton != null)
            closeSettingsButton.onClick.AddListener(CloseSettings);

        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(QuitGame);

        // Setup audio controls
        if (generalSoundToggle != null)
        {
            generalSoundToggle.onValueChanged.AddListener(OnGeneralSoundToggled);
            generalSoundToggle.isOn = true; // Default enabled
        }

        if (musicToggle != null)
        {
            musicToggle.onValueChanged.AddListener(OnMusicToggled);
            musicToggle.isOn = true; // Default enabled
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            masterVolumeSlider.value = 1f; // Default full volume
        }

        LoadSettings();
    }

    void Update()
    {
        // Handle Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isSettingsOpen)
            {
                // Play button sound when closing with Escape
                if (UIButtonHoverSound.Instance != null)
                    UIButtonHoverSound.Instance.PlayClick();

                CloseSettings();
            }
            else
            {
                OpenSettings();
            }
        }
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
            isSettingsOpen = true;

            // Make sure settings panel is on top
            settingsPanel.transform.SetAsLastSibling();

            // Ensure Canvas Group allows interaction
            CanvasGroup canvasGroup = settingsPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = settingsPanel.AddComponent<CanvasGroup>();

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1f;
        }
    }
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            isSettingsOpen = false;
        }
    }


    void OnGeneralSoundToggled(bool enabled)
    {
        // Control all UI sounds
        if (UIButtonHoverSound.Instance != null)
        {
            UIButtonHoverSound.Instance.audioSource.mute = !enabled;
        }

        // You can extend this to control other sound effects
        SaveSettings();
    }

    void OnMusicToggled(bool enabled)
    {
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.SetMusicEnabled(enabled);
        }
        SaveSettings();
    }

    void OnVolumeChanged(float volume)
    {
        // Set master volume
        AudioListener.volume = volume;
        SaveSettings();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    void SaveSettings()
    {
        if (generalSoundToggle != null)
            PlayerPrefs.SetInt("GeneralSound", generalSoundToggle.isOn ? 1 : 0);

        if (musicToggle != null)
            PlayerPrefs.SetInt("Music", musicToggle.isOn ? 1 : 0);

        if (masterVolumeSlider != null)
            PlayerPrefs.SetFloat("MasterVolume", masterVolumeSlider.value);

        PlayerPrefs.Save();
    }

    void LoadSettings()
    {
        if (generalSoundToggle != null)
        {
            bool soundEnabled = PlayerPrefs.GetInt("GeneralSound", 1) == 1;
            generalSoundToggle.isOn = soundEnabled;
            OnGeneralSoundToggled(soundEnabled);
        }

        if (musicToggle != null)
        {
            bool musicEnabled = PlayerPrefs.GetInt("Music", 1) == 1;
            musicToggle.isOn = musicEnabled;
            OnMusicToggled(musicEnabled);
        }

        if (masterVolumeSlider != null)
        {
            float volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            masterVolumeSlider.value = volume;
            OnVolumeChanged(volume);
        }
    }

    public bool IsSettingsOpen()
    {
        return isSettingsOpen;
    }
}

// Extension to your InputBasedMainMenuController
public class MenuMusicTrigger : MonoBehaviour
{
    void Start()
    {
        // Trigger background music when leaving title screen
        if (MusicManager.Instance != null)
        {
            MusicManager.Instance.StartBackgroundMusic();
        }
    }
}