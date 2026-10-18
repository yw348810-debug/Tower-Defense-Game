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
    /// <summary>Targeted runtime checks for the timeline, pooled targets and damage resolution.</summary>
    public static class CombatBoundaryValidation
    {
        static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static void Tick(LevelSession s, float time)
        {
            typeof(LevelSession).GetProperty("AssaultTime").SetValue(s, time);
            typeof(LevelSession).GetMethod("Update", Private).Invoke(s, null);
        }
        public static void Run()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Requires Play Mode.");
            var s = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            var checks = new List<RicherBattleValidation.CheckResult>();
            Action<string, bool, string> check = (name, passed, detail) => checks.Add(new RicherBattleValidation.CheckResult
                { name = name, passed = passed, detail = detail });
            var report = new RicherBattleValidation.CriticalReport();
            try
            {
                s.RestartLevel(); Tick(s, 0);
                check("Automatic opening", s.Phase == SessionPhase.Combat && s.WaveNumber == 1 && s.ActiveEnemies.Count > 0, "No start-wave action.");
                var first = s.ActiveEnemies[0];
                Tick(s, 31);
                bool atB = s.ActiveEnemies.Any(e => Vector3.Distance(e.transform.position, s.map.routes.First(r => r.routeId == "B").waypoints[0].position) < s.laneOffset + .1f);
                check("Waves overlap and use both entries", s.WaveNumber == 2 && first.IsAlive && atB, "Wave 1 enemy remains alive when route B begins.");
                Tick(s, 1000);
                int planned = s.waves.Sum(w => w.batches.Sum(b => b.count));
                check("Final spawns cannot cause early victory", s.AssaultProgress == 1 && s.Phase == SessionPhase.Combat && s.ActiveEnemies.Count == planned,
                    s.ActiveEnemies.Count + " enemies are still alive after all " + planned + " were scheduled.");

                s.RestartLevel(); Tick(s, 0);
                first = s.ActiveEnemies[0]; int oldVersion = first.SpawnVersion;
                s.FireProjectile(first.AimPosition + Vector3.up * 15, first, 100);
                var bullet = s.GetComponentsInChildren<ProjectileAgent>().First();
                int gold = s.Gold; int reward = first.Reward;
                first.TakeDamage(100000); int afterKill = s.Gold;
                first.TakeDamage(100000);
                check("Death awards once", afterKill == gold + reward && s.Gold == afterKill && s.Kills == 1, "Repeated damage does not repeat the reward.");
                Tick(s, 1.81f);
                var reused = s.ActiveEnemies.First(e => ReferenceEquals(first, e)); int hp = reused.CurrentHealth;
                typeof(ProjectileAgent).GetMethod("Update", Private).Invoke(bullet, null);
                check("Old projectile cannot hit a reused target", ReferenceEquals(first, reused) && reused.SpawnVersion != oldVersion &&
                    reused.CurrentHealth == hp && s.ActiveProjectileCount == 0, "Same pooled component, different generation.");
                reused.ApplyFrost(.6f, 2.5f); reused.ApplyFrost(.6f, 2.5f);
                float multiplier = (float)typeof(EnemyAgent).GetField("frostMultiplier", Private).GetValue(reused);
                check("Frost refreshes without multiplying", reused.IsFrosted && Mathf.Approximately(multiplier, .6f), "Two frost hits still produce 0.6 movement speed.");

                s.RestartLevel(); Tick(s, 3.7f);
                var a = s.ActiveEnemies[0]; var b = s.ActiveEnemies[1]; var outside = s.ActiveEnemies[2];
                Vector3 center = a.transform.position;
                b.transform.position = center + Vector3.right; outside.transform.position = center + Vector3.right * 10;
                a.ApplyFrost(.6f, 2.5f); b.ApplyFrost(.6f, 2.5f);
                int beforeA = a.CurrentHealth, beforeB = b.CurrentHealth, beforeOutside = outside.CurrentHealth;
                var shot = new ShotSpec { kind = TowerKind.Cannon, damage = 18, splashRadius = 2.4f, shatterDamage = 20,
                    shatterRadius = 3, color = Color.red };
                typeof(LevelSession).GetMethod("ResolveShot", Private).Invoke(s, new object[] { a, shot });
                check("Cannon and shatter damage each apply once", a.CurrentHealth == beforeA - 38 && b.CurrentHealth == beforeB - 38 &&
                    outside.CurrentHealth == beforeOutside, "Direct target and nearby enemy take 18 + 20; distant enemy takes zero.");
                check("Shatter does not recurse through area targets", s.ShattersTriggered == 1 && !a.IsFrosted && b.IsFrosted,
                    "Only the directly hit enemy consumes frost.");

                s.RestartLevel(); Tick(s, 81);
                var heavy = s.ActiveEnemies.First(e => e.MaxHealth == s.enemyProfiles.First(p => p.kind == EnemyKind.Heavy).health);
                int beforeLeak = s.BaseHealth;
                typeof(EnemyAgent).GetMethod("Resolve", Private).Invoke(heavy, new object[] { false });
                typeof(EnemyAgent).GetMethod("Resolve", Private).Invoke(heavy, new object[] { false });
                check("Brute leak applies three damage once", s.BaseHealth == beforeLeak - 3 && s.Leaks == 1, "A repeated resolve cannot deduct more health.");
                foreach (var e in s.ActiveEnemies.ToArray())
                {
                    if (s.Phase == SessionPhase.Defeat) break;
                    typeof(EnemyAgent).GetMethod("Resolve", Private).Invoke(e, new object[] { false });
                }
                Tick(s, 1000);
                check("Defeat cancels pending spawns and clears combat", s.Phase == SessionPhase.Defeat && s.ActiveEnemies.Count == 0 &&
                    s.ActiveProjectileCount == 0 && Mathf.Approximately(Time.timeScale, 1), "No enemies respawn after defeat.");
            }
            catch (Exception ex)
            {
                report.error = ex.ToString(); check("Validation completed", false, ex.Message);
                Debug.LogException(ex);
            }
            finally { if (s) s.RestartLevel(); Time.timeScale = 1; }
            report.checks = checks.ToArray(); report.failures = checks.Count(c => !c.passed); report.passed = report.failures == 0;
            Directory.CreateDirectory("Captures/SpellGameplay");
            File.WriteAllText("Captures/SpellGameplay/CombatBoundaryChecks.json", JsonUtility.ToJson(report, true));
            Debug.Log("Combat boundary checks: " + (checks.Count - report.failures) + "/" + checks.Count + " passed.");
        }
    }
}
