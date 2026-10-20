using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Eutopos.App
{
    /// <summary>UI dibangun dari kode supaya scene tetap minimal.</summary>
    public sealed class AppUi
    {
        public Text Logo, Status, Banner, TargetLabel;
        public Button LocalizeButton, NavButton, PrevTarget, NextTarget;
        public RawImage MiniMap;
        public GameObject DebugRoot;
        public Text DebugInterval, DebugJump, DebugPoint, DebugInfo;

        static Font font;
        static Font F => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static AppUi Build(AppController app)
        {
            var ui = new AppUi();
            var canvas = NewCanvas();

            var top = Panel(canvas, new Vector2(0, 0.9f), new Vector2(1, 1), new Color(0, 0, 0, 0.55f));
            ui.Logo = Label(top, "eutopos", 44, TextAnchor.MiddleLeft, new Vector2(0.03f, 0), new Vector2(0.3f, 1));
            var logoButton = ui.Logo.gameObject.AddComponent<Button>();
            logoButton.onClick.AddListener(app.OnLogoTapped);
            ui.Status = Label(top, "", 28, TextAnchor.MiddleLeft, new Vector2(0.3f, 0), new Vector2(0.98f, 1));

            ui.MiniMap = new GameObject("MiniMap", typeof(RawImage)).GetComponent<RawImage>();
            Place(ui.MiniMap.rectTransform, canvas, new Vector2(0.6f, 0.62f), new Vector2(0.98f, 0.88f));
            bool mapBig = false; // ketuk denah untuk membesarkan atau mengecilkan (spesifikasi bagian 10)
            ui.MiniMap.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                mapBig = !mapBig;
                Place(ui.MiniMap.rectTransform, canvas, mapBig ? new Vector2(0.04f, 0.3f) : new Vector2(0.6f, 0.62f), new Vector2(mapBig ? 0.96f : 0.98f, 0.88f));
            });

            var bannerPanel = Panel(canvas, new Vector2(0.05f, 0.44f), new Vector2(0.95f, 0.56f), new Color(0.1f, 0.4f, 0.2f, 0.85f));
            ui.Banner = Label(bannerPanel, "", 40, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            bannerPanel.gameObject.SetActive(false);

            var bottom = Panel(canvas, new Vector2(0, 0), new Vector2(1, 0.12f), new Color(0, 0, 0, 0.55f));
            var row = bottom.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 16, 16);
            row.spacing = 12;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = true;
            ui.LocalizeButton = NewButton(bottom, "Lokalisasi", app.OnLocalizePressed);
            ui.PrevTarget = NewButton(bottom, "<", () => app.CycleTarget(-1));
            ui.TargetLabel = Label(bottom, "Target: -", 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            ui.NextTarget = NewButton(bottom, ">", () => app.CycleTarget(1));
            ui.NavButton = NewButton(bottom, "Mulai", app.OnNavPressed);

            ui.DebugRoot = DebugPanel.Build(ui, canvas, app); // Task 10; sebelum itu kembalikan null
            return ui;
        }

        public void ShowBanner(string text)
        {
            Banner.text = text;
            Banner.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        static RectTransform NewCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return (RectTransform)go.transform;
        }

        internal static void Place(RectTransform rt, Transform parent, Vector2 min, Vector2 max)
        {
            rt.SetParent(parent, false);
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        internal static RectTransform Panel(Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject("Panel", typeof(Image));
            go.GetComponent<Image>().color = color;
            var rt = (RectTransform)go.transform;
            Place(rt, parent, min, max);
            return rt;
        }

        internal static Text Label(Transform parent, string text, int size, TextAnchor align, Vector2 min, Vector2 max)
        {
            var t = new GameObject("Text", typeof(Text)).GetComponent<Text>();
            Place(t.rectTransform, parent, min, max);
            t.font = F;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.text = text;
            return t;
        }

        internal static Button NewButton(Transform parent, string text, Action onClick)
        {
            var go = new GameObject("Button", typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.85f, 0.95f);
            Place((RectTransform)go.transform, parent, Vector2.zero, Vector2.one);
            Label(go.transform, text, 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(() => onClick());
            return b;
        }
    }
}
