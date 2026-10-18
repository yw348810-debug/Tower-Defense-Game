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
    public static class AugmentRefreshValidation
    {
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public int level, failures; public string error; public Check[] checks; }
        public static Report Run()
        {
            var s = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (!EditorApplication.isPlaying || !s) throw new InvalidOperationException("Requires a running battle.");
            var rows = new List<Check>(); var report = new Report { level = s.levelIndex };
            Action<string, bool> check = (name, pass) => rows.Add(new Check { name = name, passed = pass });
            int originalSeed = s.augmentSeed; bool originalEnabled = s.enableAugments;
            try
            {
                check("Refresh is available in this level", s.AugmentRefreshUnlocked);
                bool eachSlot = true, invariant = true, pause = true, reset = true;
                for (int seed = 0; seed < 30; seed++)
                {
                    s.enableAugments = true; s.augmentSeed = 94000 + seed; s.RestartLevel();
                    reset &= s.AugmentRefreshesUsed == 0;
                    for (int choice = 0; choice < s.ActiveAugmentChoiceCount; choice++)
                    {
                        if (choice > 0)
                        {
                            typeof(LevelSession).GetProperty("AssaultTime").SetValue(s, s.waves[s.augmentChoiceWaveIndices[choice]].startTime);
                            typeof(LevelSession).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(s, null);
                        }
                        pause &= s.IsChoosingAugment && Mathf.Approximately(Time.timeScale, 0f);
                        var original = s.AugmentOffers.Select(c => c.Id).ToArray();
                        s.OpenSystemMenu();
                        pause &= !s.CanRefreshAugment(0) && !s.TryRefreshAugment(0)
                            && original.SequenceEqual(s.AugmentOffers.Select(c => c.Id));
                        s.CloseSystemMenu();
                        eachSlot &= !s.TryRefreshAugment(-1) && !s.TryRefreshAugment(3);
                        for (int slot = 0; slot < 3; slot++)
                        {
                            var before = s.AugmentOffers[slot]; int selected = s.SelectedAugments.Count;
                            eachSlot &= s.CanRefreshAugment(slot) && s.TryRefreshAugment(slot)
                                && !s.CanRefreshAugment(slot) && !s.TryRefreshAugment(slot);
                            invariant &= s.AugmentOffers[slot].Id != before.Id && s.AugmentOffers[slot].Tier == before.Tier
                                && s.AugmentOffers.Select(c => c.Id).Distinct().Count() == 3
                                && s.AugmentOffers.Any(c => c.IsGeneric) && s.SelectedAugments.Count == selected
                                && s.AugmentOffers.All(c => c.MinimumLevel <= s.levelIndex)
                                && (s.levelIndex > 1 || s.AugmentOffers.All(c => c.Tier != AugmentTier.Prismatic));
                            pause &= s.IsChoosingAugment && Mathf.Approximately(Time.timeScale, 0f);
                        }
                        eachSlot &= s.TryChooseAugment(0) && !s.TryRefreshAugment(0);
                    }
                    eachSlot &= s.AugmentRefreshesUsed == s.ActiveAugmentChoiceCount * 3;
                }
                check("Thirty seeds allow every card to refresh exactly once at every choice", eachSlot);
                check("Refresh preserves tier, distinct offers, unlocked cards and generic option", invariant);
                check("Choice remains paused and settings cannot refresh behind the overlay", pause);
                check("Restart clears the per-choice refresh budget", reset);
            }
            catch (Exception e) { report.error = e.ToString(); check("Validation completed without exception", false); }
            finally { s.enableAugments = originalEnabled; s.augmentSeed = originalSeed; s.RestartLevel(); }
            report.checks = rows.ToArray(); report.failures = rows.Count(c => !c.passed); report.passed = report.failures == 0;
            Directory.CreateDirectory("Captures/Polish");
            File.WriteAllText("Captures/Polish/augment-refresh-level" + s.levelIndex + ".json", JsonUtility.ToJson(report, true));
            return report;
        }
    }
}
