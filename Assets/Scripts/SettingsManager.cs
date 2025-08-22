using UnityEngine.UI;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public ScrollRect settingsScrollRect;

    [Header("Audio Settings (UI)")]
    // generalSoundToggle controls SFX ONLY (not music)
    public Toggle generalSoundToggle;
    public Toggle musicToggle;

    // Controls MUSIC ONLY
    public Slider masterVolumeSlider;

    [Header("Audio Refs")]
    // Assign your background-music AudioSource here in the Inspector
    public AudioSource musicSource;

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
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (closeSettingsButton != null)
            closeSettingsButton.onClick.AddListener(CloseSettings);

        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(QuitGame);

        if (generalSoundToggle != null)
        {
            generalSoundToggle.onValueChanged.AddListener(OnGeneralSoundToggled);
            generalSoundToggle.isOn = true; // default
        }

        if (musicToggle != null)
        {
            musicToggle.onValueChanged.AddListener(OnMusicToggled);
            musicToggle.isOn = true; // default
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            masterVolumeSlider.value = 0.45f; // default
        }

        LoadSettings();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isSettingsOpen)
            {
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
            settingsPanel.transform.SetAsLastSibling();

            CanvasGroup cg = settingsPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = settingsPanel.AddComponent<CanvasGroup>();
            cg.interactable = true;
            cg.blocksRaycasts = true;
            cg.alpha = 1f;
        }
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
            isSettingsOpen = false;
        }

        // Re-apply current music state so music does NOT come back after closing
        ApplyMusicMute();
        ApplyMusicVolume();
    }

    // ==== Audio handlers ====

    // SFX ONLY
    void OnGeneralSoundToggled(bool enabled)
    {
        // Example: UI sounds
        if (UIButtonHoverSound.Instance != null && UIButtonHoverSound.Instance.audioSource != null)
        {
            UIButtonHoverSound.Instance.audioSource.mute = !enabled;
        }

        // If you have a central SFX manager/mixer, hook it here.
        // e.g., SFXManager.Instance?.SetEnabled(enabled);

        SaveSettings();
    }

    // MUSIC mute/unmute
    void OnMusicToggled(bool enabled)
    {
        ApplyMusicMute();
        SaveSettings();
    }

    // MUSIC volume only
    void OnMusicVolumeChanged(float v)
    {
        ApplyMusicVolume();
        SaveSettings();
    }

    void ApplyMusicMute()
    {
        bool musicEnabled = musicToggle == null ? true : musicToggle.isOn;

        // Prefer to mute the music AudioSource only (leaves SFX alone)
        if (musicSource != null)
        {
            musicSource.mute = !musicEnabled;

            // If music is disabled, ensure it doesn't auto-play
            if (!musicEnabled && musicSource.isPlaying)
                musicSource.Pause();
            else if (musicEnabled && !musicSource.isPlaying)
                musicSource.UnPause(); // resumes if it was paused
        }

        // If you also have a MusicManager that starts/stops music globally, you can keep this optional guard:
        // (Commented to avoid missing-method compile errors)
        // if (MusicManager.Instance != null)
        // {
        //     if (musicEnabled) MusicManager.Instance.StartBackgroundMusic();
        //     else MusicManager.Instance.StopBackgroundMusic();
        // }
    }

    void ApplyMusicVolume()
    {
        float vol = masterVolumeSlider != null ? masterVolumeSlider.value : 1f;
        if (musicSource != null)
        {
            musicSource.volume = vol;
        }
        // Do NOT touch AudioListener.volume — that would affect SFX too.
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
            bool sfx = PlayerPrefs.GetInt("GeneralSound", 1) == 1;
            generalSoundToggle.isOn = sfx;
            OnGeneralSoundToggled(sfx);
        }

        if (musicToggle != null)
        {
            bool mus = PlayerPrefs.GetInt("Music", 1) == 1;
            musicToggle.isOn = mus;
            // don’t call OnMusicToggled here to avoid double saves; just apply
        }

        if (masterVolumeSlider != null)
        {
            float vol = PlayerPrefs.GetFloat("MasterVolume", 0.45f); // default 45%
            masterVolumeSlider.value = vol;
        }


        // Apply both after values are loaded
        ApplyMusicMute();
        ApplyMusicVolume();
    }

    public bool IsSettingsOpen() => isSettingsOpen;
}
