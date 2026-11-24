using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace Eutopos.Capture
{
    public sealed class PointLayer : MonoBehaviour
    {
        const int MaxPoints = 20_000;
        static readonly Color32 PointColor = new Color32(153, 217, 200, 175);
        readonly Dictionary<ulong, Vector3> points = new Dictionary<ulong, Vector3>();
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<int> indices = new List<int>();
        ARPointCloudManager manager;
        Mesh mesh;
        MeshRenderer draw;
        bool dirty;
        float nextUpdate;

        public void Initialize(ARPointCloudManager manager, Material material)
        {
            this.manager = manager;
            mesh = new Mesh { name = "Titik ARCore capture" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            draw = gameObject.AddComponent<MeshRenderer>();
            draw.sharedMaterial = material;
            draw.shadowCastingMode = ShadowCastingMode.Off;
            draw.receiveShadows = false;
            draw.enabled = false;
            manager.trackablesChanged.AddListener(OnPoints);
        }

        public void SetVisible(bool visible)
        {
            if (draw == null) return;
            draw.enabled = visible;
            manager.enabled = visible;
        }

        public void Clear()
        {
            points.Clear();
            dirty = false;
            if (mesh != null) mesh.Clear();
        }

        void OnPoints(ARTrackablesChangedEventArgs<ARPointCloud> change)
        {
            if (!draw.enabled) return;
            foreach (var cloud in change.added) Read(cloud);
            foreach (var cloud in change.updated) Read(cloud);
        }

        void Read(ARPointCloud cloud)
        {
            var positions = cloud.positions;
            var identifiers = cloud.identifiers;
            if (!positions.HasValue || !identifiers.HasValue) return;
            var samples = positions.Value;
            var ids = identifiers.Value;
            int count = Mathf.Min(samples.Length, ids.Length);
            for (int i = 0; i < count; i++)
            {
                ulong id = ids[i];
                if (points.Count == MaxPoints && !points.ContainsKey(id)) continue;
                points[id] = cloud.transform.TransformPoint(samples[i]);
            }
            dirty = true;
        }

        void LateUpdate()
        {
            if (!dirty || !draw.enabled || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.2f;
            dirty = false;
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            foreach (var point in points.Values)
            {
                indices.Add(vertices.Count);
                vertices.Add(point);
                colors.Add(PointColor);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, MeshTopology.Points, 0);
        }

        void OnDestroy()
        {
            if (manager != null) manager.trackablesChanged.RemoveListener(OnPoints);
            if (mesh != null) Destroy(mesh);
        }
    }
}
