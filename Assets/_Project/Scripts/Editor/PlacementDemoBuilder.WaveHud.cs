#if UNITY_EDITOR
using ProjectoRTS.Waves;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.EditorTools
{
    public static partial class PlacementDemoBuilder
    {
        private static WaveHud CreateWaveHud(
            WaveManager waves,
            DayNightCalendar calendar)
        {
            GameObject canvasObject = new GameObject(
                "Wave Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Day label, top-left.
            GameObject dayObject = new GameObject("Day Label", typeof(RectTransform), typeof(Text));
            dayObject.transform.SetParent(canvasObject.transform, false);
            RectTransform dayRect = dayObject.GetComponent<RectTransform>();
            dayRect.anchorMin = new Vector2(0f, 1f);
            dayRect.anchorMax = new Vector2(0f, 1f);
            dayRect.pivot = new Vector2(0f, 1f);
            dayRect.anchoredPosition = new Vector2(24f, -24f);
            dayRect.sizeDelta = new Vector2(420f, 48f);

            Text dayLabel = dayObject.GetComponent<Text>();
            dayLabel.font = GetUIFont();
            dayLabel.fontSize = 30;
            dayLabel.fontStyle = FontStyle.Bold;
            dayLabel.alignment = TextAnchor.UpperLeft;
            dayLabel.color = new Color(1f, 0.93f, 0.78f);
            dayLabel.text = "Dia 1";
            dayLabel.raycastTarget = false;

            // Warning label, top-center.
            GameObject warningObject = new GameObject("Warning Label", typeof(RectTransform), typeof(Text));
            warningObject.transform.SetParent(canvasObject.transform, false);
            RectTransform warningRect = warningObject.GetComponent<RectTransform>();
            warningRect.anchorMin = new Vector2(0.5f, 1f);
            warningRect.anchorMax = new Vector2(0.5f, 1f);
            warningRect.pivot = new Vector2(0.5f, 1f);
            warningRect.anchoredPosition = new Vector2(0f, -24f);
            warningRect.sizeDelta = new Vector2(900f, 72f);

            Text warningLabel = warningObject.GetComponent<Text>();
            warningLabel.font = GetUIFont();
            warningLabel.fontSize = 26;
            warningLabel.fontStyle = FontStyle.Bold;
            warningLabel.alignment = TextAnchor.UpperCenter;
            warningLabel.color = new Color(1f, 0.55f, 0.4f);
            warningLabel.text = string.Empty;
            warningLabel.raycastTarget = false;

            WaveHud hud = canvasObject.AddComponent<WaveHud>();
            SetObjectReference(hud, "waves", waves);
            SetObjectReference(hud, "calendar", calendar);
            SetObjectReference(hud, "dayLabel", dayLabel);
            SetObjectReference(hud, "warningLabel", warningLabel);

            return hud;
        }
    }
}
#endif