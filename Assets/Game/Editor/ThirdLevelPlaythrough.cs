using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerDefense.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    /// <summary>Plays the saved snow map using its ordinary economy and the public player actions.</summary>
    [InitializeOnLoad]
    public static class ThirdLevelPlaythrough
    {
        static LevelSession session;
        static double began, nextAction;
        static string taskStrategy, lastError;
        static bool running;
        static int runSeed, goldSpent, seenBlocking, seenHeroes, seenMissingTroops;
        static int seenVulnerability, seenBattlecry, seenRage, peakReinforcements;
        static readonly int[] refreshedByChoice = new int[3];
        static readonly HashSet<string> seenBossModels = new HashSet<string>();
        static readonly SpellKind[] spellOrder =
        { SpellKind.Fireball, SpellKind.Reinforcements, SpellKind.FrostNova, SpellKind.Battlecry };

        [Serializable]
        public sealed class Report
        {
            public string strategy, phase, error;
            public bool running;
            public int seed, level, kills, leaks, hp, shield, waves, towers, towerUpgrades, gold, mana, goldSpent;
            public float assaultTime, seconds;
            public int fireballs, frostCasts, reinforcementsCast, battlecriesCast, spellUpgrades, refreshes;
            public int bossesSpawned, bossesKilled, bossesLeaked;
            public int blockingObservations, missingTroopObservations, heroObservations;
            public int vulnerabilityObservations, battlecryObservations, rageObservations, peakReinforcements;
            public int[] spellLevels, refreshesByChoice;
            public string[] augments, towerLevels, bossModels;
        }

        static ThirdLevelPlaythrough()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => { if (running) Finish("Assembly reload"); };
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.ExitingPlayMode && running) Finish("Play Mode exited"); };
        }

        public static void Begin(string strategy = "mixed", int seed = 9137)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Requires Play Mode.");
            if (running) Finish("Replaced by another run");
            session = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (!session || session.levelIndex != 3 || !session.map || session.map.buildSlots.Length != 10)
                throw new InvalidOperationException("Requires the saved third snow map and its ten build pads.");
            taskStrategy = strategy;
            runSeed = seed;
            lastError = null;
            goldSpent = seenBlocking = seenHeroes = seenMissingTroops = 0;
            seenVulnerability = seenBattlecry = seenRage = peakReinforcements = 0;
            Array.Clear(refreshedByChoice, 0, refreshedByChoice.Length);
            seenBossModels.Clear();
            session.augmentSeed = seed;
            session.enableAugments = true;
            session.RestartLevel();
            began = EditorApplication.timeSinceStartup;
            nextAction = 0;
            running = true;
            Choose();
            if (session.GameSpeed < 2f) session.ToggleSpeed();
            for (int i = 0; i < 4; i++) Buy();
            EditorApplication.update += Tick;
        }

        static void Choose()
        {
            if (!session.IsChoosingAugment) return;
            int choice = Mathf.Clamp(session.AugmentChoiceNumber - 1, 0, 2);
            // Refresh the lowest-priority offer once in every choice, using the same free slot action as the UI.
            if (refreshedByChoice[choice] == 0)
            {
                int worst = -1, worstRank = -1;
                for (int i = 0; i < session.AugmentOffers.Count; i++)
                {
                    if (!session.CanRefreshAugment(i)) continue;
                    int rank = Priority(session.AugmentOffers[i].Id);
                    if (rank > worstRank) { worst = i; worstRank = rank; }
                }
                if (worst >= 0 && session.TryRefreshAugment(worst)) refreshedByChoice[choice]++;
            }
            int best = 0, bestRank = int.MaxValue;
            for (int i = 0; i < session.AugmentOffers.Count; i++)
            {
                int rank = Priority(session.AugmentOffers[i].Id);
                if (rank < bestRank) { best = i; bestRank = rank; }
            }
            session.TryChooseAugment(best);
        }

        static int Priority(AugmentId id)
        {
            AugmentId[] order =
            {
                AugmentId.ConstructionGrant, AugmentId.StoredMana, AugmentId.LongRange,
                AugmentId.LuckyLoot, AugmentId.FieldWorkshop, AugmentId.HeavyHunter,
                AugmentId.ExtendedBurn, AugmentId.VeteranTraining,
                AugmentId.Aftershock, AugmentId.ManaFurnace, AugmentId.ElementalBoil,
                AugmentId.TwinMeteor, AugmentId.PolarPulse, AugmentId.UltimateRetrofit,
                AugmentId.VanguardCommand, AugmentId.PiercingRounds, AugmentId.LastStandEngine
            };
            int rank = Array.IndexOf(order, id);
            return rank < 0 ? 100 : rank;
        }

        static bool Build(int pad, TowerKind kind)
        {
            var slot = session.map.buildSlots[pad];
            if (session.GetTower(slot) || session.Gold < session.GetTowerCost(kind)) return false;
            int before = session.Gold;
            if (!session.TryBuildTower(slot, kind)) return false;
            goldSpent += before - session.Gold;
            return true;
        }

        static bool Upgrade(int pad, int level)
        {
            var slot = session.map.buildSlots[pad];
            var tower = session.GetTower(slot);
            if (!tower || tower.Level >= level || session.Gold < tower.UpgradeCost) return false;
            int before = session.Gold;
            if (!session.TryUpgradeTower(slot)) return false;
            goldSpent += before - session.Gold;
            return true;
        }

        static bool UpgradeSpell(SpellKind kind, int level)
        {
            if (session.GetSpellLevel(kind) >= level || !session.CanUpgradeSpell(kind)) return false;
            int before = session.Gold;
            if (!session.TryUpgradeSpell(kind)) return false;
            goldSpent += before - session.Gold;
            return true;
        }

        static void Buy()
        {
            if (taskStrategy == "none" || !session.IsCombatRunning) return;
            // Slow and shatter on the upper lane funds the same pair on the lower lane.
            // The first three towers cost250; complete the second pair before buying peripheral upgrades.
            int[] starters = { 2, 6, 1, 5 };
            TowerKind[] kinds = { TowerKind.Cannon, TowerKind.Cannon, TowerKind.Frost, TowerKind.Frost };
            for (int i = 0; i < starters.Length; i++)
                if (!session.GetTower(session.map.buildSlots[starters[i]]))
                { Build(starters[i], kinds[i]); return; }

            // Reserve for the two critical damage upgrades before adding cheaper peripheral towers.
            // Their damage unlocks the kill income needed for the rest of this ordinary funded strategy.
            foreach (int pad in new[] { 2, 6 })
                if (session.GetTower(session.map.buildSlots[pad]).Level < 2)
                { Upgrade(pad, 2); return; }
            if (session.AssaultTime >= 40f && session.GetSpellLevel(SpellKind.Fireball) < 2)
            { UpgradeSpell(SpellKind.Fireball, 2); return; }
            foreach (int pad in new[] { 2, 6 })
                if (session.GetTower(session.map.buildSlots[pad]).Level < 3)
                { Upgrade(pad, 3); return; }

            if (Build(3, TowerKind.Barracks)) return;
            if (Build(8, TowerKind.Support)) return;
            if (Upgrade(3, 2)) return;
            if (Build(9, TowerKind.Gun)) return;
            if (Upgrade(1, 2) || Upgrade(5, 2)) return;
            if (Build(7, TowerKind.Barracks)) return;
            if (Upgrade(7, 2)) return;
            if (Build(0, TowerKind.Gun) || Build(4, TowerKind.Gun)) return;
            if (Upgrade(8, 3) || Upgrade(1, 2) || Upgrade(5, 2)) return;
            foreach (var kind in spellOrder) if (UpgradeSpell(kind, 2)) return;
            if (Upgrade(3, 3) || Upgrade(7, 3) || Upgrade(9, 3)) return;
            foreach (var kind in spellOrder) if (UpgradeSpell(kind, 3)) return;
            foreach (int pad in new[] { 1, 5, 0, 4 }) if (Upgrade(pad, 3)) return;
        }

        static float FlatDistanceSquared(Vector3 a, Vector3 b)
        { Vector3 d = a - b; return d.x * d.x + d.z * d.z; }

        static bool IsCovered(EnemyAgent enemy)
        {
            foreach (var slot in session.map.buildSlots)
            {
                var tower = session.GetTower(slot);
                if (!tower || tower.kind == TowerKind.Support) continue;
                if (FlatDistanceSquared(enemy.transform.position, tower.transform.position)
                    <= tower.EffectiveRange * tower.EffectiveRange) return true;
            }
            return false;
        }

        static void Spells()
        {
            if (taskStrategy == "none" || !session.IsCombatRunning) return;
            var enemies = session.ActiveEnemies;
            var boss = enemies.Where(e => e && e.IsAlive && e.IsBoss)
                .OrderBy(e => e.RemainingDistance).FirstOrDefault();
            float radiusSquared = session.FireballRadius * session.FireballRadius;
            var cluster = enemies.Where(e => e && e.IsAlive && session.IsSpellPointValid(e.transform.position))
                .Select(e => new
                {
                    enemy = e,
                    score = enemies.Count(other => other && other.IsAlive
                        && FlatDistanceSquared(other.transform.position, e.transform.position) <= radiusSquared)
                        + (e.MaxHealth >= 400 ? 2 : 0) + (e.RemainingDistance < 16f ? 2 : 0)
                        + (IsCovered(e) ? 2 : 0)
                }).OrderByDescending(e => e.score).FirstOrDefault();
            bool focusBoss = boss && IsCovered(boss);
            if (!focusBoss && (cluster == null || cluster.score < 3)) return;
            var target = focusBoss ? boss : cluster.enemy;
            Vector3 point = target.transform.position;
            var coveredThreats = enemies.Where(e => e && e.IsAlive && IsCovered(e)).ToArray();
            var coveredThreat = focusBoss ? boss : coveredThreats.OrderBy(e => e.RemainingDistance).FirstOrDefault();
            bool busy = focusBoss || coveredThreats.Length >= 4 || coveredThreats.Any(e => e.MaxHealth >= 400);

            // Reserve once for each newly unlocked ability, after the opening defense has established income.
            // This spends the normal mana pool; only the first use waits instead of buying another fireball.
            if (session.AssaultTime >= 70f)
            {
                if (session.ReinforcementsCast == 0)
                {
                    if (coveredThreat && session.CanCastReinforcements)
                        session.TryCastReinforcements(coveredThreat.transform.position);
                    return;
                }
                if (session.BattlecriesCast == 0)
                {
                    if (busy && session.CanCastBattlecry) session.TryCastBattlecry();
                    return;
                }
                if (session.FrostCasts == 0)
                {
                    if (coveredThreat && session.FrostSpellCooldownRemaining <= 0f
                        && session.Mana >= session.FrostSpellCost)
                        session.TryCastFrostNova(coveredThreat.transform.position);
                    return;
                }
            }

            // Boss damage is most effective while the boss is held inside tower coverage.
            if (focusBoss)
            {
                if (session.CanCastReinforcements) session.TryCastReinforcements(point);
                if (session.CanCastBattlecry) session.TryCastBattlecry();
                if (session.FrostSpellCooldownRemaining <= 0f && session.Mana >= session.FrostSpellCost)
                    session.TryCastFrostNova(point);
                if (session.ActiveSpellCount == 0 && session.CanCastFireball) session.TryCastFireball(point);
                return;
            }

            // Early damage spells secure income before spending mana on the longer cooldown abilities.
            if (session.ActiveSpellCount == 0 && session.CanCastFireball)
                session.TryCastFireball(point);
            if (session.AssaultTime < 45f) return;
            int reserve = session.FireballCost;
            if (busy && session.CanCastBattlecry && session.Mana >= session.BattlecryCost + reserve)
                session.TryCastBattlecry();
            if (IsCovered(target) && cluster.score >= 6 && session.CanCastReinforcements
                && session.Mana >= session.ReinforcementCost + reserve)
                session.TryCastReinforcements(point);
            if (session.FrostSpellCooldownRemaining <= 0f && session.Mana >= session.FrostSpellCost
                + reserve && cluster.score >= 6)
                session.TryCastFrostNova(point);
        }

        static void Observe()
        {
            foreach (var enemy in session.ActiveEnemies)
            {
                if (!enemy || !enemy.IsAlive) continue;
                if (enemy.VulnerabilityBonus > 0f) seenVulnerability++;
                if (!enemy.IsBoss) continue;
                seenBossModels.Add(enemy.name.Replace("(Clone)", ""));
                if (enemy.IsEnraged) seenRage++;
            }
            if (session.GetBattleAttackSpeedMultiplier() > 1f) seenBattlecry++;
            peakReinforcements = Mathf.Max(peakReinforcements, session.ActiveReinforcementCount);
            foreach (var slot in session.map.buildSlots)
            {
                var tower = session.GetTower(slot);
                if (!tower || !tower.Barracks) continue;
                var barracks = tower.Barracks;
                if (barracks.AliveSoldierCount < barracks.SoldierCapacity) seenMissingTroops++;
                if (barracks.AliveHeroCount > 0) seenHeroes++;
                seenBlocking += barracks.Troops.Count(s => s && s.IsAlive && s.BlockedEnemyCount > 0);
            }
        }

        static void Tick()
        {
            if (!running) return;
            try
            {
                if (!EditorApplication.isPlaying || !session) { Finish("Session unavailable"); return; }
                if (session.Phase == SessionPhase.Victory || session.Phase == SessionPhase.Defeat)
                { Finish(null); return; }
                if (EditorApplication.timeSinceStartup - began > 330d) { Finish("Timeout"); return; }
                Choose();
                if (EditorApplication.timeSinceStartup < nextAction) return;
                nextAction = EditorApplication.timeSinceStartup + .15d;
                Buy(); Spells(); Observe();
            }
            catch (Exception exception) { Finish(exception.ToString()); }
        }

        public static Report Snapshot() => session ? new Report
        {
            strategy = taskStrategy, phase = session.Phase.ToString(), error = lastError, running = running,
            seed = runSeed, level = session.levelIndex,
            assaultTime = session.AssaultTime, seconds = (float)(EditorApplication.timeSinceStartup - began),
            kills = session.Kills, leaks = session.Leaks, hp = session.BaseHealth, shield = session.BaseShield,
            waves = session.WaveNumber, towers = session.BuiltTowerCount, towerUpgrades = session.UpgradesPurchased,
            gold = session.Gold, mana = session.Mana, goldSpent = goldSpent,
            fireballs = session.PaidFireballs + session.FreeFireballs, frostCasts = session.FrostCasts,
            reinforcementsCast = session.ReinforcementsCast, battlecriesCast = session.BattlecriesCast,
            spellUpgrades = session.SpellUpgradesPurchased, refreshes = session.AugmentRefreshesUsed,
            bossesSpawned = session.BossesSpawned, bossesKilled = session.BossesKilled, bossesLeaked = session.BossesLeaked,
            spellLevels = Enumerable.Range(0, 4).Select(i => session.GetSpellLevel((SpellKind)i)).ToArray(),
            refreshesByChoice = (int[])refreshedByChoice.Clone(),
            blockingObservations = seenBlocking, missingTroopObservations = seenMissingTroops, heroObservations = seenHeroes,
            vulnerabilityObservations = seenVulnerability, battlecryObservations = seenBattlecry,
            rageObservations = seenRage, peakReinforcements = peakReinforcements,
            augments = session.SelectedAugments.Select(card => card.Id.ToString()).ToArray(),
            towerLevels = session.map.buildSlots.Select((slot, index) => new { tower = session.GetTower(slot), index })
                .Where(item => item.tower).Select(item => "T" + (item.index + 1) + " " + item.tower.kind
                    + " L" + item.tower.Level).ToArray(),
            bossModels = seenBossModels.OrderBy(name => name).ToArray()
        } : null;

        static void Finish(string error)
        {
            running = false;
            lastError = error;
            EditorApplication.update -= Tick;
            var report = Snapshot();
            if (report == null) return;
            string safeStrategy = new string((taskStrategy ?? "run").Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
            Directory.CreateDirectory("Captures/ThirdLevelGameplay");
            File.WriteAllText("Captures/ThirdLevelGameplay/run-" + safeStrategy + ".json", JsonUtility.ToJson(report, true));
            Debug.Log("Third level playthrough: " + JsonUtility.ToJson(report));
        }
    }
}
