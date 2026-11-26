using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eutopos.Core.Tests
{
    public class PoiPointerAssetTests
    {
        const string ModelPath = "Assets/Eutopos/Models/MapPointer/map pointer.obj";
        const string MaterialPath = "Assets/Eutopos/Models/MapPointer/MapPointer.mat";

        [Test]
        public void ModelImportSettingsAndMeshAreValid()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            Assert.That(importer, Is.Not.Null, "ModelImporter not found for " + ModelPath);
            Assert.That(importer.addCollider, Is.False);
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None));
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.meshCompression, Is.EqualTo(ModelImporterMeshCompression.Low));

            Mesh mesh = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (obj is Mesh m)
                {
                    mesh = m;
                    break;
                }
            }

            Assert.That(mesh, Is.Not.Null, "Imported Mesh sub-asset not found");
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
            Assert.That(mesh.HasVertexAttribute(VertexAttribute.Normal), Is.True);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
            Debug.Log($"MAP_POINTER_MESH name={mesh.name} guid={guid} localId={localId} verts={mesh.vertexCount} min={mesh.bounds.min:F6} max={mesh.bounds.max:F6} size={mesh.bounds.size:F6}");
        }

        [Test]
        public void MaterialUsesSafeBuiltInShaderAndSourceRedColor()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.That(mat, Is.Not.Null, "Material not found at " + MaterialPath);
            Assert.That(mat.shader, Is.Not.Null);
            Assert.That(mat.shader.name, Is.EqualTo("Legacy Shaders/Diffuse"));
            Assert.That(mat.mainTexture, Is.Null);
            // baseColorFactor GLB sumber (linear 1,0; 0,024; 0,063) diubah ke sRGB karena proyek memakai ruang warna Gamma.
            Assert.That(mat.color.r, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(mat.color.g, Is.EqualTo(0.168f).Within(1e-3f));
            Assert.That(mat.color.b, Is.EqualTo(0.279f).Within(1e-3f));

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mat, out string guid, out long localId);
            Debug.Log($"MAP_POINTER_MAT guid={guid} localId={localId} shader={mat.shader.name}");
        }

        [Test]
        public void ResourcesPrefabHasExpectedSizeTipAndNoColliders()
        {
            var prefab = Resources.Load<GameObject>("MapPointer");
            Assert.That(prefab, Is.Not.Null, "Resources/MapPointer prefab missing");

            var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            try
            {
                Assert.That(instance.GetComponentsInChildren<Collider>(true), Is.Empty);

                var mf = instance.GetComponentInChildren<MeshFilter>();
                var mr = instance.GetComponentInChildren<MeshRenderer>();
                Assert.That(mf, Is.Not.Null);
                Assert.That(mf.sharedMesh, Is.Not.Null);
                Assert.That(mr, Is.Not.Null);
                Assert.That(mr.sharedMaterial, Is.Not.Null);
                Assert.That(mr.sharedMaterial.shader.name, Is.EqualTo("Legacy Shaders/Diffuse"));

                var b = mr.bounds;
                Debug.Log($"MAP_POINTER_PREFAB_BOUNDS min={b.min:F6} max={b.max:F6} size={b.size:F6}");
                Assert.That(b.size.y, Is.EqualTo(0.35f).Within(0.005f));
                Assert.That(b.min.y, Is.EqualTo(0f).Within(0.005f));
                Assert.That(b.max.y, Is.EqualTo(0.35f).Within(0.005f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
