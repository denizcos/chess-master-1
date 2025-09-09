using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class CursorManager : MonoBehaviour
{
    [System.Serializable]
    public class CursorData
    {
        public string name;
        public Texture2D texture;
        public Vector2 hotSpot = Vector2.zero;
    }

    public List<CursorData> cursors = new List<CursorData>();
    public string defaultCursorName = "Default";

    private Dictionary<string, CursorData> dict;

    void Awake()
    {
        dict = new Dictionary<string, CursorData>();
        foreach (var c in cursors)
        {
            if (c != null && !string.IsNullOrEmpty(c.name))
                dict[c.name] = c;
        }

        SceneManager.activeSceneChanged += (_, __) => ApplyDefault();
    }

    void Start() => ApplyDefault();

    void OnEnable() => ApplyDefault();

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ApplyDefault();
    }

    public void ApplyDefault() => SetCursor(defaultCursorName);

    public void SetCursor(string name)
    {
        if (!dict.TryGetValue(name, out var c) || c.texture == null)
        {
            Debug.LogWarning($"Cursor not found or texture missing: {name}");
            return;
        }

        // Force software mode to avoid OS gamma/size issues
        Cursor.SetCursor(c.texture, c.hotSpot, CursorMode.ForceSoftware);

        // Ensure cursor is visible and unlocked
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ResetCursor()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.ForceSoftware);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
