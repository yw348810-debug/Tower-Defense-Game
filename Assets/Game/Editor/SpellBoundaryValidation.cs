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
    /// <summary>Runtime scenarios for mana rewards, lane separation and periodic spell damage.</summary>
    public static class SpellBoundaryValidation
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
            var hud = s.GetComponent<GameHud>();
            var rows = new List<RicherBattleValidation.CheckResult>();
            Action<string, bool, string> check = (name, ok, detail) => rows.Add(new RicherBattleValidation.CheckResult
                { name = name, passed = ok, detail = detail });
            var report = new RicherBattleValidation.CriticalReport();
            try
            {
                s.RestartLevel();
                bool empty = s.TryCastFireball(Vector3.zero);
                check("Zero starting mana rejects casts", s.Mana == 0 && !empty && s.SpellsCast == 0 && s.ActiveSpellCount == 0,
                    "No mana, no spell and no charge.");
                Tick(s, 31);
                var normalProfile = s.enemyProfiles.First(p => p.kind == EnemyKind.Normal);
                var pair = s.ActiveEnemies.Where(e => e.MaxHealth == normalProfile.health).Take(2).ToArray();
                check("Enemies use both parallel lanes", pair.Length == 2 && Mathf.Approximately(pair[0].LateralOffset, -s.laneOffset) &&
                    Mathf.Approximately(pair[1].LateralOffset, s.laneOffset) && Mathf.Abs(pair[0].transform.position.z - pair[1].transform.position.z) > 1.4f,
                    "First two enemies stand 1.44m apart across the road.");
                int aHP = pair[0].CurrentHealth, bHP = pair[1].CurrentHealth;
                var blast = s.towerPrefabs[(int)TowerKind.Cannon].GetShotSpec();
                typeof(LevelSession).GetMethod("ResolveShot", Private).Invoke(s, new object[] { pair[0], blast });
                check("Blast hits enemies in both lanes", pair[0].CurrentHealth == aHP - blast.damage && pair[1].CurrentHealth == bHP - blast.damage,
                    "A single Blast shot damages the parallel neighbor within its AOE.");
                aHP = pair[0].CurrentHealth; bHP = pair[1].CurrentHealth;
                var gun = s.towerPrefabs[(int)TowerKind.Gun].GetShotSpec();
                typeof(LevelSession).GetMethod("ResolveShot", Private).Invoke(s, new object[] { pair[0], gun });
                check("Rapid fire remains single target", pair[0].CurrentHealth == aHP - gun.damage && pair[1].CurrentHealth == bHP,
                    "A rapid-fire shot leaves the neighboring lane enemy untouched.");
                foreach (var e in s.ActiveEnemies.ToArray().Take(12)) e.TakeDamage(100000);
                int earned = s.Mana;
                check("Kills award mana once", earned == 12 && s.Kills == 12, "12 normal kills earn 12 mana.");
                int beforeLeak = s.Mana;
                var leaking = s.ActiveEnemies.First();
                typeof(EnemyAgent).GetMethod("Resolve", Private).Invoke(leaking, new object[] { false });
                check("Leaked enemies award no mana", s.Mana == beforeLeak, "A leak cannot fund another spell.");
                check("Off-map cast has no payment", !s.TryCastFireball(new Vector3(100,0,0)) && s.Mana == earned, "Map bounds reject remote positions.");
                s.TogglePause();
                check("Paused cast has no payment", !s.TryCastFireball(Vector3.zero) && s.Mana == earned, "Pause freezes spell simulation.");
                s.TogglePause();
                Tick(s, 81);
                var target = s.ActiveEnemies.First(e => e.MaxHealth == s.enemyProfiles.First(p => p.kind == EnemyKind.Heavy).health);
                Vector3 point = new Vector3(0,.43f,-6);
                foreach (var e in s.ActiveEnemies) e.transform.position = point + Vector3.right * 10;
                target.transform.position = point;
                target.ApplyFrost(.6f, 2.5f);
                bool aimed = hud.BeginFireballTargeting();
                check("Spell card enters aiming without spending", aimed && hud.IsTargetingFireball && s.Mana == earned,
                    "Target preview alone never charges mana.");
                hud.CancelFireballTargeting();
                bool cast = s.TryCastFireball(point);
                var spell = s.GetComponentsInChildren<FireballArea>().First();
                check("Valid cast pays once", cast && s.Mana == earned - s.FireballCost && s.SpellsCast == 1 && s.ActiveSpellCount == 1,
                    "The fireball consumes exactly its configured cost.");
                int initialHP = target.CurrentHealth;
                spell.Advance(.249f);
                check("Fireball falls before damage begins", target.CurrentHealth == initialHP && s.SpellDamageTicks == 0, "No damage before impact.");
                spell.Advance(.002f);
                int perTick = Mathf.Max(1, Mathf.RoundToInt(s.fireballTickDamage * (1 - target.DamageReduction)));
                check("Impact starts periodic AOE", target.CurrentHealth == initialHP - perTick && s.SpellDamageTicks == 1,
                    "One tick at impact; armor still applies.");
                spell.Advance(1f);
                target.transform.position = point + Vector3.right * (s.FireballRadius + 1);
                int outsideHP = target.CurrentHealth;
                spell.Advance(.5f);
                check("Leaving the circle stops damage", target.CurrentHealth == outsideHP && s.SpellDamageTicks == 4,
                    "Targets are checked inside the circle each tick.");
                target.transform.position = point;
                spell.Advance(.5f); spell.Advance(.5f); spell.Advance(.5f);
                check("Burn lasts three seconds and expires", s.SpellDamageTicks == 6 && s.ActiveSpellCount == 0 &&
                    target.CurrentHealth == initialHP - perTick * 5, "Six half-second ticks; one missed while outside.");
                check("Fireball does not consume frost or shatter", target.IsFrosted && s.ShattersTriggered == 0,
                    "Fireball is independent of Blast's shatter trigger.");
                foreach (var e in s.ActiveEnemies.Where(e => e != target && e.Reward == normalProfile.reward).ToArray().Take(12)) e.TakeDamage(100000);
                bool recast = s.TryCastFireball(point);
                var reused = s.GetComponentsInChildren<FireballArea>().First();
                check("Pooled fireball resets for another cast", recast && ReferenceEquals(spell, reused) && reused.TicksApplied == 0,
                    "No ticks or callbacks leak into the next cast.");
                s.RestartLevel(); reused.Advance(5);
                check("Restart clears mana and active spells", s.Mana == 0 && s.SpellsCast == 0 && s.SpellDamageTicks == 0 && s.ActiveSpellCount == 0,
                    "A cancelled burn cannot damage a new round.");
            }
            catch (Exception ex)
            {
                report.error = ex.ToString(); check("Validation completed", false, ex.Message); Debug.LogException(ex);
            }
            finally { if (s) s.RestartLevel(); Time.timeScale = 1; hud.CancelFireballTargeting(); }
            report.checks = rows.ToArray(); report.failures = rows.Count(r => !r.passed); report.passed = report.failures == 0;
            Directory.CreateDirectory("Captures/SpellGameplay");
            File.WriteAllText("Captures/SpellGameplay/SpellBoundaryChecks.json", JsonUtility.ToJson(report, true));
            Debug.Log("Spell/lane boundary checks: " + (rows.Count - report.failures) + "/" + rows.Count + " passed.");
        }
    }
}
