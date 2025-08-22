using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource musicSource;

    [Header("Music Clips")]
    [Tooltip("First clip that plays on title screen")]
    public AudioClip titleScreenMusic;

    [Tooltip("Background music clips that play randomly after title")]
    public AudioClip[] backgroundMusicClips;

    [Header("Settings")]
    public float fadeDuration = 2f;

    private List<AudioClip> shuffledPlaylist;
    private int currentTrackIndex = 0;
    private bool isTitleMusicPlayed = false;
    private bool isMusicEnabled = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeMusic();
    }
    void OnApplicationFocus(bool hasFocus)
    {
        if (musicSource != null)
        {
            if (hasFocus && isMusicEnabled)
            {
                musicSource.UnPause();
            }
            else
            {
                musicSource.Pause();
            }
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (musicSource != null)
        {
            if (pauseStatus)
            {
                musicSource.Pause();
            }
            else if (isMusicEnabled)
            {
                musicSource.UnPause();
            }
        }
    }
    void InitializeMusic()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.loop = false;
        musicSource.playOnAwake = false;

        // Start with title screen music
        if (titleScreenMusic != null && isMusicEnabled)
        {
            PlayTitleMusic();
        }

        // Prepare shuffled playlist
        ShuffleBackgroundMusic();
    }

    public void PlayTitleMusic()
    {
        if (titleScreenMusic != null && isMusicEnabled)
        {
            musicSource.clip = titleScreenMusic;
            musicSource.Play();
            isTitleMusicPlayed = true;
        }
    }

    public void StartBackgroundMusic()
    {
        if (!isMusicEnabled) return;

        // Only start background music after title music
        if (isTitleMusicPlayed)
        {
            PlayNextTrack();
        }
    }

    void ShuffleBackgroundMusic()
    {
        if (backgroundMusicClips == null || backgroundMusicClips.Length == 0) return;

        shuffledPlaylist = new List<AudioClip>(backgroundMusicClips);

        // Fisher-Yates shuffle
        for (int i = shuffledPlaylist.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            AudioClip temp = shuffledPlaylist[i];
            shuffledPlaylist[i] = shuffledPlaylist[randomIndex];
            shuffledPlaylist[randomIndex] = temp;
        }

        currentTrackIndex = 0;
    }

    void PlayNextTrack()
    {
        if (!isMusicEnabled || shuffledPlaylist == null || shuffledPlaylist.Count == 0) return;

        musicSource.clip = shuffledPlaylist[currentTrackIndex];
        musicSource.Play();

        currentTrackIndex++;

        // Reshuffle when we reach the end to prevent immediate repeats
        if (currentTrackIndex >= shuffledPlaylist.Count)
        {
            ShuffleBackgroundMusic();
        }
    }

    void Update()
    {
        // Only continue music logic if application is focused
        if (!Application.isFocused) return;

        // Auto-play next track when current one ends (but only if it actually finished, not stopped)
        if (isMusicEnabled && !musicSource.isPlaying && musicSource.clip != null && musicSource.time == 0)
        {
            if (musicSource.clip == titleScreenMusic && isTitleMusicPlayed)
            {
                StartBackgroundMusic();
            }
            else if (shuffledPlaylist != null && shuffledPlaylist.Contains(musicSource.clip))
            {
                PlayNextTrack();
            }
        }
    }

    public void SetMusicEnabled(bool enabled)
    {
        isMusicEnabled = enabled;

        if (!enabled)
        {
            musicSource.Pause(); // Changed from Stop() to Pause()
        }
        else
        {
            musicSource.UnPause(); // Changed to resume where it left off
        }
    }

    public bool IsMusicEnabled()
    {
        return isMusicEnabled;
    }
}

