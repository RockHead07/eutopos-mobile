using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace Eutopos.EditorTools
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Eutopos/Scenes/Main.unity";

        [MenuItem("Eutopos/Atur Player Android")]
        public static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "RockHead07";
            PlayerSettings.productName = "eutopos-mobile";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.rockhead07.eutopos");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // Active Input Handling = Input System (1). Tidak ada API publik untuk ini.
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            var so = new SerializedObject(asset);
            so.FindProperty("activeInputHandler").intValue = 1;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("eutopos: pengaturan Player Android diterapkan");
        }

        /// <summary>Langkah klik di XR Plug-in Management, sebagai kode supaya bisa diulang dan diperiksa.</summary>
        [MenuItem("Eutopos/Aktifkan ARCore")]
        public static void ConfigureXR()
        {
            var perTarget = GetOrCreatePerTarget();
            if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var manager = perTarget.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android)
                && !XRPackageMetadataStore.IsLoaderAssigned("UnityEngine.XR.ARCore.ARCoreLoader", BuildTargetGroup.Android))
                throw new Exception("ARCoreLoader gagal dipasang untuk Android");

            var arcore = ARCoreSettings.GetOrCreateSettings();
            arcore.requirement = ARCoreSettings.Requirement.Required;
            arcore.depth = ARCoreSettings.Requirement.Optional;
            EditorUtility.SetDirty(arcore);
            AssetDatabase.SaveAssets();
            Debug.Log("eutopos: ARCore aktif untuk Android (Required, depth Optional)");
        }

        static XRGeneralSettingsPerBuildTarget GetOrCreatePerTarget()
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget))
            {
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            return perTarget;
        }

        /// <summary>Scene: AR Session, XR Origin (Mobile AR) dengan ARAnchorManager, dan objek App kosong.</summary>
        [MenuItem("Eutopos/Buat Scene Main")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            UnityEngine.Object.DestroyImmediate(GameObject.Find("Main Camera"));
            foreach (var item in new[] { "GameObject/XR/AR Session", "GameObject/XR/XR Origin (Mobile AR)" })
            {
                Selection.activeGameObject = null; // dibuat di akar scene
                if (!EditorApplication.ExecuteMenuItem(item)) throw new Exception("menu tidak ada: " + item);
            }
            GameObject.Find("XR Origin").AddComponent<UnityEngine.XR.ARFoundation.ARAnchorManager>();
            var app = new GameObject("App");
            // Lewat nama tipe: assembly editor tidak bergantung pada Eutopos.App (yang belum ada di Task 7).
            var controller = Type.GetType("Eutopos.App.AppController, Eutopos.App");
            if (controller != null) app.AddComponent(controller);
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("scene gagal disimpan");
            Debug.Log("eutopos: scene disimpan di " + ScenePath);
        }

        /// <summary>Batch: semua pengaturan sekali jalan (pengganti langkah klik Task 7 Step 2).</summary>
        public static void SetupAll()
        {
            ConfigurePlayer();
            ConfigureXR();
            CreateScene();
        }
    }
}
