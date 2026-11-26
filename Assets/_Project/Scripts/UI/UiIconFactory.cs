using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>Small helpers for the medallion icon buttons of the HUDs.</summary>
    public static class UiIconFactory
    {
        /// <summary>Creates a stretched icon image that never blocks pointer events.</summary>
        public static Image CreateIcon(Transform parent, string name, Sprite sprite)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);

            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        /// <summary>Updates the sprite and hides the image when no icon exists.</summary>
        public static void Apply(Image image, Sprite sprite)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
        }
    }
}
