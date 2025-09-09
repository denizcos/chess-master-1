using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_InputField))]
public class KeyboardClackSoundRandom : MonoBehaviour
{
    private TMP_InputField inputField;
    private int lastTextLength;
    private bool isBulkInsertFrame;

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();

        inputField.onValidateInput += OnValidateChar;
        inputField.onValueChanged.AddListener(OnValueChanged);

        lastTextLength = inputField.text.Length;
    }

    private char OnValidateChar(string currentText, int charIndex, char addedChar)
    {
        // Only play clacks if not in bulk paste mode
        if (!isBulkInsertFrame)
        {
            UIButtonHoverSound.Instance?.PlayRandomClack();
        }
        return addedChar;
    }

    private void OnValueChanged(string newText)
    {
        int newLen = newText.Length;
        int delta = newLen - lastTextLength;

        if (delta < 0)
        {
            // Deleted chars  single delete sound
            UIButtonHoverSound.Instance?.PlayDelete();
        }
        else if (delta > 1)
        {
            // Bulk insert (paste)  mark as bulk frame, play one clack
            isBulkInsertFrame = true;
            UIButtonHoverSound.Instance?.PlayRandomClack();
        }
        else
        {
            isBulkInsertFrame = false;
        }

        lastTextLength = newLen;
    }

    private void LateUpdate()
    {
        // Reset bulk flag at end of frame so it doesn’t block next keystroke
        isBulkInsertFrame = false;
    }

    private void OnDestroy()
    {
        inputField.onValueChanged.RemoveListener(OnValueChanged);
        inputField.onValidateInput -= OnValidateChar;
    }
}
