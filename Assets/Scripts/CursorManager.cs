// CursorManager.cs  (drop-in replacement for yours)
using UnityEngine;
using System.Collections.Generic;

public class CursorManager : MonoBehaviour
{
    [System.Serializable]
    public class CursorData {
        public string name;
        public Texture2D texture;
        public Vector2 hotSpot = Vector2.zero;
    }

    public List<CursorData> cursors = new List<CursorData>();
    public string defaultCursorName = "Default";
    public bool forceSoftwareIfNeeded = true;

    Dictionary<string, CursorData> dict;

    void Awake() {
        dict = new Dictionary<string, CursorData>();
        foreach (var c in cursors) if (c != null && !string.IsNullOrEmpty(c.name)) dict[c.name] = c;
    }

    void Start() {
        ApplyDefault();
    }

    void OnEnable() {
        ApplyDefault();
    }

    void OnApplicationFocus(bool hasFocus) {
        if (hasFocus) ApplyDefault();   // re-apply when Game view regains focus (Editor quirk)
    }

    public void ApplyDefault() {
        SetCursor(defaultCursorName);
    }

    public void SetCursor(string name) {
        if (!dict.TryGetValue(name, out var c) || c.texture == null) {
            Debug.LogWarning($"Cursor not found or texture missing: {name}");
            return;
        }
        // Try hardware first
        Cursor.SetCursor(c.texture, c.hotSpot, CursorMode.Auto);

        // If it failed on some platforms/sizes, optionally force software
        if (forceSoftwareIfNeeded && Cursor.visible == false) {
            Cursor.visible = true;
        }
    }

    public void ResetCursor() {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}
