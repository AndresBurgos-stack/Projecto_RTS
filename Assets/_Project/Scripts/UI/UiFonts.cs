using UnityEngine;

namespace ProjectoRTS.UI
{
    /// <summary>Shared font lookup for the runtime-created HUD elements.</summary>
    public static class UiFonts
    {
        private static Font _default;

        public static Font Default
        {
            get
            {
                if (_default == null)
                {
                    _default = UnityEngine.Resources.GetBuiltinResource<Font>(
                        "LegacyRuntime.ttf");
                }

                if (_default == null)
                {
                    _default = UnityEngine.Resources.GetBuiltinResource<Font>(
                        "Arial.ttf");
                }

                return _default;
            }
        }
    }
}
