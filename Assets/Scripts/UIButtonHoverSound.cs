using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHoverSound : MonoBehaviour, IPointerEnterHandler
{
    public static UIButtonHoverSound Instance;

    [Header("UI Hover")]
    public AudioClip hoverClip;
    public AudioSource audioSource;

    [Header("Chess Sounds")]
    public AudioClip moveSound;
    public AudioClip captureSound;
    public AudioClip checkSound;

    [Header("Answer Sounds")]
    public AudioClip correctClip;
    public AudioClip wrongClip;

    void Awake()
    {
        Instance = this;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverClip != null && audioSource != null)
        {
            audioSource.PlayOneShot(hoverClip);
        }
    }

    public void PlayMove()
    {
        if (audioSource != null && moveSound != null)
            audioSource.PlayOneShot(moveSound);
    }

    public void PlayCapture()
    {
        if (audioSource != null && captureSound != null)
            audioSource.PlayOneShot(captureSound);
    }

    public void PlayCheck()
    {
        if (audioSource != null && checkSound != null)
            audioSource.PlayOneShot(checkSound);
    }

    public void PlayCorrectSound()
    {
        if (audioSource != null && correctClip != null)
            audioSource.PlayOneShot(correctClip);
    }

    public void PlayWrongSound()
    {
        if (audioSource != null && wrongClip != null)
            audioSource.PlayOneShot(wrongClip);
    }
}
