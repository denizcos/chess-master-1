using UnityEngine;
using UnityEngine.EventSystems;

public class DropdownHoverSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public void OnPointerEnter(PointerEventData _) {
        if (UIButtonHoverSound.Instance) UIButtonHoverSound.Instance.PlayHover1();
    }
    public void OnSelect(BaseEventData _) {
        if (UIButtonHoverSound.Instance) UIButtonHoverSound.Instance.PlayHover1(); // keyboard nav
    }
}
