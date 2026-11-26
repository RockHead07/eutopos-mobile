using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Eutopos.EditorTools
{
    public static class BuildAndroid
    {
        // Ikon pribadi opsional (diabaikan git). Dipasang hanya selama build lalu dikembalikan, supaya
        // ProjectSettings yang di-commit tidak pernah merujuk aset yang tidak ada di repo.
        static readonly string[] LocalIconPaths =
        {
            "Assets/Eutopos/Icon/app-icon.local.png",
            "Assets/Eutopos/Icon/app-icon.local.jpg",
        };

        [MenuItem("Eutopos/Build APK")]
        public static void Build()
        {
            ProjectSetup.ConfigurePlayer();
            Texture2D icon = null;
            foreach (var path in LocalIconPaths)
                if ((icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path)) != null) break;
            var previous = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            try
            {
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
            finally
            {
                if (icon != null)
                {
                    PlayerSettings.SetIcons(NamedBuildTarget.Unknown, previous, IconKind.Any);
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
