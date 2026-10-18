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
    public static class TowerMasteryValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public string error; public int failures; public Check[] checks; }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Private)
            .First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);

        public static Report Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Requires Play Mode in level 3.");
            LevelSession session = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (!session || session.levelIndex != 3) throw new InvalidOperationException("Requires the third level.");
            var report = new Report(); var checks = new List<Check>();
            Action<string, bool> check = (name, pass) => checks.Add(new Check { name = name, passed = pass });
            bool augments = session.enableAugments;
            GameDifficulty difficulty = GameSettings.Difficulty;
            var actors = new List<EnemyAgent>();
            var fixtures = new List<GameObject>();
            var enemies = (List<EnemyAgent>)typeof(LevelSession).GetField("enemies", Private).GetValue(session);
            var profiles = (Dictionary<EnemyAgent, EnemyProfile>)typeof(LevelSession).GetField("enemyDefinitions", Private).GetValue(session);
            try
            {
                GameSettings.SetDifficulty(GameDifficulty.Hard);
                session.enableAugments = false; session.RestartLevel();
                Set(session, "Gold", 10000); Set(session, "Mana", 1000);

                EnemyAgent MakeEnemy(string name, Vector3 position)
                {
                    var go = new GameObject(name); go.transform.SetParent(session.transform, false);
                    var enemy = go.AddComponent<EnemyAgent>();
                    enemy.Initialize(session.map.routes[0], 10000, 0f, 0, 0, null);
                    enemy.transform.position = position; enemy.DamageReduction = 0f;
                    enemies.Add(enemy); profiles[enemy] = new EnemyProfile { kind = EnemyKind.Normal };
                    actors.Add(enemy); fixtures.Add(go); return enemy;
                }
                void ResetEnemy(EnemyAgent enemy, Vector3 position)
                {
                    enemy.Initialize(session.map.routes[0], 10000, 0f, 0, 0, null);
                    enemy.transform.position = position; enemy.DamageReduction = 0f;
                }
                TowerAgent MakeTower(TowerKind kind)
                {
                    var go = new GameObject("Mastery " + kind); go.transform.SetParent(session.transform, false);
                    var tower = go.AddComponent<TowerAgent>(); tower.kind = kind; tower.Initialize(session, null);
                    fixtures.Add(go); return tower;
                }

                var direct = MakeEnemy("Direct target", Vector3.zero);
                var neighbor = MakeEnemy("Near target", Vector3.right);
                var distant = MakeEnemy("Distant target", Vector3.right * 5f);
                var gun = MakeTower(TowerKind.Gun);
                ShotSpec basic = gun.GetShotSpec();
                Call(session, "ResolveShot", direct, basic);
                check("Level-one rapidfire remains a single-target shot", basic.level == 1 && basic.splashRadius == 0f
                    && direct.CurrentHealth == 10000 - basic.damage && neighbor.CurrentHealth == 10000);
                gun.ApplyUpgrade(); gun.ApplyUpgrade(); ShotSpec rapid = gun.GetShotSpec();
                ResetEnemy(direct, Vector3.zero); ResetEnemy(neighbor, Vector3.right);
                Call(session, "ResolveShot", direct, rapid);
                check("Level-three rapidfire retains full direct damage and adds 45% nearby splash", rapid.level == 3
                    && Mathf.Approximately(rapid.splashRadius, 1.25f) && direct.CurrentHealth == 10000 - rapid.damage
                    && neighbor.CurrentHealth == 10000 - Mathf.RoundToInt(rapid.damage * .45f) && distant.CurrentHealth == 10000);
                int nearbyHealth = neighbor.CurrentHealth;
                rapid.isSecondary = true; Call(session, "ResolveShot", direct, rapid);
                check("Secondary rapidfire splash is applied once without recursive shots", neighbor.CurrentHealth
                    == nearbyHealth - Mathf.RoundToInt(rapid.damage * .45f) && session.ShotsFired == 0);

                var frost = MakeTower(TowerKind.Frost);
                check("Frost levels one and two retain slow without area freeze", frost.GetShotSpec().freezeDuration == 0f
                    && frost.GetShotSpec().splashRadius == 0f);
                frost.ApplyUpgrade(); check("Frost level two still targets one enemy", frost.GetShotSpec().freezeDuration == 0f);
                frost.ApplyUpgrade(); ShotSpec ice = frost.GetShotSpec();
                ResetEnemy(direct, Vector3.zero); ResetEnemy(neighbor, Vector3.right); ResetEnemy(distant, Vector3.right * 5f);
                neighbor.ConfigureBoss(true, .3f, .4f);
                Call(session, "ResolveShot", direct, ice);
                float ReadFreeze(EnemyAgent enemy) => (float)typeof(EnemyAgent).GetField("freezeRemaining", Private).GetValue(enemy);
                check("Level-three frost freezes and slows a small group without touching distant enemies", ice.level == 3
                    && direct.IsFrozen && direct.IsFrosted && neighbor.IsFrozen && neighbor.IsFrosted && !distant.IsFrosted);
                check("Boss control resistance still shortens tower freezes", Mathf.Approximately(ReadFreeze(direct), 1f)
                    && Mathf.Approximately(ReadFreeze(neighbor), .3f));
                typeof(EnemyAgent).GetField("freezeRemaining", Private).SetValue(direct, .25f);
                Set(session, "AssaultTime", .5f); ice.source = MakeTower(TowerKind.Frost);
                Call(session, "ResolveShot", direct, ice);
                check("Several max-tier frost towers cannot refresh an enemy's shared freeze lock", Mathf.Approximately(ReadFreeze(direct), .25f));
                Set(session, "AssaultTime", 3.1f); Call(session, "ResolveShot", direct, ice);
                check("The target can be frozen again after its three-second control window", Mathf.Approximately(ReadFreeze(direct), 1f));
                ResetEnemy(direct, Vector3.zero); Call(session, "ResolveShot", direct, ice);
                check("A reused enemy cannot inherit a previous spawn's immunity", direct.IsFrozen);

                var blast = MakeTower(TowerKind.Cannon); blast.ApplyUpgrade(); blast.ApplyUpgrade(); ShotSpec explosion = blast.GetShotSpec();
                ResetEnemy(direct, Vector3.zero); ResetEnemy(neighbor, Vector3.right); direct.ApplyFrost(.6f, 3f); neighbor.ApplyFrost(.6f, 3f);
                int shatters = session.ShattersTriggered;
                Call(session, "ResolveShot", direct, explosion);
                check("Level-three Blast retains full area damage and gains stronger shatter", explosion.level == 3
                    && explosion.splashRadius >= 3.2f && explosion.shatterDamage == 64 && explosion.shatterRadius == 4f
                    && direct.CurrentHealth == 10000 - explosion.damage - explosion.shatterDamage
                    && neighbor.CurrentHealth == direct.CurrentHealth);
                check("A group shatter consumes only the direct frost mark and never chains itself", session.ShattersTriggered == shatters + 1
                    && !direct.IsFrosted && neighbor.IsFrosted);

                var barracksSlot = session.map.buildSlots.First(p => session.TryGetRallyPoint(p.transform.position, out Vector3 point)
                    && (point - p.transform.position).sqrMagnitude < 49f);
                if (!session.TryBuildTower(barracksSlot, TowerKind.Barracks)) throw new InvalidOperationException("Cannot build barracks fixture.");
                var barracksTower = session.GetTower(barracksSlot);
                session.TryUpgradeTower(barracksSlot); session.TryUpgradeTower(barracksSlot);
                var barracks = barracksTower.Barracks;
                var hero = barracks.Troops.Single(a => a.IsHero);
                check("Max-tier barracks strengthen their three soldiers and field a distinct champion", barracks.AliveSoldierCount == 3
                    && barracks.AliveHeroCount == 1 && barracks.SoldierHealth == 180 && barracks.SoldierDamage == 24
                    && hero.MaxHealth == 520 && hero.Damage == 52 && hero.VisualTier == 3);
                ResetEnemy(direct, Vector3.zero); ResetEnemy(neighbor, Vector3.right); ResetEnemy(distant, Vector3.right * 5f);
                Call(hero, "PerformAttack", direct); Call(hero, "PerformAttack", direct);
                check("The champion's first two strikes remain normal attacks", neighbor.CurrentHealth == 10000 && hero.HeroCleaveCount == 0);
                Call(hero, "PerformAttack", direct);
                check("Every third champion strike cleaves nearby foes at 60% without hitting distant foes", hero.HeroCleaveCount == 1
                    && neighbor.CurrentHealth == 10000 - Mathf.RoundToInt(hero.Damage * .6f) && distant.CurrentHealth == 10000);

                var supportSlot = session.map.buildSlots.First(p => !session.HasTower(p));
                if (!session.TryBuildTower(supportSlot, TowerKind.Support)) throw new InvalidOperationException("Cannot build sanctuary fixture.");
                var supportTower = session.GetTower(supportSlot); session.TryUpgradeTower(supportSlot); session.TryUpgradeTower(supportSlot);
                var support = supportTower.Support;
                Vector3 center = support.transform.position;
                ResetEnemy(direct, center); hero.transform.position = center; hero.TakeDamage(60);
                int beforeHeal = hero.CurrentHealth; support.Advance(1f);
                check("Max-tier sanctuary applies 38% vulnerability for three seconds", Mathf.Approximately(support.VulnerabilityPercent, .38f)
                    && Mathf.Approximately(support.VulnerabilityDuration, 3f) && Mathf.Approximately(direct.VulnerabilityBonus, .38f));
                check("Max-tier sanctuary heals living troops by 22 per second", support.HealingPerSecond == 22 && hero.CurrentHealth == beforeHeal + 22);
                var effects = CombatVisuals.For(session);
                effects.ClearBursts(); effects.SanctuaryPulse(center, 1);
                int basicLines = effects.GetComponentsInChildren<LineRenderer>().Count(line => line.enabled);
                effects.ClearBursts(); effects.SanctuaryPulse(center, 3);
                check("Max-tier sanctuary emits extra runes and jade light columns", effects.GetComponentsInChildren<LineRenderer>().Count(line => line.enabled) > basicLines);

                var guardGo = new GameObject("Veteran visual fixture"); guardGo.transform.SetParent(session.transform, false); fixtures.Add(guardGo);
                var guard = guardGo.AddComponent<SoldierAgent>(); guard.InitializeReinforcement(session, center, 230, 28, .8f, 3f, null);
                guard.SetVisualTier(2); int equipmentCount = guard.GetComponentsInChildren<Renderer>(true).Length;
                guard.SetVisualTier(3); guard.SetVisualTier(2);
                check("Elite guards have distinct, reusable equipment without creating repeated copies", guard.VisualTier == 2
                    && equipmentCount == 3 && guard.GetComponentsInChildren<Renderer>(true).Length == equipmentCount);

                // Enemy death resolves synchronously. Victory may shut down the barracks
                // during the champion's third strike, before the cleave can execute.
                typeof(SoldierAgent).GetField("heroAttackCount", Private).SetValue(hero, 2);
                direct.Initialize(session.map.routes[0], 1, 0f, 0, 0,
                    (dead, killed) => { Set(session, "Phase", SessionPhase.Victory); Call(session, "ClearCombat"); });
                direct.transform.position = center;
                Call(hero, "PerformAttack", direct);
                check("A champion's final killing strike ends combat without a post-victory cleave", session.Phase == SessionPhase.Victory
                    && !hero.gameObject.activeSelf && hero.HeroCleaveCount == 1);
            }
            catch (Exception exception) { report.error = exception.ToString(); }
            finally
            {
                foreach (EnemyAgent enemy in actors) { enemies.Remove(enemy); profiles.Remove(enemy); }
                foreach (GameObject fixture in fixtures) if (fixture) UnityEngine.Object.DestroyImmediate(fixture);
                session.enableAugments = augments; GameSettings.SetDifficulty(difficulty); session.RestartLevel();
            }
            report.checks = checks.ToArray(); report.failures = checks.Count(c => !c.passed);
            report.passed = report.failures == 0 && string.IsNullOrEmpty(report.error);
            Directory.CreateDirectory("Captures/Mastery");
            File.WriteAllText("Captures/Mastery/tower-mastery-validation.json", JsonUtility.ToJson(report, true));
            Debug.Log("Tower mastery validation: " + (checks.Count - report.failures) + "/" + checks.Count
                + " passed. " + report.error);
            return report;
        }
    }
}
