using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class AlwaysShowPlaceholder : MonoBehaviour
{
    private TMP_InputField inputField;
    private TextMeshProUGUI placeholderText;

    void Start()
    {
        inputField = GetComponent<TMP_InputField>();
        if (inputField.placeholder != null)
        {
            placeholderText = inputField.placeholder.GetComponent<TextMeshProUGUI>();
        }
    }

    void Update()
    {
        // Always keep this InputField selected/focused
        if (EventSystem.current.currentSelectedGameObject != inputField.gameObject)
        {
            inputField.Select();
            inputField.ActivateInputField();
        }

        // Force placeholder to always be visible
        if (placeholderText != null)
        {
            placeholderText.gameObject.SetActive(true);
        }
    }
}