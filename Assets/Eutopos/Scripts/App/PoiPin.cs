using Eutopos.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Eutopos.App
{
    /// <summary>
    /// Pin POI melayang untuk target yang dipilih: bola oranye di atas titik ruangan dengan garis tipis ke lantai,
    /// dan label (nama dan jarak) yang selalu menghadap kamera. Dipasang pada BuildingRoot, jadi posisinya
    /// mengikuti penyelarasan. Hanya primitif dan satu Canvas world space; tanpa aset 3D.
    /// </summary>
    public sealed class PoiPin : MonoBehaviour
    {
        const float HeightM = 1.5f, BallM = 0.2f, BobM = 0.04f, BobHz = 0.8f, TextEverySeconds = 0.2f;
        const float LabelLiftM = 0.28f, ScalePerMeter = 0.0005f, MinScale = 0.0008f, MaxScale = 0.004f;
        static readonly Color Orange = new Color(0.878f, 0.478f, 0.18f);

        Camera cam;
        GameObject pinRoot;
        Transform ball, labelRoot;
        LineRenderer stem;
        Text label;
        Vector3 basePos;
        string shownId;
        float lastTextT;
        bool built;

        public void Init(Camera arCamera)
        {
            cam = arCamera;
            pinRoot = new GameObject("PoiPin");
            pinRoot.transform.SetParent(transform, false);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sphere.GetComponent<Collider>());
            ball = sphere.transform;
            ball.SetParent(pinRoot.transform, false);
            ball.localScale = Vector3.one * BallM;
            sphere.GetComponent<Renderer>().material = new Material(Shader.Find("Sprites/Default")) { color = Orange };

            var stemGo = new GameObject("Stem");
            stemGo.transform.SetParent(pinRoot.transform, false);
            stem = stemGo.AddComponent<LineRenderer>();
            stem.useWorldSpace = false;
            stem.widthMultiplier = 0.015f;
            stem.positionCount = 2;
            stem.material = new Material(Shader.Find("Sprites/Default"));
            stem.startColor = stem.endColor = new Color(Orange.r, Orange.g, Orange.b, 0.7f);

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
            ball.localPosition = basePos + up;
            stem.SetPosition(0, new Vector3(basePos.x, 0.02f, basePos.z));
            stem.SetPosition(1, ball.localPosition);

            labelRoot.localPosition = ball.localPosition + Vector3.up * LabelLiftM;
            var toCam = cam.transform.position - labelRoot.position;
            float dist = toCam.magnitude;
            toCam.y = 0f; // hanya yaw, label tetap tegak
            if (toCam.sqrMagnitude > 1e-6f) labelRoot.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
            labelRoot.localScale = Vector3.one * Mathf.Clamp(dist * ScalePerMeter, MinScale, MaxScale);
        }
    }
}
