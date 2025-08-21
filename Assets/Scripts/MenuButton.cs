using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal; // for Light2D

public class CanvasUIManager : MonoBehaviour
{
    [Header("Assign your main Canvas here")]
    public GameObject mainCanvas;

    [Header("Assign the UI panels for Main Menu state")]
    public GameObject[] mainMenuObjects;

    [Header("Optional: extra root objects to keep enabled")]
    public GameObject[] extraExclusions;

    // Call this from any button
    public void ReturnToMainMenu()
    {
        if (mainCanvas == null)
        {
            Debug.LogError("Main Canvas not assigned!");
            return;
        }

        // 1) Hide everything under the canvas
        foreach (Transform child in mainCanvas.transform)
        {
            child.gameObject.SetActive(false);
        }

        // 2) Show only the main menu UI you specify
        if (mainMenuObjects != null)
        {
            foreach (GameObject menuObj in mainMenuObjects)
            {
                if (menuObj != null) menuObj.SetActive(true);
            }
        }

        // 3) Disable everything OUTSIDE the canvas except:
        //    - Main Camera (tagged MainCamera)
        //    - Global Light 2D
        //    - EventSystem
        //    - Anything you add to extraExclusions
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in roots)
        {
            if (root == null) continue;

            // Keep the canvas root
            if (root == mainCanvas) continue;

            // Keep exclusions (camera/global light/eventsyst/extra)
            if (IsExcludedRoot(root)) continue;

            // Otherwise, disable the whole root (and its children)
            root.SetActive(false);
        }
    }

    private bool IsExcludedRoot(GameObject root)
    {
        // Keep EventSystem roots
        if (root.GetComponentInChildren<EventSystem>(true) != null)
            return true;

        // Keep Main Camera roots (checks any Camera underneath tagged as MainCamera)
        var cam = root.GetComponentInChildren<Camera>(true);
        if (cam != null && cam.CompareTag("MainCamera"))
            return true;

        // Keep any root that contains a Global Light 2D
        var lights = root.GetComponentsInChildren<Light2D>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].lightType == Light2D.LightType.Global)
                return true;
        }

        // Keep any extra exclusions you specify
        if (extraExclusions != null)
        {
            for (int i = 0; i < extraExclusions.Length; i++)
                if (extraExclusions[i] == root) return true;
        }

        return false;
    }
}
