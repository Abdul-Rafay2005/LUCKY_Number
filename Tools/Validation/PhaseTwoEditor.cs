using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class PhaseTwoEditor
{
    public static void BuildPresentation()
    {
        LuckyNumber.Editor.LuckyNumberSetupEditor.ApplyMasterScenePresentation();
        Build();
    }

    public static void Build()
    {
        var args = Environment.GetCommandLineArgs();
        var output = args[Array.IndexOf(args, "-phase2BuildPath") + 1];
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CompressWithLz4
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Validation player build failed: " + report.summary.result);
        Debug.Log($"[PHASE2] Build succeeded with {report.summary.totalErrors} errors and {report.summary.totalWarnings} warnings.");
    }
}
