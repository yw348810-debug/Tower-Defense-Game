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
    /// <summary>Catalog invariants and bounded elemental, economy, mastery and army interactions.</summary>
    public static class AugmentSynergyValidation
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [Serializable] public sealed class Check { public string name; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public int failures; public string error; public Check[] checks; }
        static object Call(object target, string name, params object[] args) => target.GetType().GetMethods(Private)
            .First(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(target, args);
        static void Set(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
        static void Fresh(LevelSession s, params AugmentId[] cards)
        {
            s.enableAugments = false; s.RestartLevel(); Set(s, "Gold", 20000); Set(s, "Mana", 1000);
            foreach (var card in cards) Call(s, "ApplyAugment", card);
            Set(s, "AssaultTime", 10f); Call(s, "Update");
            // Update spawns the fixture wave but advances by the real frame delta.
            // Keep subsequent exact cooldown boundaries independent of editor frame rate.
            Set(s, "AssaultTime", 10f);
            foreach (var enemy in s.ActiveEnemies) enemy.transform.position = new Vector3(1000f, 0f, 1000f);
        }
        static EnemyAgent Target(LevelSession s, Vector3 point)
        {
            var enemy = s.ActiveEnemies.First(e => e && e.IsAlive);
            enemy.ApplyDifficultyStats(1000, 1f, enemy.Reward, 1); enemy.DamageReduction = 0f;
            enemy.transform.position = point; return enemy;
        }
        static TowerAgent Build(LevelSession s, TowerKind kind)
        {
            var slot = s.map.buildSlots.First(p => p && !s.HasTower(p)
                && (kind != TowerKind.Barracks || s.TryGetRallyPoint(p.transform.position, out var rally)
                    && (new Vector2(rally.x - p.transform.position.x, rally.z - p.transform.position.z)).sqrMagnitude <= 49f));
            if (!s.TryBuildTower(slot, kind)) throw new Exception("Cannot build " + kind);
            return s.GetTower(slot);
        }
        static ShotSpec Shot(TowerKind kind, TowerAgent tower = null, bool secondary = false) => new ShotSpec
            { kind = kind, source = tower, damage = 10, slowMultiplier = .6f, slowDuration = 2.5f,
                splashRadius = 1.8f, shatterRadius = 2f, shatterDamage = 10, color = Color.cyan, isSecondary = secondary };
        static void Hit(LevelSession s, EnemyAgent e, ShotSpec shot) => Call(s, "ResolveShot", e, shot);

        public static Report Run()
        {
            if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode.");
            var s = UnityEngine.Object.FindAnyObjectByType<LevelSession>();
            if (!s) throw new Exception("No running LevelSession.");
            var report = new Report(); var rows = new List<Check>();
            bool originalAugments = s.enableAugments; int originalSeed = s.augmentSeed;
            var originalDifficulty = GameSettings.Difficulty; var originalLanguage = GameSettings.Language;
            Action<string, bool> check = (name, pass) => rows.Add(new Check { name = name, passed = pass });
            try
            {
                GameSettings.SetDifficulty(GameDifficulty.Hard); GameSettings.SetLanguage(GameLanguage.Chinese);
                check("Exactly 54 unique draftable IDs", AugmentCatalog.All.Count == 54 && AugmentCatalog.All.Select(d => d.Id).Distinct().Count() == 54);
                foreach (AugmentTier tier in Enum.GetValues(typeof(AugmentTier)))
                    check(tier + " contains exactly 18 cards", AugmentCatalog.All.Count(c => c.Tier == tier) == 18);
                check("Every card has bilingual text and a short synergy hint", AugmentCatalog.All.All(c => c.Name != c.ChineseName
                    && c.Description != c.ChineseDescription && c.SynergyHint.Length > 0 && c.ChineseSynergyHint.Length > 0));
                check("Every card translates its current description and hint", AugmentCatalog.All.All(c => Localization.T(c.Description) == c.ChineseDescription
                    && Localization.T(c.SynergyHint) == c.ChineseSynergyHint && Localization.T(c.Name) == c.ChineseName));
                check("Canonical description plus synergy keeps Chinese when joined", AugmentCatalog.All.All(c => Localization.T(c.Description + "\n" + c.SynergyHint)
                    == c.ChineseDescription + "\n" + c.ChineseSynergyHint));
                check("Multi-line tower stats retain appended max-level ability translation", Localization.T("Base damage 20    Range 6\nBurst shells: splash 45% / R 1.25")
                    == "基础伤害 20    范围 6\n爆裂弹：溅射45%伤害 / 半径1.25");
                check("First-level silver and gold pools retain at least ten cards", AugmentCatalog.All.Count(c => c.Tier == AugmentTier.Silver && c.MinimumLevel == 1) >= 10
                    && AugmentCatalog.All.Count(c => c.Tier == AugmentTier.Gold && c.MinimumLevel == 1) >= 10);
                check("First-level pool excludes prismatic and future mechanics", AugmentCatalog.All.Where(c => c.MinimumLevel <= 1).All(c => c.Tier != AugmentTier.Prismatic)
                    && AugmentCatalog.Get(AugmentId.ArchmageCycle).MinimumLevel == 3 && AugmentCatalog.Get(AugmentId.SpellPrimer).MinimumLevel == 2);
                var retired = new[] { AugmentId.EmergencyShield, AugmentId.StoredMana, AugmentId.HeavyHunter,
                    AugmentId.TypeSpecialization, AugmentId.FieldMedic, AugmentId.LoneGuardian, AugmentId.LastStandEngine };
                check("Retired cards remain compatible but cannot be drafted", retired.All(id => AugmentCatalog.Get(id) != null && AugmentCatalog.All.All(c => c.Id != id)));
                bool offers = true;
                for (int seed = 0; seed < 12; seed++)
                {
                    s.enableAugments = true; s.augmentSeed = 72000 + seed; s.RestartLevel();
                    for (int slot = 0; slot < 3; slot++)
                        offers &= s.TryRefreshAugment(slot) && !s.TryRefreshAugment(slot) && s.AugmentOffers.Select(c => c.Id).Distinct().Count() == 3
                            && s.AugmentOffers.All(c => c.Tier == AugmentTier.Silver && c.MinimumLevel <= s.levelIndex)
                            && s.AugmentOffers.Any(c => c.IsGeneric);
                }
                check("Twelve seeds retain tier, eligibility, unique offers and once-per-slot refresh", offers);

                Fresh(s, AugmentId.Crossfire); var target = Target(s, Vector3.zero); int hp = target.CurrentHealth;
                Hit(s, target, Shot(TowerKind.Gun)); Hit(s, target, Shot(TowerKind.Frost));
                check("Two different normal tower hits trigger twelve crossfire damage", s.CrossfireBursts == 1 && hp - target.CurrentHealth == 32);
                Hit(s, target, Shot(TowerKind.Gun));
                check("Crossfire respects per-target cooldown", s.CrossfireBursts == 1);
                Set(s, "AssaultTime", 14f); Hit(s, target, Shot(TowerKind.Cannon, null, true));
                check("Secondary hits cannot trigger crossfire", s.CrossfireBursts == 1);
                Hit(s, target, Shot(TowerKind.Frost)); Hit(s, target, Shot(TowerKind.Gun));
                check("Crossfire works again after its cooldown", s.CrossfireBursts == 2);

                Fresh(s, AugmentId.FrostBattery); target = Target(s, Vector3.zero); int mana = s.Mana;
                Hit(s, target, Shot(TowerKind.Frost)); Hit(s, target, Shot(TowerKind.Frost));
                check("Frost battery has a global four-second mana cooldown", s.Mana == mana + 1);
                Set(s, "AssaultTime", 14f); Hit(s, target, Shot(TowerKind.Frost, null, true));
                check("Bonus frost projectiles grant no mana", s.Mana == mana + 1);
                Hit(s, target, Shot(TowerKind.Frost)); check("Frost battery recharges", s.Mana == mana + 2);

                Fresh(s, AugmentId.PowderKeg); var cannon = Build(s, TowerKind.Cannon); target = Target(s, Vector3.zero);
                for (int i = 0; i < 3; i++) Hit(s, target, Shot(TowerKind.Cannon, cannon));
                check("Powder keg waits for four ordinary cannon impacts", s.PowderBursts == 0);
                Hit(s, target, Shot(TowerKind.Cannon, cannon)); Hit(s, target, Shot(TowerKind.Cannon, cannon, true));
                check("Fourth hit explodes exactly once, secondary cannot retrigger", s.PowderBursts == 1);

                Fresh(s, AugmentId.SteamCircuit, AugmentId.ElementalBoil); target = Target(s, Vector3.zero);
                target.ApplyFrost(.6f, 3f); hp = target.CurrentHealth; mana = s.Mana;
                Call(s, "ApplyFireballDamage", Vector3.zero, 3f, 10);
                check("Steam circuit and elemental boil add their damage in one bounded burst", hp - target.CurrentHealth == 64 && s.SteamBursts == 1 && s.Mana == mana + 1);
                target.ApplyFrost(.6f, 3f); Call(s, "ApplyFireballDamage", Vector3.zero, 3f, 10);
                check("Repeated fire ticks cannot repeat steam within five seconds", s.SteamBursts == 1 && s.Mana == mana + 1);
                Set(s, "AssaultTime", 16f); target.ApplyFrost(.6f, 3f); Call(s, "ApplyFireballDamage", Vector3.zero, 3f, 10);
                check("Steam recharge triggers once again", s.SteamBursts == 2 && s.Mana == mana + 2);

                Fresh(s, AugmentId.PatientAim, AugmentId.Masterworks, AugmentId.UpgradeRelay);
                var gun = Build(s, TowerKind.Gun); s.TryUpgradeTower(gun.Slot); s.TryUpgradeTower(gun.Slot); target = Target(s, Vector3.zero);
                check("Precision craft is active only on max-level attacking towers", Mathf.Approximately(s.AugmentTowerDamageBonus(target, TowerKind.Gun, gun, false), .15f));
                mana = s.Mana; s.OnAugmentSpellCast(SpellKind.Fireball, Vector3.zero);
                check("Casting charges max towers and refunds two mana", Mathf.Approximately(s.AugmentAttackSpeedMultiplier(gun), 1.25f)
                    && Mathf.Approximately(s.AugmentTowerDamageBonus(target, TowerKind.Gun, gun, false), .35f) && s.Mana == mana + 2);
                s.OnAugmentSpellCast(SpellKind.Fireball, Vector3.zero); check("Relay refund has a five-second cooldown", s.Mana == mana + 2);
                Set(s, "AssaultTime", 17f); check("Masterwork resonance expires without permanent stat mutation", Mathf.Approximately(s.AugmentAttackSpeedMultiplier(gun), 1f)
                    && Mathf.Approximately(s.AugmentTowerDamageBonus(target, TowerKind.Gun, gun, false), .15f));

                Fresh(s, AugmentId.FinishingBlow); target = Target(s, Vector3.zero);
                check("Execution ignores high-health enemies", s.AugmentTowerDamageBonus(target, TowerKind.Gun, null, false) == 0f);
                target.TakeDamage(760); check("Execution applies below twenty-five percent health", Mathf.Approximately(s.AugmentTowerDamageBonus(target, TowerKind.Gun, null, false), .35f));

                if (s.levelIndex >= 2)
                {
                    Fresh(s, AugmentId.FieldWorkshop, AugmentId.SpellPrimer, AugmentId.ArcaneInvestment); mana = s.Mana;
                    int price = s.GetSpellUpgradeCost(SpellKind.Fireball);
                    check("Workshop discounts spell upgrades from level two onward", price == 64 && s.TryUpgradeSpell(SpellKind.Fireball));
                    check("Paid spell upgrade grants primer and actual-cost investment mana", s.Mana == mana + 6 + price / 12);
                    gun = Build(s, TowerKind.Gun); mana = s.Mana; int gold = s.Gold; s.TryUpgradeTower(gun.Slot);
                    check("Tower investment refund uses gold actually paid", s.Mana == mana + (gold - s.Gold) / 12);
                    Fresh(s, AugmentId.ArcaneInvestment, AugmentId.UltimateRetrofit); gun = Build(s, TowerKind.Gun); mana = s.Mana; s.TryUpgradeTower(gun.Slot);
                    check("Free retrofit grants no gold-spent mana refund", gun.IsMaxLevel && s.Mana == mana);

                    Fresh(s, AugmentId.ColdFront, AugmentId.GlacialCataclysm); target = Target(s, Vector3.zero); hp = target.CurrentHealth;
                    check("Cold front and glacial cataclysm trigger real frost damage and vulnerability", s.TryCastFrostNova(Vector3.zero)
                        && target.IsFrozen && target.VulnerabilityBonus >= .15f && hp - target.CurrentHealth >= 85);
                    Fresh(s, AugmentId.GuardianBond, AugmentId.PhoenixPact, AugmentId.ExplosiveFarewell);
                    var barracks = Build(s, TowerKind.Barracks); var troop = barracks.Barracks.Troops.First();
                    check("Guardian bond increases recruit maximum HP", troop.MaxHealth == Mathf.RoundToInt(90 * 1.15f));
                    target = Target(s, troop.transform.position); mana = s.Mana; hp = target.CurrentHealth; troop.TakeDamage(99999);
                    check("A real recruit death grants phoenix mana and legacy damage", s.Mana == mana + 3 && hp - target.CurrentHealth == 35);
                    var next = barracks.Barracks.Troops.First(); next.TakeDamage(99999);
                    check("Simultaneous troop deaths respect global phoenix and explosion cooldowns", s.Mana == mana + 3 && hp - target.CurrentHealth == 35);
                }

                if (s.levelIndex >= 3)
                {
                    Fresh(s, AugmentId.GrandConvergence); target = Target(s, Vector3.zero);
                    target.ApplyFrost(.6f, 3f); target.ApplyVulnerability(.15f, 4f); Call(s, "ApplyFireballDamage", Vector3.zero, 3f, 10);
                    check("Three-phase fire frost vulnerability creates one convergence", s.ConvergenceBursts == 1);
                    target.ApplyFrost(.6f, 3f); Call(s, "ApplyFireballDamage", Vector3.zero, 3f, 10);
                    check("Convergence cannot chain through the same tick or cooldown", s.ConvergenceBursts == 1);
                    Fresh(s, AugmentId.ArchmageCycle); mana = s.Mana;
                    s.OnAugmentSpellCast(SpellKind.Fireball, Vector3.zero); s.OnAugmentSpellCast(SpellKind.FrostNova, Vector3.zero);
                    check("Two spell colors do not finish the cycle", s.SpellCyclesTriggered == 0);
                    s.OnAugmentSpellCast(SpellKind.Reinforcements, Vector3.zero);
                    check("Three different manual spells grant one free charge and eight mana", s.SpellCyclesTriggered == 1 && s.FreeFireballReady && s.Mana == mana + 8);
                    s.OnAugmentSpellCast(SpellKind.Fireball, Vector3.zero); s.OnAugmentSpellCast(SpellKind.FrostNova, Vector3.zero);
                    s.OnAugmentSpellCast(SpellKind.Reinforcements, Vector3.zero);
                    check("Three-color cycle respects twelve-second global cooldown", s.SpellCyclesTriggered == 1);
                    Fresh(s, AugmentId.EternalLegion, AugmentId.Warpath, AugmentId.ArcaneSupremacy);
                    check("Army and warpath extend only their respective spell durations", s.ReinforcementDuration == 30f && s.BattlecryDuration == 11f);
                    s.TryUpgradeSpell(SpellKind.Fireball); s.TryUpgradeSpell(SpellKind.Fireball);
                    check("Supremacy scales max-level Fireball but leaves basic Frost burst unchanged", s.AugmentSpellDamage(20, SpellKind.Fireball) == 26 && s.AugmentSpellDamage(20, SpellKind.FrostNova) == 20);
                }

                Fresh(s, AugmentId.SanctuaryRoots);
                check("Sanctuary roots modify healing and vulnerability duration", s.AugmentSupportHealingBonus(6) == 9 && s.AugmentSupportDurationBonus(2f) == 3f);
                s.RestartLevel();
                check("Restart removes selected cards, trigger counters and temporary resonance", s.SelectedAugments.Count == 0 && s.CrossfireBursts == 0
                    && s.SteamBursts == 0 && s.ConvergenceBursts == 0 && s.SpellCyclesTriggered == 0 && !s.IsMasterworkResonanceActive);
                check("Restart restores support and army modifiers", s.AugmentSupportHealingBonus(6) == 6 && s.AugmentSupportDurationBonus(2f) == 2f
                    && s.AugmentReinforcementDurationBonus == 0f && s.AugmentBattlecryDurationBonus == 0f);
            }
            catch (Exception exception) { report.error = exception.ToString(); check("Validation completed without exception", false); }
            finally
            {
                GameSettings.SetDifficulty(originalDifficulty); GameSettings.SetLanguage(originalLanguage);
                s.enableAugments = originalAugments; s.augmentSeed = originalSeed; s.RestartLevel();
            }
            report.checks = rows.ToArray(); report.failures = rows.Count(row => !row.passed); report.passed = report.failures == 0;
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Captures", "AugmentRework"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "synergy-validation-level" + s.levelIndex + ".json"), JsonUtility.ToJson(report, true));
            return report;
        }
    }
}
