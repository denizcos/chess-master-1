using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Centralized sound manager for UI and game events.
/// Handles hover, click, reveal, capture, check, checkmate, stalemate, and answer sounds.
/// Attach to a persistent GameObject in your scene and assign the audio clips in the Inspector.
/// </summary>
public class UISoundManager : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public static UISoundManager Instance { get; private set; }

    [Header("Audio Source")]
    public AudioSource audioSource;

    [Header("UI Sounds")]
    public AudioClip hoverClip;
    public AudioClip clickClip;

    [Header("Game Sounds")]
    public AudioClip revealClip;
    public AudioClip captureClip;
    public AudioClip checkClip;
    public AudioClip checkmateClip;
    public AudioClip stalemateClip;

    [Header("Answer Sounds")]
    public AudioClip correctClip;
    public AudioClip wrongClip;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // IPointerEnterHandler implementation for UI hover
    public void OnPointerEnter(PointerEventData eventData)
    {
        PlayHover();
    }

    // IPointerClickHandler implementation for UI click
    public void OnPointerClick(PointerEventData eventData)
    {
        PlayClick();
    }

    // Play UI hover sound
    public void PlayHover()
    {
        if (audioSource != null && hoverClip != null)
            audioSource.PlayOneShot(hoverClip);
    }

    // Play UI click sound
    public void PlayClick()
    {
        if (audioSource != null && clickClip != null)
            audioSource.PlayOneShot(clickClip);
    }

    // Play reveal-board sound
    public void PlayReveal()
    {
        if (audioSource != null && revealClip != null)
            audioSource.PlayOneShot(revealClip);
    }

    // Play piece capture sound
    public void PlayCapture()
    {
        if (audioSource != null && captureClip != null)
            audioSource.PlayOneShot(captureClip);
    }

    // Play check sound
    public void PlayCheck()
    {
        if (audioSource != null && checkClip != null)
            audioSource.PlayOneShot(checkClip);
    }

    // Play checkmate sound
    public void PlayCheckmate()
    {
        if (audioSource != null && checkmateClip != null)
            audioSource.PlayOneShot(checkmateClip);
    }

    // Play stalemate sound
    public void PlayStalemate()
    {
        if (audioSource != null && stalemateClip != null)
            audioSource.PlayOneShot(stalemateClip);
    }

    // Play correct answer sound (retained)
    public void PlayCorrect()
    {
        if (audioSource != null && correctClip != null)
            audioSource.PlayOneShot(correctClip);
    }

    // Play wrong answer sound (retained)
    public void PlayWrong()
    {
        if (audioSource != null && wrongClip != null)
            audioSource.PlayOneShot(wrongClip);
    }
}
