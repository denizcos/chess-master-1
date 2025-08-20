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

    void LateUpdate()
    {
        // Re-focus the input field after any potential deactivation (like after Enter press)
        if (inputField != null && !inputField.isFocused)
        {
            inputField.Select();
            inputField.ActivateInputField();
        }

        // Ensure placeholder stays visible even after text changes
        if (placeholderText != null && !placeholderText.gameObject.activeInHierarchy)
        {
            placeholderText.gameObject.SetActive(true);
        }
    }
}