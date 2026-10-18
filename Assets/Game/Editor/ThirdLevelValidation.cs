using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerDefense.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    public static class ThirdLevelValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public int failures; public string error; public Check[] checks; }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Private)
            .First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        static void Tick(LevelSession s, float time) { Set(s, "AssaultTime", time); Call(s, "Update"); }
        static void Fresh(LevelSession s)
        {
            s.enableAugments = false; s.RestartLevel(); Set(s, "Gold", 10000); Set(s, "Mana", 500);
        }
        static TowerAgent Build(LevelSession s, int pad, TowerKind kind)
        {
            if (!s.TryBuildTower(s.map.buildSlots[pad], kind)) throw new Exception("Cannot build fixture " + kind);
            return s.GetTower(s.map.buildSlots[pad]);
        }
        static EnemyAgent Enemy(LevelSession s, EnemyKind kind)
        {
            var definitions = (Dictionary<EnemyAgent, EnemyProfile>)typeof(LevelSession).GetField("enemyDefinitions", Private).GetValue(s);
            return s.ActiveEnemies.First(e => e && e.IsAlive && definitions[e].kind == kind);
        }
        static void Hide(LevelSession s) { foreach (var e in s.ActiveEnemies) e.transform.position = new Vector3(1000, 0, 1000); }

        public static Report Run()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode.");
            var s = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            var rows = new List<Check>(); var report = new Report(); int originalSeed = s.augmentSeed;
            Action<string, bool> check = (name, pass) => rows.Add(new Check { name = name, passed = pass });
            try
            {
                check("Third battle uses original snow scene and ten pads", s.levelIndex == 3 && s.gameObject.scene.path.Contains("Level03_CoreFortress")
                    && s.map.buildSlots.Length == 10 && s.map.routes.Length == 2);
                check("Five towers, four spells, three augment choices", s.AvailableTowerCount == 5 && s.FrostSpellUnlocked
                    && s.ReinforcementsUnlocked && s.BattlecryUnlocked && s.ActiveAugmentChoiceCount == 3);
                check("Complete configuration", s.GetConfigurationErrors().Count == 0);
                var bossWaves = s.waves.Select((w, i) => new { w, i }).Where(x => x.w.batches.Any(b =>
                    s.enemyProfiles.Any(p => p.kind == b.enemyKind && p.boss))).ToArray();
                check("Two separated boss encounters with distinct enemies", bossWaves.Length >= 2 && bossWaves.Last().i - bossWaves.First().i >= 5
                    && bossWaves.SelectMany(w => w.w.batches).Where(b => s.enemyProfiles.Any(p => p.kind == b.enemyKind && p.boss))
                        .Select(b => b.enemyKind).Distinct().Count() >= 2);

                bool rerolls = true, invariant = true, restores = true;
                for (int seed = 0; seed < 20; seed++)
                {
                    s.enableAugments = true; s.augmentSeed = 58000 + seed; s.RestartLevel();
                    for (int choice = 0; choice < 3; choice++)
                    {
                        if (choice > 0) Tick(s, s.waves[s.augmentChoiceWaveIndices[choice]].startTime);
                        var seen = new HashSet<AugmentId>(s.AugmentOffers.Select(c => c.Id));
                        for (int slot = 0; slot < 3; slot++)
                        {
                            var previous = s.AugmentOffers[slot]; int count = s.SelectedAugments.Count;
                            rerolls &= s.CanRefreshAugment(slot) && s.TryRefreshAugment(slot)
                                && !s.CanRefreshAugment(slot) && !s.TryRefreshAugment(slot);
                            invariant &= s.AugmentOffers[slot].Tier == previous.Tier && seen.Add(s.AugmentOffers[slot].Id)
                                && s.AugmentOffers.Select(c => c.Id).Distinct().Count() == 3 && s.AugmentOffers.Any(c => c.IsGeneric)
                                && s.SelectedAugments.Count == count && Time.timeScale == 0f;
                        }
                        restores &= s.TryChooseAugment(0) && !s.TryRefreshAugment(0) && Time.timeScale == s.GameSpeed;
                    }
                    rerolls &= s.AugmentRefreshesUsed == 9 && s.SelectedAugments.Count == 3;
                }
                check("Twenty seeds permit each slot to refresh exactly once at all three choices", rerolls);
                check("Rerolls keep quality, uniqueness, a generic option, and pause", invariant);
                check("Selecting after refresh restores combat and disables refresh", restores);

                Fresh(s);
                for (int i = 0; i < s.map.buildSlots.Length; i++)
                {
                    var fullTower = Build(s, i, TowerKind.Support);
                    s.TryUpgradeTower(fullTower.Slot); s.TryUpgradeTower(fullTower.Slot);
                }
                Set(s, "BaseHealth", 1); s.enableAugments = true;
                Call(s, "BeginAugmentChoice", AugmentTier.Prismatic);
                bool narrowPool = s.IsChoosingAugment;
                for (int i = 0; i < 3; i++)
                    narrowPool &= s.TryRefreshAugment(i) && !s.TryRefreshAugment(i)
                        && s.AugmentOffers.Select(c => c.Id).Distinct().Count() == 3;
                check("Full specialized board still permits all three promised refreshes", narrowPool);
                s.levelIndex = 2;
                check("Earlier levels unlock card refresh while keeping spell upgrades locked", s.AugmentRefreshUnlocked
                    && !s.SpellUpgradeUnlocked && !s.CanUpgradeSpell(SpellKind.Fireball));
                s.levelIndex = 3;

                Fresh(s);
                bool upgrades = true, increases = true;
                foreach (SpellKind kind in Enum.GetValues(typeof(SpellKind)))
                {
                    int gold = s.Gold, firstCost = s.GetSpellUpgradeCost(kind);
                    string before = s.GetSpellSummary(kind);
                    upgrades &= s.GetSpellLevel(kind) == 1 && s.TryUpgradeSpell(kind) && s.Gold == gold - firstCost
                        && s.GetSpellLevel(kind) == 2 && s.TryUpgradeSpell(kind) && s.GetSpellLevel(kind) == 3
                        && !s.TryUpgradeSpell(kind) && s.GetSpellUpgradeCost(kind) == 0;
                    increases &= before != s.GetSpellSummary(kind);
                }
                check("All four spells accept exactly two paid upgrades", upgrades && s.SpellUpgradesPurchased == 8);
                check("Every spell upgrade improves its effect", increases && s.FireballRadius > 3.2f && s.FrostSpellDuration > 3.5f
                    && s.ReinforcementCount == 5 && s.BattlecryDuration == 12f);
                Fresh(s); Set(s, "Gold", 0);
                check("Insufficient gold cannot upgrade or alter spell", !s.TryUpgradeSpell(SpellKind.Fireball) && s.GetSpellLevel(SpellKind.Fireball) == 1 && s.Gold == 0);
                s.enableAugments = true; s.RestartLevel(); Set(s, "Gold", 10000);
                check("Augment modal blocks spell upgrades and new spells", !s.TryUpgradeSpell(SpellKind.Fireball)
                    && !s.TryCastReinforcements(Vector3.zero) && !s.TryCastBattlecry());

                Fresh(s);
                int mana = s.Mana;
                check("Reinforcements summon three troops at level one", s.TryCastReinforcements(Vector3.zero)
                    && s.ActiveReinforcementCount == 3 && s.Mana == mana - s.ReinforcementCost
                    && s.GetFriendlyUnits().All(t => t.IsReinforcement && t.MaxHealth == 120));
                check("Summon cooldown prevents an extra squad", !s.TryCastReinforcements(Vector3.zero));
                var recruit = s.GetFriendlyUnits().First(); recruit.TakeDamage(99999);
                Call(s, "UpdateThirdLevelCombat", 9f);
                check("Fallen spell troops do not respawn", s.ActiveReinforcementCount == 2);
                Call(s, "UpdateThirdLevelCombat", 10f);
                check("Temporary squad expires without leaving friendly units", s.ActiveReinforcementCount == 0 && !s.GetFriendlyUnits().Any());
                Call(s, "UpdateThirdLevelCombat", 2f);
                s.TryUpgradeSpell(SpellKind.Reinforcements); s.TryUpgradeSpell(SpellKind.Reinforcements);
                check("Level three summon has five stronger troops", s.TryCastReinforcements(Vector3.zero)
                    && s.ActiveReinforcementCount == 5 && s.GetFriendlyUnits().All(t => t.MaxHealth == 230 && t.Damage == 28));

                Fresh(s); var tower = Build(s, 0, TowerKind.Gun); float range = tower.EffectiveRange, interval = tower.EffectiveFireInterval;
                Tick(s, 0); var normal = s.ActiveEnemies[0];
                int plain = (int)Call(s, "ComputeTowerHitDamage", normal, 20, TowerKind.Gun, false, tower);
                mana = s.Mana; bool cast = s.TryCastBattlecry();
                check("Battlecry spends mana and buffs tower damage, speed and range", cast && s.Mana == mana - s.BattlecryCost
                    && (int)Call(s, "ComputeTowerHitDamage", normal, 20, TowerKind.Gun, false, tower) > plain
                    && tower.EffectiveRange > range && tower.EffectiveFireInterval < interval);
                s.TryCastReinforcements(Vector3.zero); recruit = s.GetFriendlyUnits().First();
                check("Battlecry also buffs friendly unit attack interval and reach", recruit.EffectiveAttackInterval < .8f && recruit.EffectiveAttackReach > recruit.attackReach);
                check("Battlecry cannot stack during cooldown", !s.TryCastBattlecry());
                float remaining = s.BattlecryRemaining; s.TogglePause(); Call(s, "Update");
                check("Pause freezes all new combat timers", s.BattlecryRemaining == remaining && !s.TryCastBattlecry() && !s.TryCastReinforcements(Vector3.zero));
                s.TogglePause(); Call(s, "UpdateThirdLevelCombat", 9f);
                check("Expired Battlecry restores base tower stats", s.GetBattleDamageMultiplier() == 1f
                    && Mathf.Approximately(tower.EffectiveRange, range) && Mathf.Approximately(tower.EffectiveFireInterval, interval));

                Fresh(s); tower = Build(s, 0, TowerKind.Support); var support = tower.GetComponent<SupportTowerAgent>();
                check("Support tower has level-one vulnerability and healing aura", support != null && Mathf.Approximately(support.AuraRadius, 4.5f)
                    && support.HealingPerSecond == 6);
                Tick(s, 5); Hide(s); normal = s.ActiveEnemies[0]; normal.transform.position = tower.transform.position;
                support.Advance(.5f);
                check("Support aura applies vulnerability to nearby enemies", Mathf.Approximately(normal.VulnerabilityBonus, .15f));
                int hp = normal.CurrentHealth; normal.TakeDamage(20);
                check("Vulnerability amplifies damage exactly once", normal.CurrentHealth == hp - 23);
                s.TryUpgradeTower(tower.Slot); s.TryUpgradeTower(tower.Slot);
                check("Support tower reaches stronger level-three aura", tower.Level == 3 && Mathf.Approximately(support.AuraRadius, 6f)
                    && support.HealingPerSecond == 16);
                s.TryCastReinforcements(tower.transform.position); recruit = s.GetFriendlyUnits().First();
                recruit.transform.position = tower.transform.position;
                recruit.TakeDamage(40); hp = recruit.CurrentHealth; support.Advance(1f);
                check("Support aura heals nearby friendly units each second", recruit.CurrentHealth == hp + 16);
                recruit.Heal(99999); check("Healing is capped at max health", recruit.CurrentHealth == recruit.MaxHealth);

                Fresh(s); Tick(s, bossWaves.First().w.startTime); Hide(s);
                var boss = Enemy(s, EnemyKind.BossWarden);
                check("First boss spawns with boss metadata", boss.IsBoss && s.BossesSpawned >= 1 && boss.MaxHealth >= 2000);
                boss.Freeze(4f); float frozen = (float)typeof(EnemyAgent).GetField("freezeRemaining", Private).GetValue(boss);
                check("Boss has reduced control duration", frozen > 0f && frozen < 4f);
                boss.TakeDamage(Mathf.CeilToInt(boss.MaxHealth * .75f));
                check("Damaged boss enters its rage phase", boss.IsAlive && boss.IsEnraged);
                boss.TakeDamage(999999);
                check("Boss death uses ordinary rewards and tracks boss kill", s.BossesKilled == 1);
                Tick(s, bossWaves.Last().w.startTime); boss = Enemy(s, EnemyKind.BossTitan);
                check("Final boss is a separate larger-health encounter", boss.IsBoss && boss.MaxHealth >= 4000 && s.BossesSpawned >= 2);
                boss.TakeDamage(999999);
                check("Both boss fights can be defeated", s.BossesKilled == 2);
                s.RestartLevel();
                check("Restart resets spells, refreshes, buffs, summons and boss counters", Enum.GetValues(typeof(SpellKind)).Cast<SpellKind>().All(k => s.GetSpellLevel(k) == 1)
                    && s.ActiveReinforcementCount == 0 && s.BattlecryRemaining == 0 && s.AugmentRefreshesUsed == 0 && s.BossesKilled == 0);
            }
            catch (Exception e) { report.error = e.ToString(); }
            finally
            {
                s.augmentSeed = originalSeed; s.enableAugments = true; s.RestartLevel();
                report.checks = rows.ToArray(); report.failures = rows.Count(r => !r.passed) + (string.IsNullOrEmpty(report.error) ? 0 : 1);
                report.passed = report.failures == 0;
                Directory.CreateDirectory("Captures/ThirdLevelGameplay");
                File.WriteAllText("Captures/ThirdLevelGameplay/boundary-validation.json", JsonUtility.ToJson(report, true));
            }
            return report;
        }
    }
}
