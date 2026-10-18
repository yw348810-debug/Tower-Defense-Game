using UnityEngine;

namespace TowerDefense.Gameplay
{
    public sealed partial class LevelSession
    {
        public bool IsMenuOpen { get; private set; }
        bool menuWasPaused;

        public bool OpenSystemMenu()
        {
            if (!ready || IsMenuOpen) return false;
            menuWasPaused = IsPaused;
            IsMenuOpen = true;
            Time.timeScale = 0f;
            var hud = GetComponent<GameHud>();
            if (hud) { hud.CancelSpellTargeting(); hud.BlockWorldInputForFrames(); }
            Changed?.Invoke();
            return true;
        }

        public void CloseSystemMenu()
        {
            if (!IsMenuOpen) return;
            IsMenuOpen = false;
            IsPaused = menuWasPaused;
            Time.timeScale = IsChoosingAugment ? 0f : Phase == SessionPhase.Combat
                ? IsPaused ? 0f : GameSpeed : 1f;
            var hud = GetComponent<GameHud>();
            if (hud) hud.BlockWorldInputForFrames();
            Changed?.Invoke();
        }

        void ResetSystemMenu()
        {
            IsMenuOpen = false;
            menuWasPaused = false;
        }
    }
}
