#if UNITY_EDITOR
using ProjectoRTS.Buildings;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ProjectoRTS.EditorTools
{
    public static partial class PlacementDemoBuilder
    {
        private static RecruitmentPanel CreateRecruitmentUI()
        {
            GameObject canvasObject = new GameObject(
                "Recruitment Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            InputSystemUIInputModule inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();

            GameObject panelObject = new GameObject(
                "Recruitment Panel",
                typeof(RectTransform),
                typeof(Image),
                typeof(RecruitmentPanel));
            panelObject.transform.SetParent(canvasObject.transform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(20f, 20f);
            panelRect.sizeDelta = new Vector2(360f, 260f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            panelImage.raycastTarget = true;

            RecruitmentPanel panel = panelObject.GetComponent<RecruitmentPanel>();

            GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObject.transform.SetParent(panelObject.transform, false);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -10f);
            titleRect.sizeDelta = new Vector2(-20f, 30f);

            Text title = titleObject.GetComponent<Text>();
            title.font = GetUIFont();
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleLeft;
            title.color = Color.white;
            title.text = "Reclutamiento";
            title.raycastTarget = false;

            GameObject containerObject = new GameObject(
                "Buttons",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            containerObject.transform.SetParent(panelObject.transform, false);
            RectTransform containerRect = containerObject.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0f, 0f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.offsetMin = new Vector2(10f, 10f);
            containerRect.offsetMax = new Vector2(-10f, -50f);

            VerticalLayoutGroup layout = containerObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject buttonObject = new GameObject(
                "Recruit Button Template",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(containerObject.transform, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(0f, 40f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.18f, 0.28f, 0.42f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.9f, 1f);
            colors.pressedColor = new Color(0.65f, 0.75f, 0.9f);
            button.colors = colors;

            LayoutElement layoutElement = buttonObject.GetComponent<LayoutElement>();
            layoutElement.minHeight = 40f;
            layoutElement.preferredHeight = 40f;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            Text label = labelObject.GetComponent<Text>();
            label.font = GetUIFont();
            label.fontSize = 18;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = "Reclutar";

            buttonObject.SetActive(false);

            SerializedObject panelSerialized = new SerializedObject(panel);
            panelSerialized.FindProperty("panelRoot").objectReferenceValue = panelObject;
            panelSerialized.FindProperty("buttonContainer").objectReferenceValue = containerRect;
            panelSerialized.FindProperty("title").objectReferenceValue = title;
            panelSerialized.FindProperty("buttonTemplate").objectReferenceValue = button;
            panelSerialized.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        private static Font GetUIFont()
        {
            Font font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = UnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return font;
        }
    }
}
#endif
