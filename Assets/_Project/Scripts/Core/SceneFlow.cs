using UnityEngine.SceneManagement;

namespace ProjectoRTS.Core
{
    /// <summary>
    /// Central place for the scene names used by the title screen and the
    /// gameplay scene. The gameplay name is checked positively so runtime
    /// HUD bootstraps do not appear in the title scene or in preview scenes.
    /// </summary>
    public static class SceneFlow
    {
        public const string TitleSceneName = "TitleScreen";
        public const string GameplaySceneName = "Gameplay";
        public const string LegacyGameplaySceneName = "PlacementDemo";

        public static bool IsTitleScreen
        {
            get { return SceneManager.GetActiveScene().name == TitleSceneName; }
        }

        public static bool IsGameplayScene
        {
            get { return IsGameplaySceneName(SceneManager.GetActiveScene().name); }
        }

        public static bool IsGameplaySceneName(string sceneName)
        {
            return sceneName == GameplaySceneName || sceneName == LegacyGameplaySceneName;
        }
    }
}
