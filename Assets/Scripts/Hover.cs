using UnityEngine;
using UnityEngine.EventSystems;

public class HoverPlaySound : MonoBehaviour, IPointerEnterHandler
{
    // Called automatically by Unity when the mouse enters this object
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (UIButtonHoverSound.Instance != null)
        {
            UIButtonHoverSound.Instance.PlayHover1();
        }
    }
}
