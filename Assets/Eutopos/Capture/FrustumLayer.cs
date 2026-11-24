using System.Collections.Generic;
using Eutopos.Core.Capture;
using UnityEngine;
using UnityEngine.Rendering;

namespace Eutopos.Capture
{
    public sealed class FrustumLayer : MonoBehaviour
    {
        const float Depth = 0.15f;
        static readonly Color32 Green = new Color32(102, 224, 152, 255);
        static readonly Color32 Orange = new Color32(255, 160, 72, 255);
        static readonly Color32 Yellow = new Color32(255, 229, 110, 255);
        static readonly Color32 Blue = new Color32(103, 190, 255, 255);
        static readonly int[] Edges = { 0, 1, 0, 2, 0, 3, 0, 4, 1, 2, 2, 3, 3, 4, 4, 1 };

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<int> indices = new List<int>();
        Mesh mesh;

        public void Initialize(Material material)
        {
            mesh = new Mesh { name = "Bingkai foto capture" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var draw = gameObject.AddComponent<MeshRenderer>();
            draw.sharedMaterial = material;
            draw.shadowCastingMode = ShadowCastingMode.Off;
            draw.receiveShadows = false;
        }

        public void Rebuild(IReadOnlyList<FrameRecord> mapping, IReadOnlyList<FrameRecord> queries)
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            for (int i = 0; i < mapping.Count; i++)
            {
                var photo = mapping[i];
                Color32 color = i == mapping.Count - 1 ? Yellow : photo.Segment == 0 ? Green : Orange;
                Add(photo, color);
            }
            foreach (var photo in queries) Add(photo, Blue);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
        }

        void Add(FrameRecord photo, Color32 color)
        {
            var k = photo.K;
            if (k.Fx <= 0 || k.Fy <= 0 || k.Width <= 0 || k.Height <= 0) return;
            var p = new Vector3(photo.Position.X, photo.Position.Y, photo.Position.Z);
            var q = new Quaternion(photo.Rotation.X, photo.Rotation.Y, photo.Rotation.Z, photo.Rotation.W);
            float left = -k.Cx * Depth / k.Fx, right = (k.Width - k.Cx) * Depth / k.Fx;
            float top = k.Cy * Depth / k.Fy, bottom = (k.Cy - k.Height) * Depth / k.Fy;
            int start = vertices.Count;
            vertices.Add(p);
            vertices.Add(p + q * new Vector3(left, top, Depth));
            vertices.Add(p + q * new Vector3(right, top, Depth));
            vertices.Add(p + q * new Vector3(right, bottom, Depth));
            vertices.Add(p + q * new Vector3(left, bottom, Depth));
            for (int i = 0; i < 5; i++) colors.Add(color);
            foreach (int edge in Edges) indices.Add(start + edge);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
