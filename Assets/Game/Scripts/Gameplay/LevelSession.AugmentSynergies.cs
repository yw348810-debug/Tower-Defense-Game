using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TowerDefense.Gameplay
{
    /// <summary>Bounded, run-owned synergy triggers. Secondary damage never invokes attack/cast hooks.</summary>
    public sealed partial class LevelSession
    {
        struct CrossfireRecord { public int version; public TowerKind kind; public float lastHit, readyAt; }
        readonly Dictionary<EnemyAgent, CrossfireRecord> crossfireRecords = new Dictionary<EnemyAgent, CrossfireRecord>();
        readonly Dictionary<TowerAgent, int> powderCounters = new Dictionary<TowerAgent, int>();
        readonly Dictionary<EnemyAgent, TimedSpawn> convergenceCooldowns = new Dictionary<EnemyAgent, TimedSpawn>();
        readonly Dictionary<SpellKind, float> recentSpellKinds = new Dictionary<SpellKind, float>();
        float frostBatteryReadyAt, relayReadyAt, reactorReadyAt, farewellReadyAt, phoenixReadyAt;
        float convergenceReadyAt, cycleReadyAt, masterworksUntil, allyHealCountdown;
        int economyWindow = -1, emberPayments, cinderPayments, steamPayments;

        public int CrossfireBursts { get; private set; }
        public int PowderBursts { get; private set; }
        public int ConvergenceBursts { get; private set; }
        public int SpellCyclesTriggered { get; private set; }
        public int AugmentManaGranted { get; private set; }
        public int AugmentGoldGranted { get; private set; }
        public int AugmentHealingApplied { get; private set; }
        public float AugmentReinforcementDurationBonus => Augments.Has(AugmentId.EternalLegion) ? 12f : 0f;
        public float AugmentBattlecryDurationBonus => Augments.Has(AugmentId.Warpath) ? 3f : 0f;
        public bool IsMasterworkResonanceActive => masterworksUntil > AssaultTime;

        bool IsSynergyAugmentEligible(AugmentId id)
        {
            int empty = map.buildSlots.Count(slot => slot && !HasTower(slot));
            bool CanOwn(TowerKind kind) => towers.Values.Any(t => t && t.kind == kind) || empty > 0 && GetTowerPrefab(kind);
            bool CanOwnBoth(TowerKind a, TowerKind b)
            {
                int missing = (towers.Values.Any(t => t && t.kind == a) ? 0 : 1) + (towers.Values.Any(t => t && t.kind == b) ? 0 : 1);
                return CanOwn(a) && CanOwn(b) && empty >= missing;
            }
            switch (id)
            {
                case AugmentId.FrostBattery: return CanOwn(TowerKind.Frost);
                case AugmentId.PowderKeg: return CanOwn(TowerKind.Cannon);
                case AugmentId.Crossfire:
                    return CanOwnBoth(TowerKind.Gun, TowerKind.Frost) || CanOwnBoth(TowerKind.Gun, TowerKind.Cannon)
                        || CanOwnBoth(TowerKind.Frost, TowerKind.Cannon) || CanOwnBoth(TowerKind.Gun, TowerKind.Barracks);
                case AugmentId.HeroicGuard:
                case AugmentId.GuardianBond:
                case AugmentId.ExplosiveFarewell:
                case AugmentId.PhoenixPact: return CanOwn(TowerKind.Barracks) || ReinforcementsUnlocked;
                case AugmentId.SiegeFormation: return ReinforcementsUnlocked ? CanOwn(TowerKind.Cannon) : CanOwnBoth(TowerKind.Cannon, TowerKind.Barracks);
                case AugmentId.SanctuaryRoots:
                case AugmentId.SanctuaryReactor: return CanOwn(TowerKind.Support);
                case AugmentId.GrandConvergence: return (CanOwn(TowerKind.Frost) || FrostSpellUnlocked)
                    && (CanOwn(TowerKind.Support) || Augments.Has(AugmentId.GlacialCataclysm));
                case AugmentId.SteamCircuit: return CanOwn(TowerKind.Frost) || FrostSpellUnlocked;
                case AugmentId.SpellPrimer: return SpellUpgradeUnlocked && HasUpgradeableSpell();
                case AugmentId.ArcaneInvestment: return empty > 0 || towers.Values.Any(t => t && !t.IsMaxLevel) || HasUpgradeableSpell();
                case AugmentId.ArcaneSupremacy: return SpellUpgradeUnlocked;
                case AugmentId.ColdFront:
                case AugmentId.GlacialCataclysm: return FrostSpellUnlocked;
                case AugmentId.WarDrums:
                case AugmentId.Warpath: return BattlecryUnlocked;
                case AugmentId.EternalLegion: return ReinforcementsUnlocked;
                case AugmentId.ArchmageCycle: return (FrostSpellUnlocked ? 1 : 0) + (ReinforcementsUnlocked ? 1 : 0) + (BattlecryUnlocked ? 1 : 0) >= 2;
                case AugmentId.PatientAim:
                case AugmentId.Masterworks: return CanOwn(TowerKind.Gun) || CanOwn(TowerKind.Frost) || CanOwn(TowerKind.Cannon);
                default: return true;
            }
        }

        bool HasUpgradeableSpell() => SpellUpgradeUnlocked && (GetSpellLevel(SpellKind.Fireball) < 3
            || FrostSpellUnlocked && GetSpellLevel(SpellKind.FrostNova) < 3
            || ReinforcementsUnlocked && GetSpellLevel(SpellKind.Reinforcements) < 3
            || BattlecryUnlocked && GetSpellLevel(SpellKind.Battlecry) < 3);

        public int AugmentSpellUpgradeCost(int basic) => Mathf.Max(1, Mathf.CeilToInt(basic * (1f - Augments.UpgradeDiscount)));
        public int AugmentSupportHealingBonus(int basic) => basic + (Augments.Has(AugmentId.SanctuaryRoots) ? 3 : 0);
        public float AugmentSupportDurationBonus(float basic) => basic + (Augments.Has(AugmentId.SanctuaryRoots) ? 1f : 0f);
        public float AugmentAttackSpeedMultiplier(TowerAgent source)
            => source && source.IsMaxLevel && source.kind != TowerKind.Barracks && source.kind != TowerKind.Support
                && IsMasterworkResonanceActive && Augments.Has(AugmentId.Masterworks) ? 1.25f : 1f;

        public float AugmentTowerDamageBonus(EnemyAgent target, TowerKind kind, TowerAgent source, bool shatter)
        {
            if (!target) return 0f;
            float bonus = 0f;
            bool attackingTower = kind != TowerKind.Barracks && kind != TowerKind.Support;
            if (source && source.IsMaxLevel && attackingTower)
            {
                if (Augments.Has(AugmentId.PatientAim)) bonus += .15f;
                if (Augments.Has(AugmentId.Masterworks) && IsMasterworkResonanceActive) bonus += .2f;
            }
            if (Augments.Has(AugmentId.FinishingBlow) && target.CurrentHealth <= target.MaxHealth * .25f) bonus += .35f;
            if (kind == TowerKind.Cannon && target.Blocker && Augments.Has(AugmentId.SiegeFormation)) bonus += .35f;
            if (source && attackingTower && Augments.Has(AugmentId.GuardianBond)
                && GetFriendlyUnits().Any(ally => ally && ally.IsAlive && HorizontalSquared(ally.transform.position - source.transform.position) <= 25f))
                bonus += .2f;
            if (Augments.Has(AugmentId.Warpath) && BattlecryRemaining > 0f
                && (kind == TowerKind.Barracks || source && source.IsMaxLevel)) bonus += .35f;
            return bonus;
        }

        public int AugmentSpellDamage(int basic, SpellKind kind)
            => Mathf.Max(1, Mathf.RoundToInt(basic * (Augments.Has(AugmentId.ArcaneSupremacy)
                && GetSpellLevel(kind) >= 3 ? 1.3f : 1f)));

        void GrantAugmentMana(int amount)
        {
            if (amount <= 0) return;
            Mana += amount; AugmentManaGranted += amount; Changed?.Invoke();
        }

        void RefreshEconomyWindow()
        {
            int window = Mathf.FloorToInt(AssaultTime);
            if (window == economyWindow) return;
            economyWindow = window; emberPayments = cinderPayments = steamPayments = 0;
        }

        bool IsPointBurning(Vector3 point)
            => fireballs.Any(spell => spell && spell.ContainsBurningPoint(point));

        public void OnAugmentEnemyKilled(EnemyAgent enemy)
        {
            if (!enemy || !IsCombatRunning) return;
            RefreshEconomyWindow();
            bool burning = IsPointBurning(enemy.transform.position);
            if (burning && Augments.Has(AugmentId.EmberLedger) && emberPayments < 4)
            { emberPayments++; Gold++; AugmentGoldGranted++; }
            if (burning && Augments.Has(AugmentId.CinderHarvest) && cinderPayments < 3)
            { cinderPayments++; GrantAugmentMana(1); }
            if (Augments.Has(AugmentId.SanctuaryReactor) && enemy.VulnerabilityWhenKilled > 0f && AssaultTime >= reactorReadyAt)
            {
                reactorReadyAt = AssaultTime + 2f;
                GrantAugmentMana(2); HealAugmentAllies(enemy.transform.position, 5f, 12);
                PlayPulse(enemy.transform.position + Vector3.up * .3f, 2f, new Color(.4f, 1f, .8f));
            }
            crossfireRecords.Remove(enemy); convergenceCooldowns.Remove(enemy);
        }

        public void OnAugmentTowerBuilt(TowerAgent tower) { }

        public void OnAugmentTowerUpgraded(TowerAgent tower, int goldSpent = 0)
        {
            if (!tower) return;
            GrantInvestmentRefund(goldSpent);
        }

        public void OnAugmentSpellUpgraded(SpellKind kind, int goldSpent = 0)
        {
            if (Augments.Has(AugmentId.SpellPrimer)) GrantAugmentMana(6);
            GrantInvestmentRefund(goldSpent);
        }

        void GrantInvestmentRefund(int goldSpent)
        {
            if (Augments.Has(AugmentId.ArcaneInvestment)) GrantAugmentMana(Mathf.Min(12, Mathf.Max(0, goldSpent) / 12));
        }

        public void OnAugmentSpellCast(SpellKind kind, Vector3 point)
        {
            if (!IsCombatRunning) return;
            if (Augments.Has(AugmentId.Masterworks)) masterworksUntil = AssaultTime + 6f;
            if (Augments.Has(AugmentId.UpgradeRelay) && AssaultTime >= relayReadyAt && towers.Values.Any(tower => tower && tower.IsMaxLevel))
            { relayReadyAt = AssaultTime + 5f; GrantAugmentMana(2); }
            if (Augments.Has(AugmentId.ArchmageCycle))
            {
                foreach (var expired in recentSpellKinds.Where(pair => AssaultTime - pair.Value > 20f).Select(pair => pair.Key).ToArray())
                    recentSpellKinds.Remove(expired);
                recentSpellKinds[kind] = AssaultTime;
                if (recentSpellKinds.Count >= 3 && AssaultTime >= cycleReadyAt)
                {
                    cycleReadyAt = AssaultTime + 12f; recentSpellKinds.Clear();
                    freeFireballCharges = 1; SpellCyclesTriggered++; GrantAugmentMana(8);
                }
            }
            if (kind == SpellKind.Battlecry && Augments.Has(AugmentId.WarDrums))
            { GrantAugmentMana(6); HealAugmentAllies(point, float.PositiveInfinity, 40); }
            if (GetSpellLevel(kind) >= 3 && Augments.Has(AugmentId.ArcaneSupremacy)) HealAugmentAllies(point, 5f, 30);
            if (kind != SpellKind.FrostNova) return;
            int damage = 0;
            if (Augments.Has(AugmentId.ColdFront)) damage += 10 + GetSpellLevel(kind) * 15;
            if (Augments.Has(AugmentId.GlacialCataclysm))
            {
                damage += 60;
                foreach (var enemy in enemies)
                    if (enemy && enemy.IsAlive && HorizontalSquared(enemy.transform.position - point) <= FrostSpellRadius * FrostSpellRadius)
                        enemy.ApplyVulnerability(.15f, 4f);
            }
            if (damage > 0) SynergyDamageArea(point, FrostSpellRadius, AugmentSpellDamage(damage, kind));
        }

        public void OnAugmentSoldierHit(SoldierAgent soldier, EnemyAgent target, int damage)
        {
            if (!soldier || !target || !IsCombatRunning) return;
            TryCrossfire(target, TowerKind.Barracks);
        }

        public void OnAugmentSoldierDied(SoldierAgent soldier)
        {
            if (!soldier || !IsCombatRunning) return;
            Vector3 point = soldier.transform.position;
            if (Augments.Has(AugmentId.ExplosiveFarewell) && AssaultTime >= farewellReadyAt)
            {
                farewellReadyAt = AssaultTime + 2f;
                PlayPulse(point + Vector3.up * .3f, 2.5f, new Color(1f, .5f, .15f));
                SynergyDamageArea(point, 2.5f, 35);
            }
            if (Augments.Has(AugmentId.PhoenixPact) && AssaultTime >= phoenixReadyAt)
            {
                phoenixReadyAt = AssaultTime + 5f; GrantAugmentMana(3);
                HealAugmentAllies(point, 4f, 20, soldier);
            }
        }

        public void OnAugmentAllyHealed(SoldierAgent soldier, int actualHealing)
        {
            if (soldier && actualHealing > 0) AugmentHealingApplied += actualHealing;
        }

        void HealAugmentAllies(Vector3 point, float radius, int health, SoldierAgent exclude = null)
        {
            float squared = radius * radius;
            foreach (var ally in GetFriendlyUnits().ToArray())
                if (ally && ally != exclude && ally.IsAlive && HorizontalSquared(ally.transform.position - point) <= squared)
                    ally.Heal(health);
        }

        void SynergyDamageArea(Vector3 point, float radius, int damage)
        {
            float squared = radius * radius;
            foreach (var enemy in enemies.ToArray())
                if (IsCombatRunning && enemy && enemy.IsAlive && HorizontalSquared(enemy.transform.position - point) <= squared)
                    enemy.TakeDamage(Mathf.Max(1, damage));
        }

        void TryCrossfire(EnemyAgent target, TowerKind kind)
        {
            if (!Augments.Has(AugmentId.Crossfire) || !target || !target.IsAlive) return;
            if (!crossfireRecords.TryGetValue(target, out var previous) || previous.version != target.SpawnVersion)
                previous = new CrossfireRecord { version = target.SpawnVersion, kind = kind, lastHit = AssaultTime, readyAt = AssaultTime };
            bool trigger = previous.kind != kind && AssaultTime - previous.lastHit <= 3f && AssaultTime >= previous.readyAt;
            previous.kind = kind; previous.lastHit = AssaultTime;
            if (trigger) previous.readyAt = AssaultTime + 3f;
            crossfireRecords[target] = previous;
            if (!trigger) return;
            CrossfireBursts++; PlayPulse(target.transform.position + Vector3.up * .3f, .8f, new Color(1f, .9f, .35f));
            target.TakeDamage(12);
        }

        void OnAugmentNormalShot(EnemyAgent target, ShotSpec shot, Vector3 impact)
        {
            if (!IsCombatRunning || shot.isSecondary) return;
            if (shot.kind == TowerKind.Frost && Augments.Has(AugmentId.FrostBattery) && AssaultTime >= frostBatteryReadyAt)
            { frostBatteryReadyAt = AssaultTime + 4f; GrantAugmentMana(1); }
            if (shot.kind == TowerKind.Cannon && shot.source && Augments.Has(AugmentId.PowderKeg))
            {
                powderCounters.TryGetValue(shot.source, out int count); powderCounters[shot.source] = count + 1;
                if ((count + 1) % 4 == 0)
                {
                    PowderBursts++; PlayPulse(impact + Vector3.up * .3f, 1.8f, new Color(1f, .65f, .15f));
                    SynergyDamageArea(impact, 1.8f, Mathf.Max(1, Mathf.RoundToInt(shot.damage * .35f)));
                }
            }
            if (target && target.IsAlive) TryCrossfire(target, shot.kind);
        }

        bool QueueConvergence(EnemyAgent enemy, List<Vector3> bursts)
        {
            if (!Augments.Has(AugmentId.GrandConvergence) || enemy.VulnerabilityBonus <= 0f
                || AssaultTime < convergenceReadyAt) return false;
            if (convergenceCooldowns.TryGetValue(enemy, out var last) && last.version == enemy.SpawnVersion && last.until > AssaultTime) return false;
            convergenceCooldowns[enemy] = new TimedSpawn { version = enemy.SpawnVersion, until = AssaultTime + 8f };
            convergenceReadyAt = AssaultTime + 4f; bursts.Add(enemy.transform.position); return true;
        }

        void UpdateAugmentSynergies(float delta)
        {
            if (delta <= 0f) return;
            allyHealCountdown -= delta;
            if (allyHealCountdown > 0f) return;
            allyHealCountdown = 1f;
            if (!Augments.Has(AugmentId.HeroicGuard) && !Augments.Has(AugmentId.EternalLegion)) return;
            foreach (var ally in GetFriendlyUnits().ToArray())
            {
                if (!ally || !ally.IsAlive) continue;
                int healing = Augments.Has(AugmentId.EternalLegion) ? 4 : 0;
                if (ally.IsHero && ally.BlockedEnemyCount > 0 && Augments.Has(AugmentId.HeroicGuard)) healing += 5;
                if (healing > 0) ally.Heal(healing);
            }
        }

        void ClearAugmentSynergyRuntime()
        {
            crossfireRecords.Clear(); powderCounters.Clear(); convergenceCooldowns.Clear(); recentSpellKinds.Clear();
            frostBatteryReadyAt = relayReadyAt = reactorReadyAt = farewellReadyAt = phoenixReadyAt = 0f;
            convergenceReadyAt = cycleReadyAt = masterworksUntil = 0f; allyHealCountdown = 1f;
            economyWindow = -1; emberPayments = cinderPayments = steamPayments = 0;
        }

        void ResetAugmentSynergies()
        {
            ClearAugmentSynergyRuntime();
            CrossfireBursts = PowderBursts = ConvergenceBursts = SpellCyclesTriggered = 0;
            AugmentManaGranted = AugmentGoldGranted = AugmentHealingApplied = 0;
        }
    }
}
