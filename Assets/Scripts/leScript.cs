using UnityEngine;

public class LockBoardRect : MonoBehaviour
{
    public Vector2 size = new Vector2(800, 800);
    public Vector2 anchored = Vector2.zero;

    void OnEnable()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchored; // keep Y from drifting
    }
}
