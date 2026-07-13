using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Redshift.Editor
{
    /// <summary>
    /// CI locale minimale (SPEC §9 P0). Invoqué par build.ps1 en batchmode,
    /// éditeur fermé (le projet ne peut pas être ouvert deux fois).
    /// </summary>
    public static class BuildScript
    {
        public static void BuildWindows()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/SCN_Sandbox.unity" },
                locationPathName = "Builds/Windows/Redshift.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[BuildScript] Result={summary.result} Errors={summary.totalErrors} Warnings={summary.totalWarnings} Size={summary.totalSize} bytes");

            if (summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
