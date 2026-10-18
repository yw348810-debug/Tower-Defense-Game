using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerDefense.Gameplay;
using TowerDefense.Maps;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    /// <summary>Editor-only, non-blocking playthroughs using the same purchases as a player.</summary>
    [InitializeOnLoad]
    public static class RicherBattleValidation
    {
        private const double TimeoutSeconds = 120;
        private static LevelSession session;
        private static BuildSlot[] slots;
        private static string strategy;
        private static double startedAt;
        private static double nextEconomyAt;
        private static double nextSpellCheckAt;
        private static float simulationSpeed;
        private static bool running;
        private static RunReport lastReport;
        private static string preferredSilver = "";
        private static string preferredGold = "";

        [Serializable]
        public sealed class RunReport
        {
            public string strategy;
            public string phase;
            public string outcome;
            public bool timedOut;
            public int kills;
            public int leaks;
            public int HP;
            public int gold;
            public int mana;
            public int spells;
            public int spellTicks;
            public int shots;
            public int upgrades;
            public int shatters;
            public int towercount;
            public int wave;
            public float assaultTime;
            public float realElapsed;
            public float simulationSpeed;
            public string[] slotOrder;
            public string error;
            public string[] augments;
        }

        [Serializable]
        public sealed class CheckResult
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        public sealed class CriticalReport
        {
            public bool passed;
            public int failures;
            public CheckResult[] checks;
            public string error;
        }

        static RicherBattleValidation()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                if (running)
                    Finish("AssemblyReload", false);
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode && running)
                    Finish("PlayModeExited", false);
            };
        }

        /// <summary>Strategies: none, gun, mixed, mixed_spells. Remains in Play Mode after saving results.</summary>
        public static void BeginRun(string requestedStrategy, string silver = "", string gold = "")
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("BeginRun requires Play Mode.");
            string selected = (requestedStrategy ?? "").Trim().ToLowerInvariant();
            if (selected != "none" && selected != "gun" && selected != "mixed" && selected != "mixed_spells" && selected != "blast_spells")
                throw new ArgumentException("Use none, gun, mixed, mixed_spells, or blast_spells.", nameof(requestedStrategy));
            if (running)
                Finish("ReplacedByNewRun", false);
            session = FindSession();
            slots = SortedSlots(session);
            if (slots.Length < 6)
                throw new InvalidOperationException("The first-level strategies require at least six build pads.");
            strategy = selected;
            preferredSilver = silver;
            preferredGold = gold;
            session.RestartLevel();
            ChooseAvailableAugment();
            startedAt = EditorApplication.timeSinceStartup;
            nextEconomyAt = startedAt;
            nextSpellCheckAt = startedAt;
            session.ToggleSpeed();
            simulationSpeed = session.GameSpeed;
            running = true;
            lastReport = Snapshot();
            EditorApplication.update += Tick;
            // The opening purchases happen immediately, before the first simulated frame.
            if (strategy != "none")
                for (int i = 0; i < 3; i++)
                    PerformEconomyAction();
            Debug.Log($"Started {strategy} playthrough. Pads: {string.Join(", ", slots.Select(s => s.slotId))}");
        }

        private static void Tick()
        {
            if (!running)
                return;
            try
            {
                if (!EditorApplication.isPlaying || session == null)
                {
                    Finish("SessionUnavailable", false);
                    return;
                }
                if (session.Phase == SessionPhase.Victory || session.Phase == SessionPhase.Defeat)
                {
                    Finish(session.Phase.ToString(), false);
                    return;
                }
                double now = EditorApplication.timeSinceStartup;
                if (now - startedAt >= TimeoutSeconds)
                {
                    Finish("Timeout", true);
                    return;
                }
                if (session.IsPaused)
                    return;
                if (session.IsChoosingAugment)
                    ChooseAvailableAugment();
                if (now >= nextEconomyAt)
                {
                    nextEconomyAt = now + 0.1;
                    PerformEconomyAction();
                    PerformSpellAction(now);
                    lastReport = Snapshot();
                }
            }
            catch (Exception exception)
            {
                if (lastReport != null)
                    lastReport.error = exception.ToString();
                Finish("Exception", false);
                Debug.LogException(exception);
            }
        }

        private static void PerformEconomyAction()
        {
            if (strategy == "none")
                return;
            if (strategy == "blast_spells")
            {
                // A spell build uses early area damage on the shared road instead of an early slow tower.
                if (TryOpeningBuild(0, TowerKind.Gun)) return;
                if (TryOpeningBuild(2, TowerKind.Cannon)) return;
                if (TryOpeningBuild(3, TowerKind.Frost)) return;
                if (session.GetTower(slots[2]).UpgradeLevel == 0 && TryUpgrade(2)) return;
                if (session.GetTower(slots[3]).UpgradeLevel == 0 && TryUpgrade(3)) return;
                if (TryUpgrade(2)) return;
                if (TryAdditionalBuild(1, TowerKind.Gun)) return;
                if (TryAdditionalBuild(4, TowerKind.Gun)) return;
                if (TryUpgrade(3) || TryUpgrade(4)) return;
                if (TryAdditionalBuild(5, TowerKind.Cannon)) return;
                if (TryUpgrade(5) || TryUpgrade(0) || TryUpgrade(1)) return;
                return;
            }
            if (strategy == "gun")
            {
                for (int i = 0; i < 6; i++)
                {
                    if (session.GetTower(slots[i]) != null)
                        continue;
                    if (session.Gold >= session.GetTowerCost(TowerKind.Gun))
                        session.TryBuildTower(slots[i], TowerKind.Gun);
                    return;
                }
                return;
            }

            // Establish one entry gun and a frost/cannon pair at the shared route.
            if (TryOpeningBuild(0, TowerKind.Gun)) return;
            if (TryOpeningBuild(2, TowerKind.Frost)) return;
            if (TryOpeningBuild(3, TowerKind.Cannon)) return;

            // Improve the choke point before expanding into the later route.
            // Prioritize one upgrade on each choke-point tower before taking either to tier three.
            if (session.GetTower(slots[3]).UpgradeLevel == 0 && TryUpgrade(3)) return;
            if (session.GetTower(slots[2]).UpgradeLevel == 0 && TryUpgrade(2)) return;
            if (TryUpgrade(3) || TryUpgrade(2)) return;
            if (TryAdditionalBuild(1, TowerKind.Gun)) return;
            if (TryAdditionalBuild(4, TowerKind.Gun)) return;
            if (TryUpgrade(4)) return;
            if (TryAdditionalBuild(5, TowerKind.Cannon)) return;
            if (TryUpgrade(5) || TryUpgrade(0) || TryUpgrade(1)) return;
        }

        private static void ChooseAvailableAugment()
        {
            if (!session.IsChoosingAugment || session.AugmentOffers.Count == 0) return;
            string preferred = session.AugmentOffers[0].Tier == AugmentTier.Silver ? preferredSilver : preferredGold;
            int choice = -1;
            for (int i = 0; i < session.AugmentOffers.Count; i++)
                if (session.AugmentOffers[i].Id.ToString() == preferred) choice = i;
            if (choice < 0)
                for (int i = 0; i < session.AugmentOffers.Count; i++)
                    if (session.AugmentOffers[i].IsGeneric) { choice = i; break; }
            session.TryChooseAugment(choice < 0 ? 0 : choice);
        }

        // True means the opening still needs this pad, even if the player cannot afford it yet.
        private static bool TryOpeningBuild(int index, TowerKind kind)
        {
            if (session.GetTower(slots[index]) != null)
                return false;
            if (session.Gold >= session.GetTowerCost(kind))
                session.TryBuildTower(slots[index], kind);
            return true;
        }

        private static bool TryAdditionalBuild(int index, TowerKind kind)
        {
            if (session.GetTower(slots[index]) != null || session.Gold < session.GetTowerCost(kind))
                return false;
            return session.TryBuildTower(slots[index], kind);
        }

        private static bool TryUpgrade(int index)
        {
            TowerAgent tower = session.GetTower(slots[index]);
            if (tower == null || tower.IsMaxLevel || session.Gold < tower.UpgradeCost)
                return false;
            return session.TryUpgradeTower(slots[index]);
        }

        private static void PerformSpellAction(double now)
        {
            if ((strategy != "mixed_spells" && strategy != "blast_spells") || now < nextSpellCheckAt || !session.IsCombatRunning ||
                session.ActiveSpellCount != 0 || session.Mana < session.FireballCost)
                return;
            nextSpellCheckAt = now + 1.5;

            EnemyAgent best = null;
            int bestScore = -1;
            var enemies = session.ActiveEnemies;
            float radiusSquared = session.FireballRadius * session.FireballRadius;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent center = enemies[i];
                if (center == null || !center.IsAlive || !session.IsSpellPointValid(center.transform.position))
                    continue;
                int nearby = 0;
                int heavy = 0;
                for (int j = 0; j < enemies.Count; j++)
                {
                    EnemyAgent enemy = enemies[j];
                    if (enemy == null || !enemy.IsAlive)
                        continue;
                    Vector3 offset = enemy.transform.position - center.transform.position;
                    if (offset.x * offset.x + offset.z * offset.z > radiusSquared)
                        continue;
                    nearby++;
                    if (enemy.MaxHealth >= 200 && enemy.CurrentHealth > 60)
                        heavy++;
                }
                // Hold mana for a visible cluster; a surviving late heavy is also a useful target.
                bool lateHeavy = session.AssaultProgress >= .75f && center.MaxHealth >= 200 && center.CurrentHealth > 60;
                if (nearby < 3 && !lateHeavy)
                    continue;
                int score = nearby * 10 + heavy * 8 + (center.RemainingDistance < 20f ? 3 : 0);
                if (score <= bestScore)
                    continue;
                bestScore = score;
                best = center;
            }
            if (best != null && best.IsAlive)
                session.TryCastFireball(best.transform.position);
        }

        private static RunReport Snapshot()
        {
            if (session == null)
                return lastReport ?? new RunReport { strategy = strategy };
            return new RunReport
            {
                strategy = strategy,
                phase = session.Phase.ToString(),
                kills = session.Kills,
                leaks = session.Leaks,
                HP = session.BaseHealth,
                gold = session.Gold,
                mana = session.Mana,
                spells = session.SpellsCast,
                spellTicks = session.SpellDamageTicks,
                shots = session.ShotsFired,
                upgrades = session.UpgradesPurchased,
                shatters = session.ShattersTriggered,
                towercount = session.BuiltTowerCount,
                wave = session.WaveNumber,
                assaultTime = session.AssaultTime,
                realElapsed = (float)(EditorApplication.timeSinceStartup - startedAt),
                simulationSpeed = simulationSpeed,
                augments = session.SelectedAugments.Select(a => a.Id.ToString()).ToArray(),
                slotOrder = slots?.Select(s => s != null ? s.slotId : "missing").ToArray()
            };
        }

        private static void Finish(string outcome, bool timedOut)
        {
            if (!running)
                return;
            EditorApplication.update -= Tick;
            running = false;
            string priorError = lastReport?.error;
            RunReport report = Snapshot();
            report.outcome = outcome;
            report.timedOut = timedOut;
            report.error = priorError;
            report.realElapsed = (float)(EditorApplication.timeSinceStartup - startedAt);
            Time.timeScale = session != null && session.Phase == SessionPhase.Combat
                ? session.IsPaused ? 0f : session.GameSpeed : 1f;
            Save("Run_" + strategy + ".json", report);
            Debug.Log($"Playthrough {strategy}: {outcome}; kills {report.kills}, leaks {report.leaks}, " +
                $"HP {report.HP}, towers {report.towercount}, upgrades {report.upgrades}, shatters {report.shatters}, " +
                $"mana {report.mana}, spells {report.spells}, spell ticks {report.spellTicks}, speed {report.simulationSpeed}x.");
            session = null;
            slots = null;
        }

        /// <summary>Exercise failed purchases, upgrade limits, pause/speed, and a clean restart.</summary>
        public static void ValidateTransactionsAndPause()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Validation requires Play Mode.");
            if (running)
                Finish("TransactionsValidation", false);
            var checks = new List<CheckResult>();
            var report = new CriticalReport();
            LevelSession level = null;
            try
            {
                level = FindSession();
                BuildSlot[] pads = SortedSlots(level);
                if (pads.Length < 3)
                    throw new InvalidOperationException("At least three pads are required.");
                level.RestartLevel();
                int initialGold = level.Gold;
                int initialHealth = level.BaseHealth;
                int gunCost = level.GetTowerCost(TowerKind.Gun);
                Check(checks, "Initial restart", initialGold == level.map.initialGold && initialHealth == level.map.baseHealth &&
                    level.BuiltTowerCount == 0 && level.ActiveEnemies.Count == 0 && level.WaveNumber == 1,
                    $"Gold {initialGold}, HP {initialHealth}, wave {level.WaveNumber}.");
                bool built = level.TryBuildTower(pads[0], TowerKind.Gun);
                TowerAgent first = level.GetTower(pads[0]);
                Check(checks, "Exact build payment", built && first != null && level.Gold == initialGold - gunCost && level.BuiltTowerCount == 1,
                    $"Cost {gunCost}, remaining {level.Gold}.");
                int before = level.Gold;
                int count = level.BuiltTowerCount;
                bool duplicate = level.TryBuildTower(pads[0], TowerKind.Cannon);
                Check(checks, "Duplicate build rejected without payment", !duplicate && level.Gold == before && level.BuiltTowerCount == count,
                    $"Accepted {duplicate}, gold {before} -> {level.Gold}.");
                before = level.Gold;
                bool invalid = level.TryBuildTower(null, TowerKind.Gun);
                Check(checks, "Invalid pad rejected", !invalid && level.Gold == before, $"Accepted {invalid}.");
                if (first != null)
                {
                    int price = first.UpgradeCost;
                    int damage = first.damage;
                    float range = first.range;
                    int baseDamage = damage;
                    float baseRange = range;
                    float baseInterval = first.fireInterval;
                    before = level.Gold;
                    bool upgraded = level.TryUpgradeTower(pads[0]);
                    Check(checks, "First upgrade pays and reaches level 2", upgraded && first.IsUpgraded && !first.IsMaxLevel && first.Level == 2 &&
                        level.Gold == before - price && first.damage == Mathf.RoundToInt(baseDamage * 1.65f) &&
                        Mathf.Approximately(first.range, baseRange + .6f) && Mathf.Approximately(first.fireInterval, baseInterval * .85f),
                        $"Price {price}, damage {damage} -> {first.damage}, range {range} -> {first.range}.");
                    before = level.Gold;
                    damage = first.damage;
                    range = first.range;
                    price = first.UpgradeCost;
                    bool twice = level.TryUpgradeTower(pads[0]);
                    Check(checks, "Second upgrade pays and reaches level 3", twice && first.Level == 3 && first.UpgradeLevel == 2 && first.IsMaxLevel &&
                        level.Gold == before - price && first.damage == Mathf.RoundToInt(baseDamage * 1.65f * 1.45f) &&
                        Mathf.Approximately(first.range, baseRange + 1.1f) && Mathf.Approximately(first.fireInterval, baseInterval * .85f * .9f),
                        $"Price {price}, damage {damage} -> {first.damage}, range {range} -> {first.range}, gold {before} -> {level.Gold}.");
                    Check(checks, "Level 3 visual is the final model", first.finalVisual != null && first.finalVisual.activeSelf &&
                        first.baseVisual != null && !first.baseVisual.activeSelf && first.upgradedVisual != null && !first.upgradedVisual.activeSelf,
                        $"Base {first.baseVisual?.activeSelf}, upgraded {first.upgradedVisual?.activeSelf}, final {first.finalVisual?.activeSelf}.");
                    before = level.Gold;
                    damage = first.damage;
                    range = first.range;
                    int upgrades = level.UpgradesPurchased;
                    bool third = level.TryUpgradeTower(pads[0]);
                    Check(checks, "Third upgrade rejected without payment", !third && level.Gold == before && first.Level == 3 &&
                        first.damage == damage && first.range == range && level.UpgradesPurchased == upgrades,
                        $"Accepted {third}, gold {before} -> {level.Gold}, level {first.Level}.");
                }
                before = level.Gold;
                bool emptyUpgrade = level.TryUpgradeTower(pads[2]);
                Check(checks, "Empty pad upgrade rejected", !emptyUpgrade && level.Gold == before, $"Accepted {emptyUpgrade}.");
                // Spend only through valid purchases. Leave one empty pad to attempt an unaffordable build.
                for (int i = 1; i < pads.Length - 1; i++)
                {
                    if (level.Gold >= gunCost)
                        level.TryBuildTower(pads[i], TowerKind.Gun);
                }
                before = level.Gold;
                count = level.BuiltTowerCount;
                bool enoughMoney = before >= gunCost;
                bool unaffordable = level.TryBuildTower(pads[pads.Length - 1], TowerKind.Gun);
                Check(checks, "Insufficient funds rejected without payment", !enoughMoney && !unaffordable && level.Gold == before && level.BuiltTowerCount == count,
                    $"Remaining {before}, cost {gunCost}, accepted {unaffordable}.");

                level.TogglePause();
                Check(checks, "Pause stops simulation", level.IsPaused && Mathf.Approximately(Time.timeScale, 0f), $"Time scale {Time.timeScale}.");
                level.ToggleSpeed();
                Check(checks, "Speed toggle stays paused", level.IsPaused && Mathf.Approximately(Time.timeScale, 0f) && Mathf.Approximately(level.GameSpeed, 2f),
                    $"Time scale {Time.timeScale}, selected speed {level.GameSpeed}.");
                level.TogglePause();
                Check(checks, "Resume restores selected speed", !level.IsPaused && Mathf.Approximately(Time.timeScale, 2f), $"Time scale {Time.timeScale}.");
                level.TogglePause();
                level.RestartLevel();
                Check(checks, "Restart resets all battle state", !level.IsPaused && Mathf.Approximately(Time.timeScale, 1f) && Mathf.Approximately(level.GameSpeed, 1f) &&
                    level.Gold == initialGold && level.BaseHealth == initialHealth && level.BuiltTowerCount == 0 && level.ActiveEnemies.Count == 0 &&
                    level.ActiveProjectileCount == 0 && level.WaveNumber == 1 && level.Kills == 0 && level.Leaks == 0 && level.ShotsFired == 0 &&
                    level.UpgradesPurchased == 0 && level.ShattersTriggered == 0 && level.Mana == 0 && level.SpellsCast == 0 &&
                    level.SpellDamageTicks == 0 && level.ActiveSpellCount == 0 &&
                    Mathf.Approximately(level.AssaultTime, 0f) && Mathf.Approximately(level.AssaultProgress, 0f),
                    $"Gold {level.Gold}, HP {level.BaseHealth}, progress {level.AssaultProgress}, paused {level.IsPaused}.");
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                Check(checks, "Validation completed", false, exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                if (level != null)
                    level.RestartLevel();
                Time.timeScale = 1f;
            }
            report.checks = checks.ToArray();
            report.failures = checks.Count(c => !c.passed);
            report.passed = report.failures == 0;
            Save("CriticalChecks.json", report);
            Debug.Log($"Transactions/pause validation: {checks.Count - report.failures}/{checks.Count} passed.");
        }

        private static void Check(List<CheckResult> checks, string name, bool passed, string detail)
            => checks.Add(new CheckResult { name = name, passed = passed, detail = detail });

        private static LevelSession FindSession()
        {
            LevelSession level = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (level == null || level.map == null)
                throw new InvalidOperationException("No active LevelSession and map.");
            List<string> errors = level.GetConfigurationErrors();
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join("\n", errors));
            return level;
        }

        private static BuildSlot[] SortedSlots(LevelSession level)
            => level.map.buildSlots.Where(s => s != null).OrderBy(s => s.slotId, StringComparer.Ordinal).ToArray();

        private static void Save(string name, object report)
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string destination = Path.Combine(project, "Captures", "AugmentGameplay");
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, name), JsonUtility.ToJson(report, true));
        }
    }
}
