using UnityEngine;
using UnityEngine.EventSystems;

public class UIButtonHoverSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public static UIButtonHoverSound Instance { get; private set; }

    [Header("Audio Source")]
    public AudioSource audioSource;

    [Header("UI Sounds")]
    public AudioClip hoverClip;
    public AudioClip hoverClip1;
    public AudioClip clickClip;
    public AudioClip switchClip;
    public AudioClip notificationClip;


    [Header("Game Sounds")]
    public AudioClip revealClip;
    public AudioClip revealEndClip;
    public AudioClip captureClip;
    public AudioClip checkClip;
    public AudioClip checkmateClip;
    public AudioClip stalemateClip;
    public AudioClip castleClip;
    public AudioClip invalidClip;

 //   [Header("Answer Sounds")]
    public AudioClip correctClip;
    public AudioClip wrongClip;

    [Header("Move Sounds")]
    public AudioClip[] moveClips;

    [Header("Keyboard Clacks")]
    public AudioClip[] clackClips;
    [Range(0f, 1f)] public float clackVolume = 0.7f;  // slider in Inspector

    [Header("Keyboard Special Keys")]
    public AudioClip deleteClip;
    public AudioClip enterClip;


    private AudioSource notificationSource;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.runInBackground = true;

        notificationSource = gameObject.AddComponent<AudioSource>();
        notificationSource.playOnAwake = false;
        notificationSource.ignoreListenerPause = true;
        if (audioSource != null)
            notificationSource.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
    }

    void OnApplicationFocus(bool hasFocus) { AudioListener.pause = !hasFocus; }
    void OnApplicationPause(bool paused) { if (paused) AudioListener.pause = true; }

    public void OnPointerEnter(PointerEventData eventData) => PlayHover1();
    public void OnPointerClick(PointerEventData eventData) => PlayClick();
    
    public void PlayEnter()
{
    if (audioSource != null && enterClip != null)
        audioSource.PlayOneShot(enterClip, clackVolume);
}

    public void PlayNotification()
    {
        if (notificationSource != null && notificationClip != null)
            notificationSource.PlayOneShot(notificationClip);
    }

    public void PlayDelete()
{
    if (audioSource != null && deleteClip != null)
        audioSource.PlayOneShot(deleteClip, clackVolume);
}

    public void PlayCastle()
    {
        if (audioSource != null && castleClip != null)
            audioSource.PlayOneShot(castleClip);
    }

    public void PlayInvalidMove()
    {
        if (audioSource != null && invalidClip != null)
            audioSource.PlayOneShot(invalidClip);
    }

    public void PlayHover()
    {
        if (audioSource != null && hoverClip != null)
            audioSource.PlayOneShot(hoverClip);
    }

    public void PlayHover1()
    {
        if (audioSource != null && hoverClip1 != null)
            audioSource.PlayOneShot(hoverClip1);
    }

    public void PlayClick()
    {
        if (audioSource != null && clickClip != null)
            audioSource.PlayOneShot(clickClip);
    }

    public void PlayReveal()
    {
        if (audioSource != null && revealClip != null)
            audioSource.PlayOneShot(revealClip);
    }

    public void PlayRevealEnd()
    {
        if (audioSource != null && revealEndClip != null)
            audioSource.PlayOneShot(revealEndClip);
    }

    public void PlayCapture()
    {
        if (audioSource != null && captureClip != null)
            audioSource.PlayOneShot(captureClip);
    }

    public void PlayCheck()
    {
        if (audioSource != null && checkClip != null)
            audioSource.PlayOneShot(checkClip);
    }

    public void PlayCheckmate()
    {
        if (audioSource != null && checkmateClip != null)
            audioSource.PlayOneShot(checkmateClip);
    }

    public void PlayStalemate()
    {
        if (audioSource != null && stalemateClip != null)
            audioSource.PlayOneShot(stalemateClip);
    }

    public void PlayCorrect()
    {
        if (audioSource != null && correctClip != null)
            audioSource.PlayOneShot(correctClip);
    }

    public void PlayWrong()
    {
        if (audioSource != null && wrongClip != null)
            audioSource.PlayOneShot(wrongClip);
    }

    public void PlaySwitch()
    {
        if (audioSource != null && switchClip != null)
            audioSource.PlayOneShot(switchClip);
    }

    public void PlayRandomMove()
    {
        if (audioSource == null || moveClips == null || moveClips.Length == 0) return;
        int idx = Random.Range(0, moveClips.Length);
        var clip = moveClips[idx];
        if (clip != null) audioSource.PlayOneShot(clip);
    }

    // ---- NEW: Random keyboard clack ----
    public void PlayRandomClack()
{
    if (audioSource == null || clackClips == null || clackClips.Length == 0) return;
    int idx = Random.Range(0, clackClips.Length);
    var clip = clackClips[idx];
    if (clip != null) audioSource.PlayOneShot(clip, clackVolume);
}

    // Aliases
public void PlayCorrectSound() => PlayCorrect();
public void PlayWrongSound()   => PlayWrong();


}
