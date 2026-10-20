using UnityEngine;
using UnityEngine.UI;

namespace Eutopos.App
{
    /// <summary>Panel tersembunyi: ketuk logo 5 kali dalam 3 detik (pola ADR-025 di DARSI).</summary>
    public static class DebugPanel
    {
        static int taps;
        static float firstTapT;

        public static void OnLogoTapped(AppController app)
        {
            float now = Time.realtimeSinceStartup;
            if (now - firstTapT > 3f) { taps = 0; firstTapT = now; }
            if (++taps >= 5) { taps = 0; app.ToggleDebug(); }
        }

        public static GameObject Build(AppUi ui, Transform canvas, AppController app)
        {
            var panel = AppUi.Panel(canvas, new Vector2(0.04f, 0.14f), new Vector2(0.96f, 0.6f), new Color(0, 0, 0, 0.85f));
            var col = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            col.padding = new RectOffset(20, 20, 20, 20);
            col.spacing = 14;
            col.childForceExpandWidth = true;
            col.childForceExpandHeight = true;

            ui.DebugInterval = Row(panel, "Interval -", () => app.AdjustInterval(-1f), "+", () => app.AdjustInterval(1f));
            ui.DebugJump = Row(panel, "Lompatan -", () => app.AdjustJump(-0.5f), "+", () => app.AdjustJump(0.5f));
            ui.DebugPoint = Row(panel, "<", () => app.CyclePoint(-1), ">", () => app.CyclePoint(1));
            AppUi.NewButton(panel, "Saya berdiri di titik ini", app.MarkGroundTruth);
            AppUi.NewButton(panel, "Ekspor log", app.ExportLog);
            ui.DebugInfo = AppUi.Label(panel, "", 24, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one);
            panel.gameObject.SetActive(false);
            return panel.gameObject;
        }

        static Text Row(RectTransform parent, string left, System.Action onLeft, string right, System.Action onRight)
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            AppUi.Place((RectTransform)row.transform, parent, Vector2.zero, Vector2.one);
            var h = row.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = true;
            AppUi.NewButton(row.transform, left, onLeft);
            var label = AppUi.Label(row.transform, "", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            AppUi.NewButton(row.transform, right, onRight);
            return label;
        }
    }
}
