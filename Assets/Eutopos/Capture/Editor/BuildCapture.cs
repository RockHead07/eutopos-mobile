using System;
using System.IO;
using System.Linq;
using Eutopos.App;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Eutopos.Capture.EditorTools
{
    public static class BuildCapture
    {
        const string ScenePath = "Assets/Eutopos/Capture/Capture.unity";
        const string ApkPath = "Builds/eutopos-capture.apk";
        const string ShaderPath = "Assets/Eutopos/Capture/OverlayGeometry.shader";
        const string MaterialPath = "Assets/Eutopos/Capture/OverlayGeometry.mat";

        [MenuItem("Eutopos/Build Capture APK")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene();
            Directory.CreateDirectory("Builds");
            var android = NamedBuildTarget.Android;
            string originalId = PlayerSettings.GetApplicationIdentifier(android);
            string originalName = PlayerSettings.productName;
            bool originalBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                PlayerSettings.SetApplicationIdentifier(android, originalId + ".capture");
                PlayerSettings.productName = "Eutopos Capture";
                EditorUserBuildSettings.buildAppBundle = false;
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = ApkPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                    extraScriptingDefines = new[] { "EUTOPOS_CAPTURE" },
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException("Build capture gagal: " + report.summary.result);
                Debug.Log($"Capture APK: {ApkPath}, {new FileInfo(ApkPath).Length} byte");
            }
            finally
            {
                EditorUserBuildSettings.buildAppBundle = originalBundle;
                PlayerSettings.productName = originalName;
                PlayerSettings.SetApplicationIdentifier(android, originalId);
                AssetDatabase.SaveAssets();
            }
        }

        public static void CreateScene()
        {
            var previous = SceneManager.GetActiveScene();
            var selected = Selection.activeObject;
            bool additive = !Application.isBatchMode;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                additive ? NewSceneMode.Additive : NewSceneMode.Single);
            try
            {
                SceneManager.SetActiveScene(scene);
                foreach (string menu in new[] { "GameObject/XR/AR Session", "GameObject/XR/XR Origin (Mobile AR)" })
                {
                    Selection.activeObject = null;
                    if (!EditorApplication.ExecuteMenuItem(menu))
                        throw new InvalidOperationException("Menu Unity tidak tersedia: " + menu);
                }

                var roots = scene.GetRootGameObjects();
                var session = roots.Select(root => root.GetComponent<ARSession>()).Single(x => x != null);
                var camera = roots.Select(root => root.GetComponentInChildren<ARCameraManager>()).Single(x => x != null);
                var origin = camera.transform.root.gameObject;
                var points = origin.AddComponent<ARPointCloudManager>();
                points.enabled = false;

                var canvas = new GameObject("Capture Canvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(720, 1280);
                scaler.matchWidthOrHeight = 0.5f;
                new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));

                var controller = new GameObject("Capture Controller");
                var frameCapture = controller.AddComponent<FrameCapture>();
                var settings = new SerializedObject(controller.AddComponent<CaptureController>());
                settings.FindProperty("arSession").objectReferenceValue = session;
                settings.FindProperty("cameraManager").objectReferenceValue = camera;
                settings.FindProperty("pointCloudManager").objectReferenceValue = points;
                settings.FindProperty("frameCapture").objectReferenceValue = frameCapture;
                settings.FindProperty("canvas").objectReferenceValue = canvas;
                settings.FindProperty("geometryMaterial").objectReferenceValue = GeometryMaterial();
                settings.ApplyModifiedPropertiesWithoutUndo();

                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new IOException("Scene capture gagal disimpan.");
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

        static Material GeometryMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new IOException("Shader visualisasi capture tidak tersedia.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
