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
    /// <summary>Runtime boundaries for difficulty changes, finite banks and pooled enemies.</summary>
    public static class DifficultyValidation
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

        static object Call(object target, string method, params object[] args) => target.GetType().GetMethods(Private)
            .First(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
        static void Set(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);
        static float Speed(EnemyAgent enemy) => (float)typeof(EnemyAgent).GetField("moveSpeed", Private).GetValue(enemy);
        static int Melee(EnemyAgent enemy) => (int)typeof(EnemyAgent).GetField("meleeDamage", Private).GetValue(enemy);
        static object Difficulty => typeof(GameSettings).GetProperty("Difficulty").GetValue(null);
        static void ChooseDifficulty(string name)
        {
            var setter = typeof(GameSettings).GetMethod("SetDifficulty");
            setter.Invoke(null, new[] { Enum.Parse(setter.GetParameters()[0].ParameterType, name) });
        }
        static void Tick(LevelSession session, float time)
        {
            Set(session, "AssaultTime", time);
            Call(session, "Update");
        }
        static Dictionary<EnemyAgent, EnemyProfile> Definitions(LevelSession session)
            => (Dictionary<EnemyAgent, EnemyProfile>)typeof(LevelSession).GetField("enemyDefinitions", Private).GetValue(session);

        public static Report Run() => RunInLevel(UnityEngine.Object.FindAnyObjectByType<LevelSession>());

        public static Report RunInLevel(LevelSession session)
        {
            if (!EditorApplication.isPlaying || !session) throw new InvalidOperationException("Requires a running battlefield.");
            var rows = new List<Check>();
            var report = new Report { level = session.levelIndex, scene = session.gameObject.scene.path };
            Action<string, bool> check = (name, pass) => rows.Add(new Check { name = name, passed = pass });
            object originalDifficulty = Difficulty;
            bool originalAugments = session.enableAugments;
            int originalSeed = session.augmentSeed;
            int mapGold = session.map.initialGold, mapHealth = session.map.baseHealth;
            string[] originalProfiles = session.enemyProfiles.Select(JsonUtility.ToJson).ToArray();
            try
            {
                session.enableAugments = false;
                ChooseDifficulty("Hard");
                session.RestartLevel();
                check("Hard preserves the original starting gold and base health", session.Gold == mapGold
                    && session.BaseHealth == mapHealth && session.MaxBaseHealth == mapHealth && session.Mana == 0);
                Tick(session, 0f);
                var enemy = session.ActiveEnemies.First();
                var profile = Definitions(session)[enemy];
                check("Hard preserves original enemy health, speed, reward and melee", enemy.MaxHealth == profile.health
                    && Mathf.Approximately(Speed(enemy), profile.speed) && enemy.Reward == profile.reward
                    && Melee(enemy) == profile.meleeDamage);
                enemy.TakeDamage(Mathf.Max(1, enemy.MaxHealth / 4));
                float healthFraction = enemy.CurrentHealth / (float)enemy.MaxHealth;
                enemy.ApplyFrost(.6f, 3f);
                enemy.Freeze(3f);
                Vector3 position = enemy.transform.position;
                int version = enemy.SpawnVersion;
                float assault = session.AssaultTime;
                session.OpenSystemMenu();
                ChooseDifficulty("Easy");
                check("Changing difficulty in settings preserves pause and the running battle", session.IsMenuOpen
                    && !session.IsCombatRunning && Mathf.Approximately(Time.timeScale, 0f)
                    && Mathf.Approximately(session.AssaultTime, assault));
                check("Easy immediately weakens an existing enemy", enemy.MaxHealth == Mathf.Max(1, Mathf.RoundToInt(profile.health * .55f))
                    && Mathf.Approximately(Speed(enemy), profile.speed * .8f)
                    && Melee(enemy) == Mathf.Max(1, Mathf.RoundToInt(profile.meleeDamage * .65f)));
                check("Live difficulty changes preserve health fraction, path and crowd control", Mathf.Abs(enemy.CurrentHealth
                    - Mathf.RoundToInt(enemy.MaxHealth * healthFraction)) <= 1 && enemy.SpawnVersion == version
                    && enemy.transform.position == position && enemy.IsFrozen && enemy.IsFrosted);
                int entitlementGold = session.Gold;
                ChooseDifficulty("Hard");
                ChooseDifficulty("Easy");
                check("Repeated difficulty changes cannot repeatedly mint starting gold", session.Gold == entitlementGold);
                session.CloseSystemMenu();

                session.RestartLevel();
                check("Easy starts with eighty percent more gold and twice the base health", session.Gold == Mathf.RoundToInt(mapGold * 1.8f)
                    && session.BaseHealth == mapHealth * 2 && session.MaxBaseHealth == mapHealth * 2);
                Tick(session, 0f);
                enemy = session.ActiveEnemies.First();
                profile = Definitions(session)[enemy];
                check("Newly spawned Easy enemies use the weaker statistics", enemy.MaxHealth == Mathf.Max(1, Mathf.RoundToInt(profile.health * .55f))
                    && Mathf.Approximately(Speed(enemy), profile.speed * .8f)
                    && Melee(enemy) == Mathf.Max(1, Mathf.RoundToInt(profile.meleeDamage * .65f)));
                int goldBeforeKill = session.Gold, manaBeforeKill = session.Mana;
                int easyReward = enemy.Reward;
                check("Easy enemies give a larger gold reward", easyReward >= profile.reward
                    && easyReward == Mathf.RoundToInt(profile.reward * 1.25f));
                int manaReward = profile.manaReward;
                enemy.TakeDamage(1000000);
                check("Easy kills award the scaled gold and mana exactly once", session.Gold == goldBeforeKill + easyReward
                    && session.Mana == manaBeforeKill + Mathf.CeilToInt(manaReward * 1.5f));
                int afterKillGold = session.Gold, afterKillMana = session.Mana;
                enemy.TakeDamage(1000000);
                check("Repeated death cannot duplicate Easy rewards", session.Gold == afterKillGold && session.Mana == afterKillMana);

                Set(session, "BaseHealth", 1);
                ChooseDifficulty("Hard");
                check("Switching a living one-health Easy core to Hard cannot round it down to zero", session.BaseHealth == 1
                    && session.Phase == SessionPhase.Combat);
                ChooseDifficulty("Easy");
                Set(session, "BaseHealth", 1);
                ChooseDifficulty("Cheat");
                ChooseDifficulty("Hard");
                check("A one-health Easy core also survives switching through Cheat to Hard", session.BaseHealth == 1
                    && session.Phase == SessionPhase.Combat);

                ChooseDifficulty("Hard");
                session.RestartLevel();
                Set(session, "Gold", 0);
                Set(session, "Mana", 0);
                Set(session, "BaseHealth", 1);
                session.OpenSystemMenu();
                ChooseDifficulty("Cheat");
                check("Cheat still respects the settings pause", session.IsMenuOpen && !session.IsCombatRunning
                    && !session.TryBuildTower(session.map.buildSlots[0]) && !session.TryCastFireball(Vector3.zero));
                session.CloseSystemMenu();
                int finiteGold = session.Gold, finiteMana = session.Mana, finiteHealth = session.BaseHealth;
                check("Cheat builds from an empty finite gold bank", session.TryBuildTower(session.map.buildSlots[0]));
                var tower = session.GetTower(session.map.buildSlots[0]);
                check("Cheat permits both tower upgrades", tower && session.TryUpgradeTower(tower.Slot)
                    && session.TryUpgradeTower(tower.Slot) && tower.Level == 3 && !session.TryUpgradeTower(tower.Slot));
                check("Cheat fireball works with an empty finite mana bank", session.CanCastFireball && session.TryCastFireball(Vector3.zero));
                if (session.FrostSpellUnlocked)
                {
                    check("Cheat frost works without mana and keeps its cooldown", session.TryCastFrostNova(Vector3.zero)
                        && !session.TryCastFrostNova(Vector3.zero));
                }
                if (session.ReinforcementsUnlocked)
                    check("Cheat reinforcements work without mana", session.CanCastReinforcements
                        && session.TryCastReinforcements(session.map.routes[0].waypoints[0].position));
                if (session.BattlecryUnlocked)
                    check("Cheat battlecry works without mana", session.CanCastBattlecry && session.TryCastBattlecry());
                if (session.SpellUpgradeUnlocked)
                {
                    bool upgraded = true;
                    foreach (SpellKind kind in Enum.GetValues(typeof(SpellKind)))
                        upgraded &= session.TryUpgradeSpell(kind) && session.TryUpgradeSpell(kind)
                            && session.GetSpellLevel(kind) == 3 && !session.TryUpgradeSpell(kind);
                    check("Cheat upgrades all four spells twice without gold", upgraded);
                }
                check("Cheat purchases and casts never consume the finite bank", session.Gold == finiteGold && session.Mana == finiteMana);
                Tick(session, 0f);
                enemy = session.ActiveEnemies.First();
                int leaks = session.Leaks;
                Call(enemy, "Resolve", false);
                check("Cheat leaks cannot reduce base health or cause defeat", session.BaseHealth == finiteHealth
                    && session.Phase == SessionPhase.Combat && session.Leaks == leaks + 1);
                ChooseDifficulty("Hard");
                check("Leaving Cheat restores the finite bank without a huge resource gift", session.Gold == 0
                    && session.Mana == 0 && session.BaseHealth > 0 && session.BaseHealth <= session.MaxBaseHealth);
                check("Normal purchases and spell costs resume after leaving Cheat", !session.TryBuildTower(session.map.buildSlots[1])
                    && !session.CanCastFireball && !session.TryCastFireball(Vector3.zero));
                session.RestartLevel();
                check("Restart resets difficulty entitlements and restores Hard starting values", session.Gold == mapGold
                    && session.BaseHealth == mapHealth && session.Mana == 0);
                check("Difficulty never mutates the map or serialized enemy profiles", session.map.initialGold == mapGold
                    && session.map.baseHealth == mapHealth && session.enemyProfiles.Select(JsonUtility.ToJson).SequenceEqual(originalProfiles));
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                check("Validation completed without exception", false);
            }
            finally
            {
                typeof(GameSettings).GetMethod("SetDifficulty").Invoke(null, new[] { originalDifficulty });
                session.enableAugments = originalAugments;
                session.augmentSeed = originalSeed;
                if (session.IsMenuOpen) session.CloseSystemMenu();
                session.RestartLevel();
            }
            report.checks = rows.ToArray();
            report.failures = rows.Count(c => !c.passed);
            report.passed = report.failures == 0;
            string directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "DifficultyGameplay");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "boundary-validation-level" + session.levelIndex + ".json"), JsonUtility.ToJson(report, true));
            return report;
        }
    }
}
