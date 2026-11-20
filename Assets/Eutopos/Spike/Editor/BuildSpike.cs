using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace Eutopos.Spike.Editor
{
    public static class BuildSpike
    {
        const string ScenePath = "Assets/Eutopos/Spike/SpikeRecorder.unity";
        const string ApkPath = "Builds/eutopos-spike.apk";

        [MenuItem("Eutopos/Build Spike APK")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene();
            Directory.CreateDirectory("Builds");
            var android = NamedBuildTarget.Android;
            string originalId = PlayerSettings.GetApplicationIdentifier(android);
            bool originalBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                PlayerSettings.SetApplicationIdentifier(android, originalId + ".spike");
                EditorUserBuildSettings.buildAppBundle = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = ApkPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Build spike gagal: " + report.summary.result);
                Debug.Log($"Spike APK: {ApkPath}, {new FileInfo(ApkPath).Length} bytes");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = originalBundle;
                PlayerSettings.SetApplicationIdentifier(android, originalId);
                Debug.Log("Spike: Android package ID restored to " +
                    PlayerSettings.GetApplicationIdentifier(android));
            }
        }

        public static void CreateScene()
        {
            var previous = SceneManager.GetActiveScene();
            var selected = Selection.activeObject;
            // Batch mode starts with an untitled scene that cannot be kept open additively.
            bool additive = !Application.isBatchMode;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                additive ? NewSceneMode.Additive : NewSceneMode.Single);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (var menu in new[] { "GameObject/XR/AR Session", "GameObject/XR/XR Origin (Mobile AR)" })
                {
                    Selection.activeGameObject = null;
                    if (!EditorApplication.ExecuteMenuItem(menu))
                        throw new InvalidOperationException("Menu Unity tidak tersedia: " + menu);
                }

                var roots = scene.GetRootGameObjects();
                var recorder = new GameObject("Spike Recorder").AddComponent<SpikeRecorder>();
                var serialized = new SerializedObject(recorder);
                serialized.FindProperty("arSession").objectReferenceValue =
                    roots.Select(root => root.GetComponent<ARSession>()).Single(session => session != null);
                serialized.FindProperty("cameraManager").objectReferenceValue =
                    roots.Select(root => root.GetComponentInChildren<ARCameraManager>()).Single(camera => camera != null);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Scene spike gagal disimpan");
                Debug.Log("Spike scene: " + ScenePath);
            }
            finally
            {
                if (additive)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
                Selection.activeObject = selected;
            }
        }
    }
}
