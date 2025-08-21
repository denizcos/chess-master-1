using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class PieceResizer : MonoBehaviour
{
    [Range(0.1f, 1.0f)]
    public float fillPercent = 0.9f;   //  control this in Inspector (90% of the square by default)

    [Min(0f)]
    public float pixelPadding = 0f;    // optional extra padding inside the square

    RectTransform rt;
    RectTransform parentSquare;

    void OnEnable()
    {
        rt = (RectTransform)transform;
        parentSquare = transform.parent as RectTransform;

        // ensure centered anchors/pivot (so it stays centered while resizing)
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Resize();
    }

    void OnRectTransformDimensionsChange() => Resize();
    void OnTransformParentChanged()         => OnEnable(); // rebind parent & resize

    void Resize()
    {
        if (!rt || !parentSquare) return;

        float baseSize = Mathf.Min(parentSquare.rect.width, parentSquare.rect.height);
        float target = Mathf.Max(0f, (baseSize * fillPercent) - (2f * pixelPadding));

        rt.sizeDelta = new Vector2(target, target);
        rt.anchoredPosition = Vector2.zero;
    }
}
