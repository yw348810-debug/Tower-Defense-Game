using TMPro;
using TowerDefense.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>Home, level selection and persistent settings, independent of a running battle.</summary>
    [DisallowMultipleComponent]
    public sealed class FrontendMenu : MonoBehaviour
    {
        public Sprite[] levelPreviews;
        public TMP_FontAsset fontOverride;

        private static readonly string[] Scenes =
        {
            "Level01_ForestOutpost", "Level02_CanyonConfluence", "Level03_CoreFortress"
        };
        private static readonly string[] LevelNames = { "FOREST OUTPOST", "CANYON CONFLUENCE", "CORE FORTRESS" };
        private static readonly string[] LevelRoles = { "LEARN THE DEFENSE", "HOLD TWO FRONTS", "FACE THE TITANS" };
        private static readonly string[] LevelDescriptions =
        {
            "Establish your defenses in the forest.",
            "Command soldiers and control the battlefield.",
            "Master powerful spells and confront two bosses."
        };
        private static readonly Color[] LevelColors =
        {
            new Color(.41f, .91f, .64f), new Color(1f, .70f, .35f), new Color(.45f, .84f, 1f)
        };
        private Canvas canvas;
        private GameObject home;
        private GameObject levels;
        private MenuSettingsView settings;
        private MenuVeil veil;
        private bool loading;

        public bool IsSettingsVisible => settings != null && settings.Root.activeSelf;
        public bool IsLevelSelectionVisible => levels != null && levels.activeSelf;

        private void Start()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Build();
            ShowHome();
        }

        private void OnEnable() => GameSettings.Changed += RefreshSettings;
        private void OnDisable() => GameSettings.Changed -= RefreshSettings;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame &&
                (IsSettingsVisible || IsLevelSelectionVisible))
            {
                GameAudio.Play(SoundCue.UICancel);
                ShowHome();
            }
        }

        private void Build()
        {
            if (canvas != null) return;
            canvas = MenuVisual.Canvas(transform, "Home Interface", 500);
            veil = MenuVisual.Fill(canvas.transform, "Atmospheric Veil").gameObject.AddComponent<MenuVeil>();
            veil.raycastTarget = false;
            var area = MenuVisual.DesignArea(canvas);
            home = MenuVisual.Fill(area, "Home Page").gameObject;
            BuildHome(home.transform);
            UiPolish.Entrance(home);
            levels = MenuVisual.Fill(area, "Level Selection Page").gameObject;
            BuildLevels(levels.transform);
            settings = MenuVisual.Settings(area, fontOverride, ShowHome);
            MenuVisual.AddLocalization(canvas.gameObject);
        }

        private void BuildHome(Transform parent)
        {
            MenuVisual.Block(parent, "Top Accent", 96, 74, 44, 4, MenuVisual.Teal);
            MenuVisual.Label(parent, "Brand", "THE LAST LINE", 155, 52, 510, 46, 21, MenuVisual.Teal, fontOverride, FontStyles.Bold);
            MenuVisual.Label(parent, "Title", "HEX\nDEFENSE", 96, 142, 690, 220, 90, MenuVisual.White,
                fontOverride, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            var emblem = MenuVisual.Rect(parent, "Hex Brand Emblem", 612, 68, 54, 54).gameObject.AddComponent<TowerDefense.Gameplay.HudIcon>();
            emblem.symbol = TowerDefense.Gameplay.HudIcon.Symbol.Hexagon;
            emblem.color = new Color(.24f, .91f, .78f, .48f);
            emblem.raycastTarget = false;
            MenuVisual.Label(parent, "Motto", "BUILD. ADAPT. DEFEND.", 100, 390, 640, 46, 24, MenuVisual.Gold, fontOverride, FontStyles.Bold);
            MenuVisual.Label(parent, "Subtitle", "Defend three frontiers. Shape your strategy.", 100, 446, 640, 62, 22, MenuVisual.Muted, fontOverride);
            MenuVisual.Button(parent, "Start Game", "START GAME", 100, 548, 420, 76, fontOverride, ShowLevels, true, SoundCue.UIConfirm);
            MenuVisual.Button(parent, "Settings", "SETTINGS", 100, 638, 420, 68, fontOverride, ShowSettings, false, SoundCue.UIConfirm);
            MenuVisual.Button(parent, "Quit Game", "QUIT GAME", 100, 720, 420, 68, fontOverride, QuitGame);
            MenuVisual.Block(parent, "Footer Line", 100, 838, 1400, 1, new Color(.27f, .44f, .48f, .55f));
            MenuVisual.Label(parent, "Footer", "THREE FRONTIERS · ONE DEFENSE", 100, 850, 900, 32, 16, MenuVisual.Muted, fontOverride);
            MenuVisual.Label(parent, "Feature Number", "01 / 02 / 03", 1000, 757, 500, 36, 23, MenuVisual.Gold,
                fontOverride, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            MenuVisual.Label(parent, "Feature Label", "CHOOSE YOUR BATTLEFIELD", 900, 798, 600, 32, 17, MenuVisual.White,
                fontOverride, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        }

        private void BuildLevels(Transform parent)
        {
            MenuVisual.Label(parent, "Eyebrow", "SELECT A LEVEL", 100, 58, 1200, 42, 20, MenuVisual.Teal, fontOverride, FontStyles.Bold);
            MenuVisual.Label(parent, "Title", "Choose your battlefield", 100, 105, 1400, 82, 48, MenuVisual.White, fontOverride, FontStyles.Bold);
            MenuVisual.Label(parent, "Subtitle", "Every frontier brings a new challenge.", 100, 193, 1400, 42, 22, MenuVisual.Muted, fontOverride);
            for (int i = 0; i < 3; i++)
            {
                int level = i + 1;
                float x = 100 + 478 * i;
                var card = MenuVisual.Rect(parent, "Level " + level + " Card", x, 270, 444, 500);
                card.gameObject.AddComponent<Image>().color = new Color(.035f, .08f, .105f, .99f);
                MenuVisual.Border(card, 0, 0, 444, 500, new Color(.19f, .32f, .36f), 1);
                UiPolish.SetAccent(card.GetComponent<Image>(), LevelColors[i]);
                UiPolish.Entrance(card.gameObject, i * .055f);
                var previewFrame = MenuVisual.Block(card, "Preview Frame", 10, 10, 424, 212, Color.white);
                UiPolish.Skin(previewFrame, LevelColors[i], 8f);
                var previewMask = previewFrame.gameObject.AddComponent<Mask>();
                previewMask.showMaskGraphic = false;
                var preview = MenuVisual.Fill(previewFrame.transform, "Preview").gameObject.AddComponent<Image>();
                preview.color = new Color(.08f, .18f, .21f);
                preview.raycastTarget = false;
                if (levelPreviews != null && i < levelPreviews.Length && levelPreviews[i] != null)
                {
                    preview.sprite = levelPreviews[i];
                    preview.color = Color.white;
                    preview.preserveAspect = false;
                }
                MenuVisual.Block(previewFrame.transform, "Preview Footer", 0, 190, 424, 22, new Color(.025f, .06f, .08f, .72f));
                MenuVisual.Block(card, "Accent", 24, 226, 48, 2, LevelColors[i]);
                var badge = MenuVisual.Block(card, "Level Badge", 20, 20, 62, 54, new Color(.018f, .045f, .055f, .94f));
                UiPolish.Skin(badge, LevelColors[i]);
                MenuVisual.Label(card, "Level Number", "0" + level, 20, 20, 62, 54, 25, LevelColors[i], fontOverride,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                MenuVisual.Label(card, "Role", LevelRoles[i], 24, 242, 396, 30, 15, LevelColors[i], fontOverride, FontStyles.Bold);
                MenuVisual.Label(card, "Level Name", LevelNames[i], 24, 279, 396, 48, 28, MenuVisual.White, fontOverride, FontStyles.Bold);
                MenuVisual.Label(card, "Description", LevelDescriptions[i], 24, 335, 396, 66, 19, MenuVisual.Muted, fontOverride);
                MenuVisual.Label(card, "Waves", new[] { "7 WAVES", "12 WAVES", "18 WAVES" }[i], 24, 403, 396, 28, 15,
                    LevelColors[i], fontOverride, FontStyles.Bold);
                MenuVisual.Button(card, "Enter Level " + level, "ENTER BATTLE", 24, 438, 396, 44, fontOverride, () => OpenLevel(level), true, SoundCue.UIConfirm);
            }
            MenuVisual.Button(parent, "Back Home", "BACK", 100, 805, 245, 56, fontOverride, ShowHome, false, SoundCue.UICancel);
            MenuVisual.Label(parent, "Back Hint", "ESC / BACK", 1230, 811, 270, 44, 16, MenuVisual.Muted,
                fontOverride, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        }

        public void ShowHome()
        {
            if (canvas == null) Build();
            home.SetActive(true);
            levels.SetActive(false);
            settings.Root.SetActive(false);
            veil.fullShade = false;
            veil.SetVerticesDirty();
        }

        public void ShowLevels()
        {
            if (canvas == null) Build();
            home.SetActive(false);
            levels.SetActive(true);
            settings.Root.SetActive(false);
            veil.fullShade = true;
            veil.SetVerticesDirty();
        }

        public void ShowSettings()
        {
            if (canvas == null) Build();
            settings.Refresh();
            settings.Root.SetActive(true);
        }

        public void OpenLevel(int level)
        {
            if (loading || level < 1 || level > Scenes.Length) return;
            string scene = Scenes[level - 1];
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError("The selected battlefield is not included in Build Settings: " + scene);
                return;
            }
            loading = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RefreshSettings() => settings?.Refresh();
    }
}
