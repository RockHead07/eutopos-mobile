using System;
using UnityEngine;
using UnityEngine.UI;

namespace Eutopos.Capture
{
    public sealed class CaptureUi
    {
        static readonly Color Ink = new Color32(242, 248, 243, 255);
        static readonly Color Muted = new Color32(178, 199, 187, 255);
        static readonly Color Backdrop = new Color32(18, 25, 21, 255);
        static readonly Color Panel = new Color32(33, 48, 40, 255);
        static readonly Color Accent = new Color32(198, 235, 207, 255);
        static readonly Color DarkInk = new Color32(22, 39, 30, 255);
        static readonly Color Warning = new Color32(255, 209, 122, 255);
        static readonly Color Glass = new Color32(18, 28, 22, 232);

        readonly Font font;
        readonly RectTransform safe;
        RectTransform screen;
        Rect lastSafe;
        Vector2Int lastSize;
        Texture2D shutterTexture;
        Sprite shutterSprite;

        public CaptureUi(Canvas canvas)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            safe = Node(canvas.transform, "Safe area");
            Stretch(safe);
            FitSafeArea();
        }

        public void FitSafeArea()
        {
            var area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size.x <= 0 || size.y <= 0 || (area == lastSafe && size == lastSize)) return;
            lastSafe = area;
            lastSize = size;
            safe.anchorMin = new Vector2(area.xMin / size.x, area.yMin / size.y);
            safe.anchorMax = new Vector2(area.xMax / size.x, area.yMax / size.y);
        }

        public RectTransform Page(string title, string subtitle, Action back = null, bool cover = true)
        {
            Reset();
            if (cover) Fill(screen, Backdrop);
            var main = Column(screen, "Halaman", 22, new RectOffset(34, 34, 30, 28));
            Stretch(main);
            if (back != null) Button(main, "Kembali", back, height: 64);
            Label(main, title, size: 48, bold: true);
            if (!string.IsNullOrEmpty(subtitle)) Label(main, subtitle, size: 24, muted: true);

            var viewport = Node(main, "Area gulir");
            viewport.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            Fill(viewport, Color.clear, raycast: true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;

            var content = Column(viewport, "Isi", 18, new RectOffset(0, 0, 4, 10));
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        public Button Button(Transform parent, string label, Action action, bool primary = false,
            bool interactable = true, int height = 78)
        {
            var rect = Node(parent, label);
            AddHeight(rect, height);
            var image = Fill(rect, primary ? Accent : Panel, raycast: true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = interactable;
            var colors = button.colors;
            colors.disabledColor = new Color(0.68f, 0.68f, 0.68f, 0.58f);
            button.colors = colors;
            if (action != null) button.onClick.AddListener(() => action());
            var caption = Caption(rect, label, 28, primary ? DarkInk : Ink, true);
            Stretch(caption.rectTransform, 10);
            caption.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        public Text Label(Transform parent, string text, int size = 26, bool muted = false, bool bold = false) =>
            Caption(parent, text, size, muted ? Muted : Ink, bold);

        public InputField Input(Transform parent, string placeholder, bool password = false, int limit = 100)
        {
            var rect = Node(parent, "Input");
            AddHeight(rect, 82);
            var image = Fill(rect, Panel, raycast: true);
            var input = rect.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.characterLimit = limit;
            var value = Caption(rect, "", 28, Ink, false);
            var hint = Caption(rect, placeholder, 26, Muted, false);
            foreach (var text in new[] { value, hint })
            {
                Stretch(text.rectTransform, 18);
                text.alignment = TextAnchor.MiddleLeft;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            input.textComponent = value;
            input.placeholder = hint;
            return input;
        }

        public sealed class CameraControls
        {
            public Text Stats, Tracking, Notice;
            public Button Undo, Shutter, Next, Auto, Points;
        }

        public CameraControls CameraPage(bool test, Action back, Action undo, Action shutter, Action next,
            Action toggleAuto, Action togglePoints)
        {
            Reset();
            var top = CameraPanel("Info rekam", true);
            var header = Row(top, 14);
            Button(header, "Beranda", back, height: 62);
            Label(header, test ? "Foto uji" : "Rekam peta", 31, bold: true).alignment = TextAnchor.MiddleCenter;
            var controls = new CameraControls
            {
                Stats = Label(top, "", 26, bold: true),
                Tracking = Label(top, "", 25),
                Notice = Label(top, "", 24),
            };
            controls.Tracking.color = Warning;
            controls.Notice.color = Warning;
            var modes = Row(top, 12);
            if (!test) controls.Auto = Button(modes, "Otomatis aktif", toggleAuto, height: 64);
            controls.Points = Button(modes, "Titik mati", togglePoints, height: 64);

            var bottom = CameraPanel("Kontrol rana", false);
            var actions = Row(bottom, 12);
            controls.Undo = Button(actions, "Batal foto", undo, height: 78);
            controls.Shutter = Button(actions, "", shutter, primary: true, height: 108);
            var rect = controls.Shutter.GetComponent<RectTransform>();
            rect.GetComponent<LayoutElement>().minWidth = 108;
            rect.GetComponent<LayoutElement>().preferredWidth = 108;
            rect.GetComponent<LayoutElement>().flexibleWidth = 0;
            var graphic = (Image)controls.Shutter.targetGraphic;
            graphic.sprite = Circle();
            graphic.preserveAspect = true;
            Caption(rect, "●", 36, DarkInk, true).alignment = TextAnchor.MiddleCenter;
            var center = rect.GetChild(rect.childCount - 1).GetComponent<RectTransform>();
            Stretch(center);
            controls.Next = Button(actions, test ? "Lanjut unggah" : "Selesai", next, primary: true, height: 78);
            return controls;
        }

        public static void SetButtonText(Button button, string label)
        {
            if (button != null) button.GetComponentInChildren<Text>().text = label;
        }

        public static void Show(Text element, string value)
        {
            if (element == null) return;
            element.text = value ?? "";
            element.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        RectTransform CameraPanel(string name, bool top)
        {
            var panel = Column(screen, name, 10, new RectOffset(16, 16, 16, 16));
            panel.anchorMin = new Vector2(0, top ? 1 : 0);
            panel.anchorMax = new Vector2(1, top ? 1 : 0);
            panel.pivot = new Vector2(0.5f, top ? 1 : 0);
            panel.sizeDelta = new Vector2(-28, 0);
            panel.anchoredPosition = new Vector2(0, top ? -14 : 14);
            Fill(panel, Glass);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel;
        }

        void Reset()
        {
            if (screen != null)
            {
                screen.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(screen.gameObject);
            }
            screen = Node(safe, "Layar");
            Stretch(screen);
        }

        Sprite Circle()
        {
            if (shutterSprite != null) return shutterSprite;
            const int size = 96;
            shutterTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2(x - 47.5f, y - 47.5f).magnitude;
                    byte alpha = (byte)(255 * Mathf.Clamp01(47 - radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            shutterTexture.SetPixels32(pixels);
            shutterTexture.Apply();
            shutterSprite = Sprite.Create(shutterTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return shutterSprite;
        }

        public void Dispose()
        {
            if (shutterSprite != null) UnityEngine.Object.Destroy(shutterSprite);
            if (shutterTexture != null) UnityEngine.Object.Destroy(shutterTexture);
            if (safe != null) UnityEngine.Object.Destroy(safe.gameObject);
        }

        Text Caption(Transform parent, string value, int size, Color color, bool bold)
        {
            var text = Node(parent, "Teks").gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.text = value;
            text.color = color;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        static RectTransform Node(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect, int padding = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        static Image Fill(RectTransform rect, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        static void AddHeight(RectTransform target, int height)
        {
            var size = target.gameObject.AddComponent<LayoutElement>();
            size.minHeight = height;
            size.flexibleWidth = 1;
        }

        static RectTransform Column(Transform parent, string name, float gap, RectOffset padding)
        {
            var rect = Node(parent, name);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = gap;
            layout.padding = padding;
            layout.childForceExpandHeight = false;
            return rect;
        }

        static RectTransform Row(Transform parent, float gap)
        {
            var rect = Node(parent, "Baris");
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = gap;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            return rect;
        }
    }
}
