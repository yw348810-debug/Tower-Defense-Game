using TMPro;
using TowerDefense.Audio;
using TowerDefense.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>One Esc menu for all three battlefields; the session owns the pause snapshot.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        public LevelSession session;
        public TMP_FontAsset fontOverride;
        private Canvas canvas;
        private GameObject page;
        private MenuSettingsView settings;
        private bool returningHome;

        public bool IsOpen => session != null && session.IsMenuOpen;
        public bool IsSettingsVisible => settings != null && settings.Root.activeSelf;

        private void Start()
        {
            if (session == null) session = GetComponent<LevelSession>();
            Build();
            canvas.gameObject.SetActive(IsOpen);
        }

        private void OnEnable() => GameSettings.Changed += RefreshSettings;
        private void OnDisable() => GameSettings.Changed -= RefreshSettings;

        private void Update()
        {
            if (session == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (IsSettingsVisible)
                {
                    GameAudio.Play(SoundCue.UICancel);
                    ShowPausePage();
                }
                else if (IsOpen)
                {
                    GameAudio.Play(SoundCue.UICancel);
                    Close();
                }
                else
                {
                    GameAudio.Play(SoundCue.UIConfirm);
                    Open();
                }
            }
            if (canvas != null && canvas.gameObject.activeSelf != IsOpen)
                canvas.gameObject.SetActive(IsOpen);
        }

        private void Build()
        {
            if (canvas != null) return;
            canvas = MenuVisual.Canvas(transform, "Battle Pause Interface", 1000);
            var dim = MenuVisual.Fill(canvas.transform, "Battle Dimmer").gameObject.AddComponent<Image>();
            dim.color = new Color(.01f, .025f, .04f, .84f);
            dim.raycastTarget = true;
            var area = MenuVisual.DesignArea(canvas);
            page = MenuVisual.Fill(area, "Pause Page").gameObject;
            var panel = MenuVisual.Rect(page.transform, "Pause Panel", 490, 180, 620, 540);
            panel.gameObject.AddComponent<Image>().color = MenuVisual.Panel;
            MenuVisual.Border(panel, 0, 0, 620, 540, new Color(.22f, .43f, .46f));
            MenuVisual.Block(panel, "Accent", 0, 0, 620, 4, MenuVisual.Teal);
            MenuVisual.Label(panel, "Paused", "GAME PAUSED", 52, 44, 516, 70, 42, MenuVisual.White, fontOverride, FontStyles.Bold);
            MenuVisual.Label(panel, "Subtitle", "Take a breath. Plan your next move.", 52, 119, 516, 60, 21, MenuVisual.Muted, fontOverride);
            MenuVisual.Button(panel, "Resume", "RESUME GAME", 52, 214, 516, 72, fontOverride, Close, true, SoundCue.UICancel);
            MenuVisual.Button(panel, "Settings", "SETTINGS", 52, 301, 516, 66, fontOverride, ShowSettings, false, SoundCue.UIConfirm);
            MenuVisual.Button(panel, "Return Home", "RETURN TO HOME", 52, 382, 516, 66, fontOverride, ReturnHome, false, SoundCue.UICancel);
            MenuVisual.Label(panel, "Resume Hint", "ESC / RESUME", 52, 478, 516, 28, 16, MenuVisual.Muted,
                fontOverride, FontStyles.Normal, TextAlignmentOptions.Center);
            settings = MenuVisual.Settings(area, fontOverride, ShowPausePage);
            MenuVisual.AddLocalization(canvas.gameObject);
            canvas.gameObject.SetActive(false);
        }

        public void Open()
        {
            if (session == null || returningHome) return;
            if (canvas == null) Build();
            session.OpenSystemMenu();
            ShowPausePage();
            canvas.gameObject.SetActive(IsOpen);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void Close()
        {
            if (session == null || returningHome) return;
            session.CloseSystemMenu();
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        public void ShowSettings()
        {
            if (settings == null) Build();
            page.SetActive(false);
            settings.Refresh();
            settings.Root.SetActive(true);
        }

        public void ShowPausePage()
        {
            if (settings == null) Build();
            settings.Root.SetActive(false);
            page.SetActive(true);
        }

        public void ReturnHome()
        {
            if (returningHome || !Application.CanStreamedLevelBeLoaded("MainMenu")) return;
            returningHome = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        private void RefreshSettings() => settings?.Refresh();
    }
}
