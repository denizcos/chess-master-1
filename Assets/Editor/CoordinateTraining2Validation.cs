using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Batch regression check: -batchmode -executeMethod CoordinateTraining2Validation.RunBatch
public static class CoordinateTraining2Validation
{
    private const string Running = "CoordinateTraining2Validation.Running";
    private static IEnumerator checks;
    private static double nextTick;
    private static double deadline;

    public static void RunBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Running, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Running, false)) return;
        deadline = EditorApplication.timeSinceStartup + 90;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play mode test timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < nextTick) return;
            if (checks == null) checks = CheckMode();
            if (!checks.MoveNext())
            {
                Debug.Log("COORDINATE_TRAINING_2_PASS: scene, 64 coordinates, input, feedback, perspective, menu return and re-entry");
                Finish(0);
                return;
            }
            nextTick = EditorApplication.timeSinceStartup + (checks.Current is float delay ? delay : 0.1f);
        }
        catch (Exception e)
        {
            Debug.LogError("COORDINATE_TRAINING_2_FAIL: " + e);
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(code);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static int Target(CoordinateTraining2Mode mode) => (int)typeof(CoordinateTraining2Mode)
        .GetField("target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(mode);

    private static IEnumerator CheckMode()
    {
        yield return 0.5f;
        for (int i = 0; i < 64; i++)
        {
            string coord = CoordinateTraining2Mode.Coordinate(i);
            Require(CoordinateTraining2Mode.TryParseCoordinate(" " + coord.ToUpperInvariant() + " ", out int parsed) && parsed == i, "Coordinate round trip: " + coord);
        }
        foreach (string invalid in new[] { "", "a", "a0", "a9", "i4", "11", "a11", "4a", "zz" })
            Require(!CoordinateTraining2Mode.TryParseCoordinate(invalid, out _), "Invalid coordinate accepted: " + invalid);

        var menu = UnityEngine.Object.FindFirstObjectByType<InputBasedMainMenuController>();
        Require(menu != null && menu.mainMenuPanel.activeInHierarchy, "Main menu missing");
        var mode = menu.coordinateTraining2Mode.modePanel.GetComponent<CoordinateTraining2Mode>();
        Require(mode != null && !mode.gameObject.activeSelf, "Mode scene binding missing");
        Require(mode.answerInput.GetComponent<KeyboardClackSoundRandom>() != null, "Keyboard sounds missing");
        Require(UIButtonHoverSound.Instance != null && UIButtonHoverSound.Instance.clackClips.Length > 0, "Shared audio missing");
        menu.modeInputField.text = "b2";
        menu.modeInputField.onSubmit.Invoke("b2");
        yield return 0.3f;
        Require(mode.isActiveAndEnabled && !menu.mainMenuPanel.activeInHierarchy, "b2 did not open mode");
        Require(mode.boardParent.childCount == 64 && mode.scoreText.text == "0", "Board/session initialization");
        Require(mode.boardParent.GetChild(0).name == "a8", "White orientation");
        int first = Target(mode);
        var settings = SettingsManager.Instance;
        settings.OpenSettings();
        mode.answerInput.onSubmit.Invoke(CoordinateTraining2Mode.Coordinate(first));
        Require(mode.scoreText.text == "0" && Target(mode) == first, "Settings did not block answers");
        settings.CloseSettings();
        var square = mode.boardParent.Find(CoordinateTraining2Mode.Coordinate(first)).GetComponent<Image>();
        Color before = square.color;
        yield return mode.blinkInterval;
        Require(square.color != before, "Target is not blinking");
        mode.answerInput.onSubmit.Invoke("zz");
        Require(mode.scoreText.text == "0" && Target(mode) == first && square.color == Color.red, "Wrong answer behavior");
        yield return mode.feedbackDuration + 0.15f;
        mode.perspectiveButton.onClick.Invoke();
        Require(mode.boardParent.GetChild(0).name == "h1" && Target(mode) == first, "Black orientation changed coordinate");
        string answer = CoordinateTraining2Mode.Coordinate(first).ToUpperInvariant();
        mode.answerInput.onSubmit.Invoke(answer);
        mode.answerInput.onSubmit.Invoke(answer);
        Require(mode.scoreText.text == "1", "Correct answer should score once");
        yield return mode.feedbackDuration + 0.15f;
        Require(Target(mode) != first, "Next target repeats");
        mode.answerInput.onSubmit.Invoke(CoordinateTraining2Mode.Coordinate(Target(mode)));
        // Leave during pending feedback, as a player can do via the copied menu button.
        var buttons = mode.GetComponentsInChildren<Button>();
        foreach (var button in buttons)
            if (button != mode.perspectiveButton) { button.onClick.Invoke(); break; }
        Require(!mode.gameObject.activeInHierarchy && menu.mainMenuPanel.activeInHierarchy, "Return button did not restore menu");
        yield return mode.feedbackDuration + 0.15f;
        menu.modeInputField.onSubmit.Invoke("b2");
        yield return 0.3f;
        Require(mode.boardParent.childCount == 64 && mode.scoreText.text == "0" && mode.answerInput.text == "", "Re-entry did not reset cleanly");
        UnityEngine.Object.FindFirstObjectByType<CanvasUIManager>().ReturnToMainMenu();
        menu.modeInputField.onSubmit.Invoke("a1");
        yield return 0.3f;
        Require(menu.coordinateMode.modePanel.activeInHierarchy && !mode.gameObject.activeInHierarchy, "Original a1 navigation regressed");
    }
}
