using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerDefense.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    public static class VfxPolishValidation
    {
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public int failures, level; public Check[] checks; }
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        public static Report Run()
        {
            var s = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (!EditorApplication.isPlaying || !s) throw new InvalidOperationException("Requires a running battle.");
            var effects = CombatVisuals.For(s); effects.ClearBursts();
            var rows = new List<Check>();
            Action<string, bool> check = (name, pass) => rows.Add(new Check { name = name, passed = pass });
            int kills = s.Kills, gold = s.Gold, mana = s.Mana;
            float clock = s.AssaultTime, scale = Time.timeScale;
            check("Effects belong to the current session", effects.gameObject == s.gameObject);
            for (int i = 0; i < CombatVisuals.BurstCapacity * 3; i++)
                effects.Impact(new Vector3(i % 7, .5f, i % 5), (TowerKind)(i % 3), Color.cyan);
            var all = (IList)typeof(CombatVisuals).GetField("all", Private).GetValue(effects);
            check("Burst pool remains bounded during a large assault", effects.ActiveBurstCount <= CombatVisuals.BurstCapacity
                && effects.PeakBurstCount <= CombatVisuals.BurstCapacity && all.Count <= CombatVisuals.BurstCapacity);
            var particles = effects.GetComponentsInChildren<ParticleSystem>(true);
            check("Particle clocks respect pause", particles.All(p => !p.main.useUnscaledTime));
            Time.timeScale = 0f;
            int active = effects.ActiveBurstCount;
            typeof(CombatVisuals).GetMethod("LateUpdate", Private).Invoke(effects, null);
            check("Pause does not advance or return active bursts", effects.ActiveBurstCount == active);
            check("Visual effects do not change combat or economy", kills == s.Kills && gold == s.Gold && mana == s.Mana
                && Mathf.Approximately(clock, s.AssaultTime));
            check("Trails support translucent rendering", effects.RibbonMaterial.renderQueue >= 3000
                && effects.RibbonMaterial.GetFloat("_ZWrite") == 0f);
            s.RestartLevel();
            check("Restart clears all bursts, including an assault at time zero", effects.ActiveBurstCount == 0
                && particles.All(p => p.particleCount == 0));
            // Restart owns the new round's time scale, including its paused opening card choice.
            if (!s.IsChoosingAugment) Time.timeScale = scale;
            var report = new Report { level = s.levelIndex, checks = rows.ToArray(), failures = rows.Count(c => !c.passed) };
            report.passed = report.failures == 0;
            Directory.CreateDirectory("Captures/Polish");
            File.WriteAllText("Captures/Polish/vfx-boundaries-level" + s.levelIndex + ".json", JsonUtility.ToJson(report, true));
            return report;
        }
    }
}
