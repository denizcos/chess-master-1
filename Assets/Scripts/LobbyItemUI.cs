using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text lobbyNameText;
    public TMP_Text hostNameText;
    public TMP_Text playerCountText;
    public TMP_Text gameModeText;
    public Image lockIcon;
    public Button joinButton;
    public Image backgroundImage;

    [Header("Visual Settings")]
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.9f, 0.9f, 0.9f);
    public Color fullLobbyColor = new Color(0.8f, 0.8f, 0.8f);

    private string lobbyId;
    private bool isPrivate;
    private bool isFull;

    public void SetupLobbyItem(LobbyData lobbyData, System.Action<string> onJoinCallback)
    {
        lobbyId = lobbyData.lobbyId;
        isPrivate = lobbyData.isPrivate;
        isFull = lobbyData.isFull;

        // Set text displays
        if (lobbyNameText) lobbyNameText.text = lobbyData.lobbyName;
        if (hostNameText) hostNameText.text = "Host: " + lobbyData.hostName;
        if (playerCountText) playerCountText.text = $"{lobbyData.playerCount}/2";
        if (gameModeText) gameModeText.text = lobbyData.gameMode;

        // Show/hide lock icon
        if (lockIcon) lockIcon.gameObject.SetActive(isPrivate);

        // Setup join button
        if (joinButton)
        {
            joinButton.interactable = !isFull;
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => onJoinCallback?.Invoke(lobbyId));

            TMP_Text buttonText = joinButton.GetComponentInChildren<TMP_Text>();
            if (buttonText)
            {
                buttonText.text = isFull ? "Full" : "Join";
            }
        }

        // Set background color based on state
        if (backgroundImage)
        {
            backgroundImage.color = isFull ? fullLobbyColor : normalColor;
        }
    }

    public void OnPointerEnter()
    {
        if (!isFull && backgroundImage)
        {
            backgroundImage.color = hoverColor;
        }
    }

    public void OnPointerExit()
    {
        if (backgroundImage)
        {
            backgroundImage.color = isFull ? fullLobbyColor : normalColor;
        }
    }
}