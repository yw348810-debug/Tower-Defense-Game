using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using TowerDefense.Gameplay;
using TowerDefense.UI;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    /// <summary>Checks rendered glyphs as well as translated strings in the compact battle HUD.</summary>
    public static class HudLocalizationValidation
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int failures;
            public string error;
            public Check[] checks;
        }

        static bool HasVisibleGlyphs(TMP_Text text)
        {
            text.ForceMeshUpdate(true, true);
            return text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c => c.isVisible);
        }

        public static Report Run()
        {
            var rows = new List<Check>();
            var report = new Report();
            Action<string, bool> check = (name, passed) => rows.Add(new Check { name = name, passed = passed });
            GameLanguage original = GameSettings.Language;
            GameObject fixture = null;
            try
            {
                check("Portable Chinese font is available", Localization.Font != null);
                GameSettings.SetLanguage(GameLanguage.English);
                fixture = new GameObject("HUD glyph regression fixture", typeof(RectTransform), typeof(Canvas));
                fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var hud = fixture.AddComponent<GameHud>();
                hud.enabled = false;
                var specs = new[]
                {
                    ("Gold", "180", 85f, 34f, 26),
                    ("Health", "20", 64f, 34f, 26),
                    ("Mana", "0", 82f, 34f, 26),
                    ("Wave", "WAVE 1 / 7", 174f, 30f, 21),
                    ("Tower name", "RAPID FIRE", 111f, 26f, 18),
                    ("Tower role", "Focus fire", 111f, 25f, 18),
                    ("Tower price", "60", 86f, 30f, 22),
                    ("Spell name", "FIREBALL", 162f, 27f, 23),
                    ("Spell cost", "12 MANA", 134f, 28f, 20),
                    ("Spell stats", "3s burn / radius 3.2", 222f, 25f, 18),
                    ("Spell shortcut", "[Q] AIM", 222f, 25f, 18)
                };
                var labels = new List<TextMeshProUGUI>();
                MethodInfo configure = typeof(GameHud).GetMethod("ConfigureText", Private);
                foreach (var spec in specs)
                {
                    var item = new GameObject(spec.Item1, typeof(RectTransform), typeof(TextMeshProUGUI));
                    item.transform.SetParent(fixture.transform, false);
                    item.GetComponent<RectTransform>().sizeDelta = new Vector2(spec.Item3, spec.Item4);
                    var text = item.GetComponent<TextMeshProUGUI>();
                    configure.Invoke(hud, new object[] { text, spec.Item2, spec.Item5, Color.white });
                    labels.Add(text);
                }
                var localized = fixture.AddComponent<LocalizedCanvas>();
                foreach (GameLanguage language in new[] { GameLanguage.Chinese, GameLanguage.English, GameLanguage.Chinese })
                {
                    GameSettings.SetLanguage(language);
                    localized.RefreshNow();
                    Canvas.ForceUpdateCanvases();
                    for (int i = 0; i < labels.Count; i++)
                    {
                        check(language + " source: " + specs[i].Item1, labels[i].text == Localization.T(specs[i].Item2));
                        check(language + " visible mesh: " + specs[i].Item1, HasVisibleGlyphs(labels[i]));
                    }
                }

                // Resource updates must remain visible at zero, after spending, and after rewards.
                foreach (int value in new[] { 0, 1, 57, 280, 0, 999999 })
                {
                    labels[0].SetText("{0}", value);
                    labels[1].SetText("{0}", Mathf.Min(value, 20));
                    labels[2].text = value.ToString();
                    localized.RefreshNow();
                    check("Dynamic counters preserve value " + value, labels[0].text == value.ToString()
                        && labels[1].text == Mathf.Min(value, 20).ToString() && labels[2].text == value.ToString());
                    check("Dynamic counters render value " + value, labels.Take(3).All(HasVisibleGlyphs));
                }
                labels[0].text = "∞";
                localized.RefreshNow();
                check("Cheat infinity counter renders", labels[0].text == "∞" && HasVisibleGlyphs(labels[0]));

                labels[3].SetText("WAVE {0} / {1}", 3f, 18f);
                localized.RefreshNow();
                string chineseWave = labels[3].text;
                GameSettings.SetLanguage(GameLanguage.English);
                localized.RefreshNow();
                check("Formatted dynamic source returns to English", labels[3].text == "WAVE 3 / 18");
                GameSettings.SetLanguage(GameLanguage.Chinese);
                localized.RefreshNow();
                check("Formatted dynamic source returns to Chinese", labels[3].text == chineseWave && HasVisibleGlyphs(labels[3]));

                if (EditorApplication.isPlaying)
                {
                    var live = UnityEngine.Object.FindObjectsByType<GameHud>()
                        .FirstOrDefault(candidate => candidate != hud && candidate.session);
                    if (live)
                    {
                        typeof(GameHud).GetMethod("Refresh", Private).Invoke(live, null);
                        var canvas = (LocalizedCanvas)typeof(GameHud).GetField("localizedCanvas", Private).GetValue(live);
                        canvas.RefreshNow();
                        foreach (string field in new[] { "goldText", "healthText", "manaText", "waveText", "fireballPriceText", "fireballStatsText", "fireballModeText" })
                        {
                            var text = (TMP_Text)typeof(GameHud).GetField(field, Private).GetValue(live);
                            check("Live Chinese battle HUD: " + field, text && !string.IsNullOrWhiteSpace(text.text) && HasVisibleGlyphs(text));
                        }
                        var prices = (TextMeshProUGUI[])typeof(GameHud).GetField("towerPrices", Private).GetValue(live);
                        for (int i = 0; i < live.session.AvailableTowerCount; i++)
                            check("Live Chinese tower price " + i, !string.IsNullOrWhiteSpace(prices[i].text) && HasVisibleGlyphs(prices[i]));
                    }
                }
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                check("Glyph regression validation completed", false);
            }
            finally
            {
                if (fixture) UnityEngine.Object.DestroyImmediate(fixture);
                GameSettings.SetLanguage(original);
            }
            report.checks = rows.ToArray();
            report.failures = rows.Count(c => !c.passed);
            report.passed = report.failures == 0;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "DifficultyAndUI");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "hud-localization-validation.json"), JsonUtility.ToJson(report, true));
            return report;
        }
    }
}
