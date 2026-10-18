using System;
using System.IO;
using System.Linq;
using TowerDefense.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    /// <summary>Runs an ordinary funded strategy through both canyon entrances without changing combat stats.</summary>
    [InitializeOnLoad]
    public static class SecondLevelPlaythrough
    {
        static LevelSession session;
        static double began, nextAction;
        static string strategy;
        static bool running;
        static int seenDead, seenBlocking, seenHeroes;
        [Serializable] public sealed class Report
        {
            public string strategy, phase, error;
            public float assaultTime, seconds;
            public int kills, leaks, hp, waves, towers, upgrades, mana, fireballs, frostCasts;
            public int blockingObservations, missingTroopObservations, heroObservations;
            public string[] augments;
        }
        static SecondLevelPlaythrough()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => { if (running) Finish("Assembly reload"); };
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.ExitingPlayMode && running) Finish("Play Mode exited"); };
        }
        public static void Begin(string name = "mixed", int seed = 8123)
        {
            if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode.");
            if (running) Finish("Replaced");
            session = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (session.levelIndex != 2) throw new Exception("Requires second map.");
            strategy = name; session.augmentSeed = seed; session.enableAugments = true; session.RestartLevel();
            seenDead = seenBlocking = seenHeroes = 0;
            began = EditorApplication.timeSinceStartup; nextAction = 0; running = true;
            Choose(); session.ToggleSpeed();
            for (int i = 0; i < 4; i++) Buy();
            EditorApplication.update += Tick;
        }
        static void Choose()
        {
            if (!session.IsChoosingAugment) return;
            AugmentId[] order = { AugmentId.ConstructionGrant, AugmentId.StoredMana, AugmentId.LongRange,
                AugmentId.VeteranTraining, AugmentId.FieldWorkshop, AugmentId.HeavyHunter, AugmentId.ExtendedBurn,
                AugmentId.UltimateRetrofit, AugmentId.VanguardCommand, AugmentId.PolarPulse, AugmentId.TwinMeteor,
                AugmentId.LastStandEngine, AugmentId.Aftershock, AugmentId.PiercingRounds, AugmentId.ManaFurnace };
            int best = -1;
            foreach (var card in order)
            {
                for (int i = 0; i < session.AugmentOffers.Count; i++) if (session.AugmentOffers[i].Id == card) best = i;
                if (best >= 0) break;
            }
            session.TryChooseAugment(best >= 0 ? best : 0);
        }
        static bool Build(int pad, TowerKind kind)
        {
            var slot = session.map.buildSlots[pad];
            if (session.GetTower(slot)) return false;
            if (session.Gold >= session.GetTowerCost(kind)) session.TryBuildTower(slot, kind);
            return true;
        }
        static bool Upgrade(int pad, int level)
        {
            var slot = session.map.buildSlots[pad]; var tower = session.GetTower(slot);
            if (!tower || tower.Level >= level || session.Gold < tower.UpgradeCost) return false;
            return session.TryUpgradeTower(slot);
        }
        static void Buy()
        {
            if (strategy == "none") return;
            // T3 covers the upper bend and T5 the lower bend before both roads join near T7.
            if (Build(2, TowerKind.Cannon)) return;
            if (Build(4, TowerKind.Barracks)) return;
            if (Build(6, TowerKind.Frost)) return;
            if (Build(3, TowerKind.Gun)) return;
            if (Upgrade(2, 2) || Upgrade(4, 2)) return;
            if (Build(5, TowerKind.Cannon)) return;
            if (Upgrade(4, 3) || Upgrade(2, 3) || Upgrade(6, 2)) return;
            if (Build(0, TowerKind.Gun) || Build(7, TowerKind.Gun) || Build(1, TowerKind.Cannon)) return;
            foreach (int pad in new[] { 5, 6, 7, 0, 1, 3 }) if (Upgrade(pad, 3)) return;
        }
        static void Spells()
        {
            if (strategy == "none" || !session.IsCombatRunning) return;
            var enemies = session.ActiveEnemies;
            var best = enemies.Where(e => e && e.IsAlive && session.IsSpellPointValid(e.transform.position))
                .Select(e => new { enemy = e, score = enemies.Count(n => n && n.IsAlive
                    && (n.transform.position - e.transform.position).sqrMagnitude < 10f)
                    + (e.MaxHealth >= 400 ? 2 : 0) + (e.RemainingDistance < 20f ? 1 : 0) })
                .OrderByDescending(e => e.score).FirstOrDefault();
            if (best == null || best.score < 3) return;
            Vector3 point = best.enemy.transform.position;
            if (session.FrostSpellCooldownRemaining <= 0f && session.Mana >= session.FrostSpellCost + session.FireballCost
                && best.score >= 5) session.TryCastFrostNova(point);
            if (session.ActiveSpellCount == 0 && session.CanCastFireball) session.TryCastFireball(point);
        }
        static void Tick()
        {
            if (!running) return;
            try
            {
                if (!EditorApplication.isPlaying || !session) { Finish("Session unavailable"); return; }
                if (session.Phase == SessionPhase.Victory || session.Phase == SessionPhase.Defeat) { Finish(null); return; }
                if (EditorApplication.timeSinceStartup - began > 240) { Finish("Timeout"); return; }
                Choose();
                if (EditorApplication.timeSinceStartup < nextAction) return;
                nextAction = EditorApplication.timeSinceStartup + .15;
                Buy(); Spells();
                foreach (var slot in session.map.buildSlots)
                {
                    var tower = session.GetTower(slot);
                    if (!tower || !tower.Barracks) continue;
                    var b = tower.Barracks;
                    if (b.AliveSoldierCount < b.SoldierCapacity) seenDead++;
                    if (b.AliveHeroCount > 0) seenHeroes++;
                    seenBlocking += b.Troops.Count(t => t && t.IsAlive && t.BlockedEnemyCount > 0);
                }
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        public static Report Snapshot() => session ? new Report
        {
            strategy = strategy, phase = session.Phase.ToString(), assaultTime = session.AssaultTime,
            seconds = (float)(EditorApplication.timeSinceStartup - began), kills = session.Kills, leaks = session.Leaks,
            hp = session.BaseHealth, waves = session.WaveNumber, towers = session.BuiltTowerCount, upgrades = session.UpgradesPurchased,
            mana = session.Mana, fireballs = session.PaidFireballs + session.FreeFireballs, frostCasts = session.FrostCasts,
            blockingObservations = seenBlocking, missingTroopObservations = seenDead, heroObservations = seenHeroes,
            augments = session.SelectedAugments.Select(x => x.Id.ToString()).ToArray()
        } : null;
        static void Finish(string error)
        {
            running = false; EditorApplication.update -= Tick;
            var report = Snapshot();
            if (report == null) return;
            report.error = error;
            Directory.CreateDirectory("Captures/SecondLevelGameplay");
            File.WriteAllText("Captures/SecondLevelGameplay/run-" + strategy + ".json", JsonUtility.ToJson(report, true));
            Debug.Log("Second level playthrough: " + JsonUtility.ToJson(report));
        }
    }
}
