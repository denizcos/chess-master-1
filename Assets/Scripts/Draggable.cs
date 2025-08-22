using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform targetPanel;   // the panel we want to move
    private Canvas canvas;
    private Vector2 originalLocalPointerPosition;
    private Vector2 originalPanelLocalPosition;

    void Awake()
    {
        // Move the parent (the full settings panel), not just the header
        targetPanel = transform.parent.GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetPanel.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out originalLocalPointerPosition
        );

        originalPanelLocalPosition = targetPanel.localPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (targetPanel == null || canvas == null) return;

        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetPanel.parent as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPointerPosition))
        {
            Vector2 offset = localPointerPosition - originalLocalPointerPosition;
            targetPanel.localPosition = originalPanelLocalPosition + offset;
        }
    }
}
