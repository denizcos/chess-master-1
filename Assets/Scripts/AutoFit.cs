using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(GridLayoutGroup))]
public class ResponsiveGrid8x8 : MonoBehaviour
{
    GridLayoutGroup grid;
    RectTransform rt;

    void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        rt   = GetComponent<RectTransform>();
        UpdateCells();
    }

    void OnRectTransformDimensionsChange()
    {
        // called when the panel size changes (screen resize, canvas scale, etc.)
        UpdateCells();
    }

    void UpdateCells()
    {
        if (!grid || !rt) return;

        // Panel size in pixels
        float w = rt.rect.width;
        float h = rt.rect.height;

        // Remove padding
        float innerW = w - grid.padding.left - grid.padding.right;
        float innerH = h - grid.padding.top  - grid.padding.bottom;

        // Spacing total across 7 gaps
        float totalSpaceX = grid.spacing.x * 7f;
        float totalSpaceY = grid.spacing.y * 7f;

        // Size per cell (fit in both axes, keep square)
        float cellW = (innerW - totalSpaceX) / 8f;
        float cellH = (innerH - totalSpaceY) / 8f;
        float cell  = Mathf.Floor(Mathf.Min(cellW, cellH));

        grid.cellSize = new Vector2(cell, cell);
    }
}
