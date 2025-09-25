#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using System.Diagnostics;

public static class PostBuildStockfish
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.StandaloneOSX) return;

        string streamingAssetsInApp = Path.Combine(pathToBuiltProject, "Contents", "Resources", "Data", "StreamingAssets");

        string[] names = { "stockfish-mac", "stockfish-mac-x64", "stockfish-mac-arm64" };

        foreach (var name in names)
        {
            string full = Path.Combine(streamingAssetsInApp, name);
            if (File.Exists(full))
            {
                var psi = new ProcessStartInfo("/bin/chmod", $"+x \"{full}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                p.WaitForExit();
            }
        }

        UnityEngine.Debug.Log("✅ PostBuild: Stockfish marked executable (if present).");
    }
}
#endif
