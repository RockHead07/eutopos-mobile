using Eutopos.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Eutopos.App
{
    /// <summary>
    /// Pin POI melayang untuk target yang dipilih: model 3D penunjuk peta oranye (jatuh balik ke bola primitif bila
    /// aset gagal dimuat) di atas titik ruangan dengan garis tipis ke lantai, dan label (nama dan jarak) yang selalu
    /// menghadap kamera. Dipasang pada BuildingRoot, jadi posisinya mengikuti penyelarasan.
    /// </summary>
    public sealed class PoiPin : MonoBehaviour
    {
        const string PointerResource = "MapPointer";
        // Ujung penunjuk 1,15 m di atas lantai: badannya (0,35 m) berada setinggi mata (+-1,3 m), jadi tampil di
        // tengah layar saat ponsel dipegang biasa. Tanpa garis ke lantai, melayang saja.
        const float HeightM = 1.15f, PointerHeightM = 0.35f, BallM = 0.2f, BobM = 0.04f, BobHz = 0.8f;
        const float TextEverySeconds = 0.2f;
        const float PointerLabelClearanceM = 0.23f, FallbackLabelClearanceM = 0.18f;
        const float ScalePerMeter = 0.0005f, MinScale = 0.0008f, MaxScale = 0.004f;
        static readonly Color Orange = new Color(0.878f, 0.478f, 0.18f);

        Camera cam;
        GameObject pinRoot;
        Transform marker, labelRoot;
        Text label;
        Vector3 basePos;
        string shownId;
        float lastTextT, labelLiftM;
        bool built;

        public void Init(Camera arCamera)
        {
            cam = arCamera;
            pinRoot = new GameObject("PoiPin");
            pinRoot.transform.SetParent(transform, false);

            marker = BuildMarker();

            var canvasGo = new GameObject("Label", typeof(Canvas));
            labelRoot = canvasGo.transform;
            labelRoot.SetParent(pinRoot.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            ((RectTransform)labelRoot).sizeDelta = new Vector2(420, 110);

            var bg = new GameObject("Bg", typeof(Image));
            bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            var bgRt = (RectTransform)bg.transform;
            bgRt.SetParent(labelRoot, false);
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

            var textGo = new GameObject("Text", typeof(Text));
            label = textGo.GetComponent<Text>();
            var rt = label.rectTransform;
            rt.SetParent(labelRoot, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 44;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            pinRoot.SetActive(false);
            built = true;
        }

        Transform BuildMarker()
        {
            var prefab = Resources.Load<GameObject>(PointerResource);
            if (prefab != null)
            {
                var prefabMf = prefab.GetComponentInChildren<MeshFilter>(true);
                var prefabMr = prefab.GetComponentInChildren<Renderer>(true);
                float scale = prefabMf != null && prefabMf.sharedMesh != null
                    ? PoiPointerLayout.ScaleForHeight(prefabMf.sharedMesh.bounds.size.y, PointerHeightM)
                    : 0f;
                bool hasMaterial = prefabMr != null && prefabMr.sharedMaterial != null
                    && prefabMr.sharedMaterial.shader != null
                    && prefabMr.sharedMaterial.shader.name != "Hidden/InternalErrorShader";
                if (scale > 0f && hasMaterial)
                {
                    var go = Instantiate(prefab, pinRoot.transform, false);
                    foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
                    var mf = go.GetComponentInChildren<MeshFilter>(true);
                    var b = mf.sharedMesh.bounds;
                    if (mf.transform != go.transform)
                    {
                        mf.transform.localRotation = Quaternion.identity;
                        mf.transform.localScale = Vector3.one * scale;
                        mf.transform.localPosition = new Vector3(0f, PoiPointerLayout.TipLocalOffsetY(b.min.y, scale), 0f);
                    }
                    labelLiftM = PoiPointerLayout.LabelLiftForHeight(PointerHeightM, PointerLabelClearanceM);
                    return go.transform;
                }
            }

            Debug.LogWarning("PoiPin: aset 3D MapPointer gagal dimuat, kembali ke bola primitif.");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            var t = sphere.transform;
            t.SetParent(pinRoot.transform, false);
            t.localScale = Vector3.one * BallM;
            sphere.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { color = Orange };
            labelLiftM = PoiPointerLayout.LabelLiftForHeight(BallM * 0.5f, FallbackLabelClearanceM);
            return t;
        }

        /// <summary>Panggil tiap frame selama pin boleh tampil; teksnya diperbarui paling cepat 5 kali per detik.</summary>
        public void Show(WalkTarget target, float distanceM)
        {
            if (!built) return;
            basePos = new Vector3(target.Position.X, HeightM, target.Position.Y);
            if (!pinRoot.activeSelf) pinRoot.SetActive(true);
            if (shownId != target.Id || Time.time - lastTextT >= TextEverySeconds)
            {
                label.text = PoiLabel.Format(target.Name, distanceM);
                shownId = target.Id;
                lastTextT = Time.time;
            }
        }

        public void Hide()
        {
            if (built && pinRoot.activeSelf) pinRoot.SetActive(false);
        }

        void LateUpdate()
        {
            if (!built || !pinRoot.activeSelf) return;
            var up = Vector3.up * (Mathf.Sin(Time.time * Mathf.PI * 2f * BobHz) * BobM);
            marker.localPosition = basePos + up;
            labelRoot.localPosition = marker.localPosition + Vector3.up * labelLiftM;

            var toCam = cam.transform.position - labelRoot.position;
            float dist = toCam.magnitude;
            toCam.y = 0f; // hanya yaw: penunjuk dan label tetap tegak dan selalu menghadap pengguna
            if (toCam.sqrMagnitude > 1e-6f)
                marker.rotation = labelRoot.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
            labelRoot.localScale = Vector3.one * Mathf.Clamp(dist * ScalePerMeter, MinScale, MaxScale);
        }
    }
}
