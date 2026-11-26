using UnityEngine;
using UnityEngine.UI;

namespace ProjectoRTS.UI
{
    /// <summary>Shared colours for the icon buttons of the runtime HUDs.</summary>
    public static class UiButtonStyle
    {
        public static ColorBlock CreateIconColors()
        {
            return new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.93f, 0.75f, 1f),
                pressedColor = new Color(0.78f, 0.72f, 0.58f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.45f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }
    }
}
