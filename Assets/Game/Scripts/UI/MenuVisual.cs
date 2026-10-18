using System;
using TMPro;
using TowerDefense.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    /// <summary>Shared typography, controls and settings for the front end and pause overlay.</summary>
    internal static class MenuVisual
    {
        internal static readonly Color Navy = new Color(.027f, .055f, .08f, 1f);
        internal static readonly Color Panel = new Color(.045f, .09f, .12f, .985f);
        internal static readonly Color Teal = new Color(.24f, .91f, .78f);
        internal static readonly Color Gold = new Color(1f, .79f, .42f);
        internal static readonly Color White = new Color(.94f, .98f, 1f);
        internal static readonly Color Muted = new Color(.59f, .72f, .77f);

        internal static Canvas Canvas(Transform owner, string name, int order)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(owner, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            if (EventSystem.current == null)
            {
                var events = new GameObject("Menu Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(owner, false);
            }
            return canvas;
        }

        internal static RectTransform DesignArea(Canvas canvas)
        {
            var rect = Rect(canvas.transform, "Menu Layout", 0, 0, 1600, 900);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        internal static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static RectTransform Fill(Transform parent, string name)
        {
            var rect = Rect(parent, name, 0, 0, 0, 0);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal static Image Block(Transform parent, string name, float x, float y, float width, float height, Color color, bool raycast = false)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        internal static TextMeshProUGUI Label(Transform parent, string name, string text, float x, float y, float width, float height,
            float size, Color color, TMP_FontAsset font, FontStyles style = FontStyles.Normal, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var label = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font != null ? font : Localization.Font;
            if (label.font == null) label.font = TMP_Settings.defaultFontAsset;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            // This SDF atlas has eight pixels of gutter. TMP's extra-padding option
            // expands UVs by 10.25 pixels and samples neighbouring glyphs as stray lines.
            label.extraPadding = false;
            return label;
        }

        internal static Button Button(Transform parent, string name, string text, float x, float y, float width, float height,
            TMP_FontAsset font, Action action, bool primary = false, SoundCue sound = SoundCue.UIClick)
        {
            var background = Block(parent, name, x, y, width, height, primary ? Teal : new Color(.065f, .14f, .18f, .98f), true);
            UiPolish.Skin(background, primary ? new Color(.73f, 1f, .87f) : UiPolish.Trim);
            var button = background.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(1.1f, 1.08f, 1.06f) : new Color(1.35f, 1.45f, 1.5f);
            colors.pressedColor = new Color(.72f, .85f, .87f);
            colors.selectedColor = new Color(1.12f, 1.2f, 1.22f);
            colors.disabledColor = new Color(.45f, .45f, .45f, .8f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            UiPolish.Feedback(button, width > 300f ? 1.008f : 1.015f);
            UiSound.Attach(button).Cue = sound;
            button.onClick.AddListener(() => action?.Invoke());
            Label(background.transform, "Label", text, 24, 0, width - 48, height, 22,
                primary ? Navy : White, font, FontStyles.Bold);
            Label(background.transform, "Arrow", ">", width - 40, 0, 24, height, 22,
                primary ? Navy : Teal, font, FontStyles.Normal, TextAlignmentOptions.Center);
            return button;
        }

        internal static void Border(Transform parent, float x, float y, float width, float height, Color color, float thickness = 1)
        {
            var surface = parent.GetComponent<Image>();
            if (surface != null && x == 0 && y == 0)
            {
                UiPolish.Skin(surface, color, 10f);
                return;
            }
            Block(parent, "Border Top", x, y, width, thickness, color);
            Block(parent, "Border Bottom", x, y + height - thickness, width, thickness, color);
            Block(parent, "Border Left", x, y, thickness, height, color);
            Block(parent, "Border Right", x + width - thickness, y, thickness, height, color);
        }

        internal static void AddLocalization(GameObject canvas)
        {
            if (canvas.GetComponent<LocalizedCanvas>() == null) canvas.AddComponent<LocalizedCanvas>();
        }

        internal static MenuSettingsView Settings(Transform parent, TMP_FontAsset font, Action back)
        {
            return new MenuSettingsView(parent, font, back);
        }
    }

    internal sealed class MenuSettingsView
    {
        internal readonly GameObject Root;
        private readonly Slider volume;
        private readonly TextMeshProUGUI volumeValue;
        private readonly Image englishBackground;
        private readonly Image chineseBackground;
        private readonly Image[] difficultyBackgrounds;
        private readonly TextMeshProUGUI difficultyHint;

        internal MenuSettingsView(Transform parent, TMP_FontAsset font, Action back)
        {
            Root = MenuVisual.Fill(parent, "Settings Page").gameObject;
            var dim = MenuVisual.Fill(Root.transform, "Settings Dimmer").gameObject.AddComponent<Image>();
            dim.color = new Color(.015f, .03f, .045f, .74f);
            dim.raycastTarget = true;
            var panel = MenuVisual.Rect(Root.transform, "Settings Panel", 440, 80, 720, 740);
            var surface = panel.gameObject.AddComponent<Image>();
            surface.color = MenuVisual.Panel;
            MenuVisual.Border(panel, 0, 0, 720, 740, new Color(.23f, .48f, .52f));
            MenuVisual.Block(panel, "Accent", 0, 36, 3, 56, MenuVisual.Teal);
            UiPolish.Entrance(panel.gameObject);
            MenuVisual.Label(panel, "Heading", "SETTINGS", 52, 32, 610, 64, 40, MenuVisual.White, font, FontStyles.Bold);
            MenuVisual.Block(panel, "Divider", 52, 114, 616, 1, new Color(.17f, .29f, .33f));
            MenuVisual.Label(panel, "Volume Label", "MASTER VOLUME", 52, 142, 480, 36, 22, MenuVisual.White, font, FontStyles.Bold);
            volumeValue = MenuVisual.Label(panel, "Volume Value", "100%", 552, 142, 116, 36, 24, MenuVisual.Teal, font,
                FontStyles.Bold, TextAlignmentOptions.MidlineRight);
            var sliderRect = MenuVisual.Rect(panel, "Master Volume Slider", 52, 195, 616, 42);
            volume = sliderRect.gameObject.AddComponent<Slider>();
            sliderRect.gameObject.AddComponent<UiSliderSound>();
            volume.minValue = 0;
            volume.maxValue = 1;
            volume.wholeNumbers = false;
            var track = MenuVisual.Block(sliderRect, "Track", 0, 15, 616, 8, new Color(.13f, .25f, .29f), true);
            UiPolish.Skin(track, new Color(.18f, .42f, .44f), 4f);
            var fillArea = MenuVisual.Rect(sliderRect, "Fill Area", 0, 15, 616, 8);
            var fill = MenuVisual.Fill(fillArea, "Fill").gameObject.AddComponent<Image>();
            fill.color = MenuVisual.Teal;
            UiPolish.Skin(fill, MenuVisual.Teal, 4f);
            fill.raycastTarget = false;
            volume.fillRect = fill.rectTransform;
            var handleArea = MenuVisual.Rect(sliderRect, "Handle Area", 8, 0, 600, 42);
            var handle = MenuVisual.Block(handleArea, "Handle", 0, 6, 18, 30, MenuVisual.White, true);
            UiPolish.Skin(handle, MenuVisual.Teal, 4f);
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0, .5f);
            handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.rectTransform.anchoredPosition = Vector2.zero;
            volume.handleRect = handle.rectTransform;
            volume.targetGraphic = handle;
            volume.direction = Slider.Direction.LeftToRight;
            volume.onValueChanged.AddListener(GameSettings.SetVolume);
            MenuVisual.Label(panel, "Volume Hint", "Adjust music and sound effects instantly. Your volume is saved.", 52, 245, 616, 42, 18, MenuVisual.Muted, font);
            MenuVisual.Label(panel, "Language Label", "LANGUAGE", 52, 300, 616, 36, 22, MenuVisual.White, font, FontStyles.Bold);
            var english = MenuVisual.Button(panel, "English Language", "English", 52, 350, 298, 58, font, () => GameSettings.SetLanguage(GameLanguage.English));
            var chinese = MenuVisual.Button(panel, "Chinese Language", "中文", 370, 350, 298, 58, font, () => GameSettings.SetLanguage(GameLanguage.Chinese));
            englishBackground = english.GetComponent<Image>();
            chineseBackground = chinese.GetComponent<Image>();
            MenuVisual.Label(panel, "Language Hint", "Your language choice applies throughout the game.", 52, 420, 616, 38, 18, MenuVisual.Muted, font);
            MenuVisual.Label(panel, "Difficulty Label", "DIFFICULTY", 52, 474, 616, 36, 22, MenuVisual.White, font, FontStyles.Bold);
            difficultyBackgrounds = new Image[3];
            string[] modes = { "EASY", "HARD", "CHEAT" };
            for (int i = 0; i < modes.Length; i++)
            {
                var difficulty = (GameDifficulty)i;
                difficultyBackgrounds[i] = MenuVisual.Button(panel, modes[i] + " Difficulty", modes[i], 52 + i * 212, 524, 192, 58,
                    font, () => GameSettings.SetDifficulty(difficulty)).GetComponent<Image>();
            }
            difficultyHint = MenuVisual.Label(panel, "Difficulty Hint", "", 52, 592, 616, 62, 18, MenuVisual.Muted, font);
            MenuVisual.Button(panel, "Back", "BACK", 52, 666, 616, 52, font, back, false, SoundCue.UICancel);
            Refresh();
            Root.SetActive(false);
        }

        internal void Refresh()
        {
            volume.SetValueWithoutNotify(GameSettings.MasterVolume);
            volumeValue.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
            englishBackground.color = GameSettings.Language == GameLanguage.English ? new Color(.12f, .40f, .39f) : new Color(.065f, .14f, .18f);
            chineseBackground.color = GameSettings.Language == GameLanguage.Chinese ? new Color(.12f, .40f, .39f) : new Color(.065f, .14f, .18f);
            UiPolish.SetAccent(englishBackground, GameSettings.Language == GameLanguage.English ? MenuVisual.Teal : UiPolish.Trim,
                GameSettings.Language == GameLanguage.English);
            UiPolish.SetAccent(chineseBackground, GameSettings.Language == GameLanguage.Chinese ? MenuVisual.Teal : UiPolish.Trim,
                GameSettings.Language == GameLanguage.Chinese);
            for (int i = 0; i < difficultyBackgrounds.Length; i++)
            {
                difficultyBackgrounds[i].color = (int)GameSettings.Difficulty == i ? new Color(.12f, .40f, .39f) : new Color(.065f, .14f, .18f);
                Color accent = i == 0 ? MenuVisual.Teal : i == 1 ? MenuVisual.Gold : new Color(.79f, .65f, 1f);
                UiPolish.SetAccent(difficultyBackgrounds[i], accent, (int)GameSettings.Difficulty == i);
            }
            switch (GameSettings.Difficulty)
            {
                case GameDifficulty.Hard:
                    difficultyHint.text = "Original enemy strength, speed and starting resources.\nChanges apply to the current battle.";
                    break;
                case GameDifficulty.Cheat:
                    difficultyHint.text = "Unlimited gold, base health and mana.\nChanges apply to the current battle.";
                    break;
                default:
                    difficultyHint.text = "More gold and health. Weaker, slower enemies.\nChanges apply to the current battle.";
                    break;
            }
        }
    }

}
