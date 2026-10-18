using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerDefense.Gameplay;
using TowerDefense.UI;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    public static class SpellMasteryValidation
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public int failures, level; public string error; public Check[] checks; }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Private)
            .First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);

        public static Report Run()
        {
            var s = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (!EditorApplication.isPlaying || !s) throw new InvalidOperationException("Requires a running level.");
            bool originalAugments = s.enableAugments;
            var difficulty = GameSettings.Difficulty;
            var checks = new List<Check>(); var report = new Report { level = s.levelIndex };
            var fixtures = new List<GameObject>();
            Action<string, bool> check = (name, pass) => checks.Add(new Check { name = name, passed = pass });
            void Fresh()
            {
                s.enableAugments = false; s.RestartLevel(); Set(s, "Gold", 10000); Set(s, "Mana", 1000);
            }
            try
            {
                GameSettings.SetDifficulty(GameDifficulty.Hard); Fresh();
                check("Spell upgrade availability follows the level", s.SpellUpgradeUnlocked == (s.levelIndex >= 2));
                if (s.levelIndex == 1)
                    check("First-level Fireball cannot be upgraded", !s.TryUpgradeSpell(SpellKind.Fireball)
                        && s.GetSpellUpgradeCost(SpellKind.Fireball) == 0 && s.GetSpellLevel(SpellKind.Fireball) == 1);
                else
                {
                    foreach (var kind in new[] { SpellKind.Fireball, SpellKind.FrostNova })
                    {
                        Fresh(); int cost1 = kind == SpellKind.Fireball ? 80 : 90, cost2 = kind == SpellKind.Fireball ? 140 : 150;
                        float radius = kind == SpellKind.Fireball ? s.FireballRadius : s.FrostSpellRadius;
                        int gold = s.Gold, mana = s.Mana;
                        check(kind + " first upgrade charges the exact cost", s.GetSpellUpgradeCost(kind) == cost1
                            && s.TryUpgradeSpell(kind) && s.GetSpellLevel(kind) == 2 && s.Gold == gold - cost1 && s.Mana == mana);
                        check(kind + " second upgrade reaches level three", s.GetSpellUpgradeCost(kind) == cost2
                            && s.TryUpgradeSpell(kind) && s.GetSpellLevel(kind) == 3 && s.Gold == gold - cost1 - cost2);
                        check(kind + " upgrades increase its effective radius", (kind == SpellKind.Fireball ? s.FireballRadius : s.FrostSpellRadius) > radius);
                        gold = s.Gold;
                        check(kind + " stops at level three without charging again", !s.TryUpgradeSpell(kind)
                            && s.GetSpellUpgradeCost(kind) == 0 && s.Gold == gold);
                        Fresh(); Set(s, "Gold", cost1 - 1);
                        check(kind + " rejects an unaffordable upgrade", !s.TryUpgradeSpell(kind) && s.Gold == cost1 - 1);
                    }
                    Fresh(); int bank = s.Gold; s.OpenSystemMenu();
                    check("Settings pause blocks spell purchases", !s.TryUpgradeSpell(SpellKind.Fireball) && s.Gold == bank); s.CloseSystemMenu();
                    s.enableAugments = true; s.RestartLevel(); Set(s, "Gold", 10000);
                    check("Augment choice blocks spell purchases", s.IsChoosingAugment && !s.TryUpgradeSpell(SpellKind.Fireball));
                    Fresh(); Set(s, "Phase", SessionPhase.Victory);
                    check("Victory blocks spell purchases", !s.TryUpgradeSpell(SpellKind.Fireball));
                    Fresh(); Set(s, "Phase", SessionPhase.Defeat);
                    check("Defeat blocks spell purchases", !s.TryUpgradeSpell(SpellKind.FrostNova));
                }
                Fresh();
                if (s.levelIndex == 2)
                    check("Second level retains two unlocked spells", !s.TryUpgradeSpell(SpellKind.Reinforcements)
                        && !s.TryUpgradeSpell(SpellKind.Battlecry) && !s.ReinforcementsUnlocked && !s.BattlecryUnlocked);
                if (s.levelIndex == 3)
                {
                    for (int tier = 1; tier <= 3; tier++)
                    {
                        Fresh(); for (int i = 1; i < tier; i++) s.TryUpgradeSpell(SpellKind.Reinforcements);
                        Vector3 rally = s.map.routes[0].waypoints[1].position;
                        check("Reinforcement level " + tier + " can be cast", s.TryCastReinforcements(rally));
                        var troops = s.GetFriendlyUnits().Where(t => t.IsReinforcement).ToArray();
                        int guardHp = tier == 1 ? 120 : tier == 2 ? 170 : 230;
                        check("Reinforcement level " + tier + " has the correct roster", troops.Length == tier + 2
                            && troops.Count(t => t.IsHero) == (tier == 3 ? 1 : 0) && troops.All(t => t.VisualTier == tier)
                            && troops.Where(t => !t.IsHero).All(t => t.MaxHealth == guardHp));
                        if (tier == 3)
                        {
                            var hero = troops.Single(t => t.IsHero); var guard = troops.First(t => !t.IsHero);
                            var heroMesh = hero.GetComponentInChildren<SkinnedMeshRenderer>(); var guardMesh = guard.GetComponentInChildren<SkinnedMeshRenderer>();
                            check("Champion uses a distinct character mesh and stronger stats", heroMesh && guardMesh
                                && heroMesh.sharedMesh != guardMesh.sharedMesh && hero.MaxHealth == 420 && hero.Damage == 44 && guard.Damage == 28);
                        }
                        check("Reinforcements expire without kill rewards", s.ReinforcementDuration == 18f + (tier - 1) * 4f);
                        Call(s, "UpdateThirdLevelCombat", s.ReinforcementDuration + .1f);
                        check("Expired reinforcements leave no active friendly units", s.ActiveReinforcementCount == 0 && s.AugmentManaGranted == 0);
                    }
                    Fresh(); float basicDamage = s.GetBattleDamageMultiplier(); s.TryCastBattlecry();
                    float tier1 = s.GetBattleDamageMultiplier(); Fresh(); s.TryUpgradeSpell(SpellKind.Battlecry); s.TryUpgradeSpell(SpellKind.Battlecry); s.TryCastBattlecry();
                    check("Max-level battlecry improves real combat multipliers", basicDamage == 1f && tier1 == 1.25f
                        && s.GetBattleDamageMultiplier() == 1.5f && s.GetBattleAttackSpeedMultiplier() == 1.55f && s.GetBattleRangeBonus() == 1.6f);
                }
                Fresh(); var fx = CombatVisuals.For(s);
                var go = new GameObject("Fireball mastery fixture"); go.transform.SetParent(s.transform); fixtures.Add(go);
                var spell = go.AddComponent<FireballArea>(); int ticks1 = 0, ticks3 = 0;
                spell.Play(Vector3.zero, 3f, 3f, .5f, 10, (p, r, d) => ticks1++, area => { }, 1);
                float scale1 = go.transform.Find("Falling Fireball").localScale.x;
                spell.Advance(3.3f);
                spell.Play(Vector3.zero, 3f, 3f, .5f, 10, (p, r, d) => ticks3++, area => { }, 3);
                check("Max-level meteor and its trail are visibly larger", spell.VisualLevel == 3
                    && go.transform.Find("Falling Fireball").localScale.x > scale1 * 2f);
                check("Burn decals sit above the road surface", spell.VisualGroundHeight >= s.map.transform.position.y + .49f);
                spell.Advance(3.3f);
                check("Visual tiers preserve the same burn tick schedule", ticks1 == 6 && ticks3 == ticks1);
                fx.ClearBursts(); fx.FrostNova(Vector3.zero, 3f, 1);
                int basicLines = fx.GetComponentsInChildren<LineRenderer>().Count(l => l.enabled);
                fx.ClearBursts(); fx.FrostNova(Vector3.zero, 3f, 3);
                check("Max-level Frost adds a distinct rune circle", fx.GetComponentsInChildren<LineRenderer>().Count(l => l.enabled) > basicLines);
                fx.ClearBursts(); fx.ReinforcementArrival(Vector3.zero, 1);
                basicLines = fx.GetComponentsInChildren<LineRenderer>().Count(l => l.enabled);
                fx.ClearBursts(); fx.ReinforcementArrival(Vector3.zero, 3);
                check("Max-level summoning adds a complete gold ritual", fx.GetComponentsInChildren<LineRenderer>().Count(l => l.enabled) > basicLines);
                fx.ClearBursts(); fx.BattlecryAura(s.transform, 1, 2f);
                basicLines = fx.GetComponentsInChildren<LineRenderer>().Count(l => l.enabled);
                fx.ClearBursts(); fx.BattlecryAura(s.transform, 3, 2f);
                check("Max-level battlecry adds runes and several light columns", fx.GetComponentsInChildren<LineRenderer>().Count(l => l.enabled) > basicLines);
                Time.timeScale = 0f; int bursts = fx.ActiveBurstCount; Call(fx, "LateUpdate");
                check("Pause holds spell visual lifetimes", fx.ActiveBurstCount == bursts);
                s.RestartLevel();
                check("Restart resets spell levels and transient visuals", Enum.GetValues(typeof(SpellKind)).Cast<SpellKind>().All(k => s.GetSpellLevel(k) == 1)
                    && s.ActiveReinforcementCount == 0 && fx.ActiveBurstCount == 0 && s.BattlecryRemaining == 0f);
            }
            catch (Exception ex) { report.error = ex.ToString(); }
            finally
            {
                foreach (var fixture in fixtures) if (fixture) UnityEngine.Object.DestroyImmediate(fixture);
                s.enableAugments = originalAugments; GameSettings.SetDifficulty(difficulty); s.RestartLevel();
            }
            report.checks = checks.ToArray(); report.failures = checks.Count(c => !c.passed);
            report.passed = report.failures == 0 && string.IsNullOrEmpty(report.error);
            Directory.CreateDirectory("Captures/Mastery");
            File.WriteAllText("Captures/Mastery/spell-mastery-level" + s.levelIndex + ".json", JsonUtility.ToJson(report, true));
            return report;
        }
    }
}
