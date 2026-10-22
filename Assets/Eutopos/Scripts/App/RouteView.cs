using System.Collections.Generic;
using Eutopos.Core;
using UnityEngine;

namespace Eutopos.App
{
    /// <summary>
    /// Rute sebagai deretan panah chevron di lantai (BuildingRoot, kerangka B). Posisi panah dihitung ChevronPlacer
    /// dari TUJUAN, jadi panah diam di dunia saat pengguna berjalan dan mengalir menuju tujuan. Kolam 12 objek yang
    /// berbagi satu Mesh dan satu material; tanpa aset 3D.
    /// </summary>
    public sealed class RouteView : MonoBehaviour
    {
        const float FloorY = 0.03f, SpacingM = 0.9f, LengthM = 0.5f, WidthM = 0.55f, ArmM = 0.18f;
        const float MaxAheadM = 8f, FadeM = 2f, PhaseSpeed = 0.6f, Follow = 10f;
        const int PoolSize = 12;
        static readonly Color Blue = new Color(0.18f, 0.44f, 0.87f);

        readonly Transform[] slots = new Transform[PoolSize];
        readonly MeshRenderer[] renderers = new MeshRenderer[PoolSize];
        readonly bool[] shown = new bool[PoolSize];
        readonly bool[] used = new bool[PoolSize];
        MaterialPropertyBlock block;
        IReadOnlyList<System.Numerics.Vector2> points;

        void Awake()
        {
            var mesh = BuildChevron();
            var material = new Material(Shader.Find("Sprites/Default"));
            block = new MaterialPropertyBlock();
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Chevron" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                go.SetActive(false);
                slots[i] = go.transform;
                renderers[i] = r;
            }
        }

        /// <summary>Chevron menghadap +Z lokal, rata di bidang XZ: dua lengan dari ujung ke sayap belakang.</summary>
        static Mesh BuildChevron()
        {
            float hl = LengthM / 2f, hw = WidthM / 2f, notchZ = hl - 0.28f;
            var v = new[]
            {
                new Vector3(0, 0, hl),             // 0 ujung
                new Vector3(hw, 0, -hl),           // 1 sayap kanan luar
                new Vector3(hw - ArmM, 0, -hl),    // 2 sayap kanan dalam
                new Vector3(0, 0, notchZ),         // 3 takik
                new Vector3(-(hw - ArmM), 0, -hl), // 4 sayap kiri dalam
                new Vector3(-hw, 0, -hl),          // 5 sayap kiri luar
            };
            var mesh = new Mesh { name = "Chevron", vertices = v };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5 };
            var colors = new Color32[v.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Show(Route route) => points = route.Points;

        public void Clear()
        {
            points = null;
            for (int i = 0; i < PoolSize; i++) Hide(i);
        }

        void Hide(int i)
        {
            if (!shown[i]) return;
            slots[i].gameObject.SetActive(false);
            shown[i] = false;
        }

        void Update()
        {
            if (points == null) return;
            float phase = (Time.time * PhaseSpeed) % SpacingM;
            var list = ChevronPlacer.Place(points, SpacingM, phase, MaxAheadM, FadeM);
            System.Array.Clear(used, 0, PoolSize);
            float k = 1f - Mathf.Exp(-Follow * Time.deltaTime);
            foreach (var c in list)
            {
                int slot = ((c.Index % PoolSize) + PoolSize) % PoolSize;
                var pos = new Vector3(c.Position.X, FloorY, c.Position.Y);
                var rot = Quaternion.LookRotation(new Vector3(c.Direction.X, 0f, c.Direction.Y), Vector3.up);
                var t = slots[slot];
                if (!shown[slot])
                {
                    t.gameObject.SetActive(true);
                    t.localPosition = pos;
                    t.localRotation = rot;
                    shown[slot] = true;
                }
                else
                {
                    // rute dihitung ulang tiap pengguna bergerak: dihaluskan supaya panah tidak melompat
                    t.localPosition = Vector3.Lerp(t.localPosition, pos, k);
                    t.localRotation = Quaternion.Slerp(t.localRotation, rot, k);
                }
                block.SetColor("_Color", new Color(Blue.r, Blue.g, Blue.b, 0.9f * c.Alpha));
                renderers[slot].SetPropertyBlock(block);
                used[slot] = true;
            }
            for (int i = 0; i < PoolSize; i++) if (!used[i]) Hide(i);
        }
    }
}
