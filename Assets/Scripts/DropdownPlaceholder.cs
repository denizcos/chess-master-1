using UnityEngine;
using TMPro;
public class DropdownPlaceholder : MonoBehaviour
{
    private TMP_Dropdown dropdown;
    void Start()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        dropdown.value = -1;
    }
    void Update()
    {
        // Always show placeholder when no selection
        if (dropdown.value == -1)
        {
            dropdown.captionText.text = "Choose difficulty";
        }
    }
}