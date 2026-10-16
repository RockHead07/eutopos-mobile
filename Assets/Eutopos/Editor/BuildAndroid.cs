using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Eutopos.EditorTools
{
    public static class BuildAndroid
    {
        [MenuItem("Eutopos/Build APK")]
        public static void Build()
        {
            ProjectSetup.ConfigurePlayer();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = "Builds/eutopos-mobile.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("build gagal: " + report.summary.result);
        }
    }
}
