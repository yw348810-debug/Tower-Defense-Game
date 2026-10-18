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
    /// <summary>Play-mode menu boundaries. Scene navigation is intentionally tested separately.</summary>
    public static class FrontendValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int failures;
            public int level;
            public string scene;
            public string error;
            public Check[] checks;
        }

        static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        static AugmentId[] Offers(LevelSession session) => session.AugmentOffers.Select(c => c.Id).ToArray();
        static bool Cjk(char c) => c >= '\u4e00' && c <= '\u9fff';

        public static Report Run() => RunInLevel(UnityEngine.Object.FindAnyObjectByType<LevelSession>());

        public static Report RunInLevel(LevelSession session)
        {
            if (!EditorApplication.isPlaying || !session) throw new InvalidOperationException("Requires a running battlefield.");
            var rows = new List<Check>();
            var report = new Report { level = session.levelIndex, scene = session.gameObject.scene.path };
            Action<string, bool> check = (name, passed) => rows.Add(new Check { name = name, passed = passed });
            bool originalAugments = session.enableAugments;
            int originalSeed = session.augmentSeed;
            GameLanguage originalLanguage = GameSettings.Language;
            float originalVolume = GameSettings.MasterVolume;
            GameObject textFixture = null;
            try
            {
                check("Battlefield has an Esc menu", session.GetComponent<PauseMenu>() != null);
                check("Home and all three distinct battle scenes are registered", new[]
                {
                    "MainMenu", "Level01_ForestOutpost", "Level02_CanyonConfluence", "Level03_CoreFortress"
                }.All(Application.CanStreamedLevelBeLoaded));

                session.enableAugments = false;
                session.RestartLevel();
                Set(session, "Gold", 10000);
                Set(session, "Mana", 500);
                var occupiedPad = session.map.buildSlots[0];
                var emptyPad = session.map.buildSlots[1];
                if (!session.TryBuildTower(occupiedPad, TowerKind.Gun)) throw new InvalidOperationException("Cannot build the menu fixture tower.");
                TowerAgent tower = session.GetTower(occupiedPad);
                Vector3 spellPoint = session.map.routes[0].waypoints[0].position;
                session.ToggleSpeed();
                check("Combat fixture runs at double speed", session.IsCombatRunning && !session.IsPaused && Mathf.Approximately(session.GameSpeed, 2f));
                float assaultTime = session.AssaultTime;
                int enemies = session.ActiveEnemies.Count;
                int gold = session.Gold, mana = session.Mana, towerLevel = tower.Level;
                int spellsCast = session.SpellsCast;
                session.OpenSystemMenu();
                check("Opening the menu stops combat and simulation time", session.IsMenuOpen && !session.IsCombatRunning && Mathf.Approximately(Time.timeScale, 0f));
                session.OpenSystemMenu();
                check("Opening an already open menu is idempotent", session.IsMenuOpen && Mathf.Approximately(Time.timeScale, 0f));
                typeof(LevelSession).GetMethod("Update", Private).Invoke(session, null);
                check("The assault clock and spawn count remain still behind the menu", Mathf.Approximately(session.AssaultTime, assaultTime) && session.ActiveEnemies.Count == enemies);
                check("Building is blocked behind the menu", !session.TryBuildTower(emptyPad, TowerKind.Gun) && !session.HasTower(emptyPad));
                check("Tower upgrades are blocked behind the menu", !session.TryUpgradeTower(occupiedPad) && tower.Level == towerLevel);
                check("Fireball is blocked behind the menu", !session.TryCastFireball(spellPoint));
                if (session.FrostSpellUnlocked) check("Frost Nova is blocked behind the menu", !session.TryCastFrostNova(spellPoint));
                if (session.ReinforcementsUnlocked) check("Reinforcements are blocked behind the menu", !session.TryCastReinforcements(spellPoint));
                if (session.BattlecryUnlocked) check("Battlecry is blocked behind the menu", !session.TryCastBattlecry());
                if (session.SpellUpgradeUnlocked)
                    foreach (SpellKind kind in Enum.GetValues(typeof(SpellKind)))
                        check(kind + " upgrades are blocked behind the menu", !session.CanUpgradeSpell(kind) && !session.TryUpgradeSpell(kind));
                session.ToggleSpeed();
                session.TogglePause();
                check("Pause and speed shortcuts cannot change the menu snapshot", Mathf.Approximately(session.GameSpeed, 2f) && Mathf.Approximately(Time.timeScale, 0f));
                check("Blocked actions spend no resources or spell casts", session.Gold == gold && session.Mana == mana && session.SpellsCast == spellsCast);
                session.CloseSystemMenu();
                check("Closing the menu restores double-speed combat", !session.IsMenuOpen && session.IsCombatRunning && !session.IsPaused && Mathf.Approximately(Time.timeScale, 2f));
                session.CloseSystemMenu();
                check("Closing an already closed menu does not alter combat speed", !session.IsMenuOpen && session.IsCombatRunning && Mathf.Approximately(Time.timeScale, 2f));

                session.TogglePause();
                session.OpenSystemMenu();
                session.CloseSystemMenu();
                check("A previously paused battle stays paused after closing the menu", !session.IsMenuOpen && session.IsPaused && !session.IsCombatRunning && Mathf.Approximately(Time.timeScale, 0f));
                session.TogglePause();
                check("Manually resuming preserves the previous speed", session.IsCombatRunning && Mathf.Approximately(Time.timeScale, 2f));

                session.enableAugments = true;
                session.augmentSeed = 841037;
                session.RestartLevel();
                AugmentId[] offers = Offers(session);
                int choiceNumber = session.AugmentChoiceNumber;
                int selected = session.SelectedAugments.Count;
                gold = session.Gold; mana = session.Mana;
                session.OpenSystemMenu();
                check("Esc menu can open during an augment choice", session.IsMenuOpen && session.IsChoosingAugment && Mathf.Approximately(Time.timeScale, 0f));
                check("A card cannot be selected through the menu", !session.TryChooseAugment(0));
                if (session.AugmentRefreshUnlocked)
                    check("A card cannot be refreshed through the menu", !session.CanRefreshAugment(0) && !session.TryRefreshAugment(0));
                check("Menu actions preserve offered and equipped augments", Offers(session).SequenceEqual(offers) && session.AugmentChoiceNumber == choiceNumber
                    && session.SelectedAugments.Count == selected && session.Gold == gold && session.Mana == mana);

                GameSettings.SetLanguage(GameLanguage.English);
                textFixture = new GameObject("Localization boundary fixture", typeof(RectTransform), typeof(Canvas));
                var textObject = new GameObject("Dynamic text", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(textFixture.transform, false);
                var text = textObject.GetComponent<TextMeshProUGUI>();
                text.text = "START GAME";
                var localized = textFixture.AddComponent<LocalizedCanvas>();
                localized.RefreshNow();
                GameSettings.SetLanguage(GameLanguage.Chinese);
                localized.RefreshNow();
                check("Static labels switch to Chinese immediately", text.text == "开始游戏");
                GameSettings.SetLanguage(GameLanguage.English);
                localized.RefreshNow();
                check("Static labels retain their original English source", text.text == "START GAME");
                text.text = "NEXT WAVE IN 24s";
                localized.RefreshNow();
                GameSettings.SetLanguage(GameLanguage.Chinese);
                localized.RefreshNow();
                check("Formatted labels preserve numbers while translating", text.text == "下一波：24 秒");
                text.text = "NEXT WAVE IN 23s";
                localized.RefreshNow();
                check("Dynamic text updates remain localized", text.text == "下一波：23 秒");
                GameSettings.SetLanguage(GameLanguage.English);
                localized.RefreshNow();
                check("Returning to English restores the latest dynamic source", text.text == "NEXT WAVE IN 23s");
                GameSettings.SetLanguage(GameLanguage.Chinese);
                foreach (var card in AugmentCatalog.All)
                    check("Chinese augment: " + card.Name, Localization.T(card.Name).Any(Cjk) && Localization.T(card.Description).Any(Cjk));
                string[] formatted =
                {
                    "BOSS / ENRAGED    900 / 2500 HP", "SUPPORT / LV 3", "LV 3 / 140 GOLD",
                    "3/3 soldiers · 1/1 hero\nSoldier HP 140 / ATK 18 / revive 8/15s",
                    "Vulnerability +25% / 3s\nAllies heal 5 HP/s / range 5.5",
                    "LATE GAME CHOICE / 3 OF 3 / BATTLE PAUSED WHILE YOU CHOOSE",
                    "Battlecry upgraded to level 3. 12s / DMG +50% / SPEED +55% / R +1.6"
                };
                check("Advanced tower, boss, spell and card details translate", formatted.All(s => Localization.T(s).Any(Cjk)));
                TMP_FontAsset font = Resources.Load<TMP_FontAsset>("UI/ChineseFont");
                char[] chineseCharacters = Localization.ChineseTexts.SelectMany(t => t).Where(Cjk).Distinct().ToArray();
                check("Chinese font asset is present", font != null);
                check("Chinese font contains every exact translation character", font != null && chineseCharacters.All(c => font.HasCharacter(c, true, false)));
                check("Changing language does not reroll or select cards", Offers(session).SequenceEqual(offers) && session.AugmentChoiceNumber == choiceNumber
                    && session.SelectedAugments.Count == selected && session.IsChoosingAugment && session.IsMenuOpen && Mathf.Approximately(Time.timeScale, 0f));
                GameSettings.SetVolume(.37f);
                check("Master volume applies while the game is paused", Mathf.Approximately(GameSettings.MasterVolume, .37f) && Mathf.Approximately(AudioListener.volume, .37f)
                    && session.IsMenuOpen && Mathf.Approximately(Time.timeScale, 0f));
                session.CloseSystemMenu();
                check("Closing settings returns to the same paused augment choice", !session.IsMenuOpen && session.IsChoosingAugment && Mathf.Approximately(Time.timeScale, 0f)
                    && Offers(session).SequenceEqual(offers));
                check("Augment selection works after the menu is closed", session.TryChooseAugment(0) && !session.IsChoosingAugment && session.IsCombatRunning);
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                check("Validation completed without exception", false);
            }
            finally
            {
                if (textFixture) UnityEngine.Object.DestroyImmediate(textFixture);
                GameSettings.SetLanguage(originalLanguage);
                GameSettings.SetVolume(originalVolume);
                session.enableAugments = originalAugments;
                session.augmentSeed = originalSeed;
                if (session.IsMenuOpen) session.CloseSystemMenu();
                session.RestartLevel();
            }
            report.checks = rows.ToArray();
            report.failures = rows.Count(c => !c.passed);
            report.passed = report.failures == 0;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "FrontendGameplay");
            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(directory, "boundary-validation.json"), json);
            File.WriteAllText(Path.Combine(directory, "boundary-validation-level" + session.levelIndex + ".json"), json);
            return report;
        }
    }
}
