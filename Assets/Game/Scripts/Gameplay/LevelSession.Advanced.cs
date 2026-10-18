using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TowerDefense.Audio;

namespace TowerDefense.Gameplay
{
    public sealed partial class LevelSession
    {
        int baseHealthPenalty, freeFireballCharges;
        float furnaceTimer;
        bool secondWindUsed;
        readonly HashSet<TowerAgent> retrofittedTowers = new HashSet<TowerAgent>();
        readonly Dictionary<EnemyAgent, TimedSpawn> polarImmunity = new Dictionary<EnemyAgent, TimedSpawn>();
        readonly Dictionary<EnemyAgent, TimedSpawn> steamCooldowns = new Dictionary<EnemyAgent, TimedSpawn>();
        readonly List<DelayedEffect> delayedEffects = new List<DelayedEffect>();
        struct TimedSpawn { public int version; public float until; }
        sealed class DelayedEffect
        {
            public float remaining;
            public bool meteor;
            public Vector3 center;
            public float radius;
            public int damage;
            public TowerAgent source;
        }
        public int FrostCasts { get; private set; }
        public int PaidFireballs { get; private set; }
        public int FreeFireballs { get; private set; }
        public int ExtraMeteors { get; private set; }
        public int SteamBursts { get; private set; }
        public int PolarPulses { get; private set; }
        public bool SecondWindUsed => secondWindUsed;

        static float HorizontalSquared(Vector3 v) => v.x * v.x + v.z * v.z;

        void OnAugmentApplied(AugmentId id)
        {
            if (id == AugmentId.LastStandEngine)
            {
                baseHealthPenalty += 5;
                BaseHealth = Mathf.Max(1, BaseHealth - 5);
                Gold += 100;
                Mana += 24;
            }
            if (id == AugmentId.ManaFurnace) furnaceTimer = 15f;
            if (id == AugmentId.VeteranTraining || id == AugmentId.VanguardCommand || id == AugmentId.GuardianBond)
            {
                foreach (var tower in towers.Values)
                    if (tower && tower.Barracks) tower.Barracks.RefreshTroopStats();
                // Summoned elites may have different base stats. Retune by multiplier ratio,
                // preserving their model, tier, damage and current health fraction.
                float healthAdded = id == AugmentId.VanguardCommand ? .35f : id == AugmentId.GuardianBond ? .15f : .25f;
                float damageAdded = id == AugmentId.GuardianBond ? 0f : .25f;
                foreach (var ally in GetFriendlyUnits())
                    if (ally && ally.IsReinforcement)
                        ally.SetStats(Mathf.RoundToInt(ally.MaxHealth * Augments.SoldierHealthMultiplier / (Augments.SoldierHealthMultiplier - healthAdded)),
                            Mathf.RoundToInt(ally.Damage * Augments.SoldierDamageMultiplier / (Augments.SoldierDamageMultiplier - damageAdded)),
                            ally.EffectiveAttackInterval * GetBattleAttackSpeedMultiplier());
            }
        }

        bool TryApplyRetrofitUpgrade(TowerAgent tower)
        {
            if (!Augments.RetrofitPending || !tower || tower.IsMaxLevel) return false;
            while (!tower.IsMaxLevel) tower.ApplyUpgrade();
            Augments.ConsumeRetrofit();
            retrofittedTowers.Add(tower);
            return true;
        }

        public bool IsTowerIsolated(TowerAgent tower)
        {
            if (!tower) return false;
            foreach (var other in towers.Values)
                if (other && other != tower && HorizontalSquared(other.transform.position - tower.transform.position) < 100f)
                    return false;
            return true;
        }

        public float GetPrismaticRangeBonus(TowerAgent tower)
            => Augments.Has(AugmentId.LoneGuardian) && IsTowerIsolated(tower) ? 1f : 0f;

        float GetPrismaticTowerBonus(TowerAgent tower)
        {
            float bonus = Augments.Has(AugmentId.LastStandEngine) ? .25f : 0f;
            if (tower && retrofittedTowers.Contains(tower)) bonus += .25f;
            if (tower && Augments.Has(AugmentId.LoneGuardian) && IsTowerIsolated(tower)) bonus += .5f;
            return bonus;
        }

        bool CastFireballInternal(Vector3 groundPoint, bool manual)
        {
            if (!ready || Phase != SessionPhase.Combat || IsChoosingAugment || IsMenuOpen) return false;
            if (IsPaused) { Notify("Resume the assault to cast Fireball."); return false; }
            if (!IsSpellPointValid(groundPoint)) { Notify("Choose a point inside the map."); return false; }
            bool free = manual && freeFireballCharges > 0;
            if (manual && !free && Mana < FireballCost)
            { Notify("Need " + FireballCost + " mana to cast Fireball."); return false; }
            if (manual)
            {
                if (free) { freeFireballCharges--; FreeFireballs++; }
                else { Mana -= FireballCost; PaidFireballs++; }
                SpellsCast++;
                if (!free && Augments.Has(AugmentId.FireDividend)) Gold += 8;
                if (!free && Augments.Has(AugmentId.TwinMeteor))
                    delayedEffects.Add(new DelayedEffect { remaining = .75f, meteor = true, center = groundPoint });
                OnAugmentSpellCast(SpellKind.Fireball, groundPoint);
                GameAudio.Play(SoundCue.Fireball, groundPoint);
            }
            SpawnFireball(groundPoint);
            if (manual) Notify(free ? "Free Fireball cast!" : "Fireball cast! Burning ground for " + FireballDuration.ToString("0.#") + " seconds.");
            return true;
        }

        void SpawnFireball(Vector3 point)
        {
            if (!IsCombatRunning) return;
            var spell = fireballPool.Get();
            fireballs.Add(spell);
            spell.Play(new Vector3(point.x, map.transform.position.y + .49f, point.z), FireballRadius,
                FireballDuration, fireballTickInterval, fireballTickDamage + (GetSpellLevel(SpellKind.Fireball) - 1) * 4,
                ResolveFireballTick, ReturnFireball, GetSpellLevel(SpellKind.Fireball));
        }

        void UpdateAdvancedCombat(float delta)
        {
            if (delta <= 0f) return;
            UpdateThirdLevelCombat(delta);
            UpdateAugmentSynergies(delta);
            frostSpellCooldownRemaining = Mathf.Max(0f, frostSpellCooldownRemaining - delta);
            if (Augments.Has(AugmentId.ManaFurnace))
            {
                furnaceTimer -= delta;
                if (furnaceTimer <= 0f)
                { furnaceTimer = 15f; freeFireballCharges = 1; Changed?.Invoke(); }
            }
            if (delayedEffects.Count == 0) return;
            // Remove before callbacks: damage can end combat and clear the queue.
            var due = new List<DelayedEffect>();
            for (int i = delayedEffects.Count - 1; i >= 0; i--)
            {
                delayedEffects[i].remaining -= delta;
                if (delayedEffects[i].remaining > 0f) continue;
                due.Add(delayedEffects[i]); delayedEffects.RemoveAt(i);
            }
            foreach (var effect in due)
            {
                if (!IsCombatRunning) break;
                if (effect.meteor) { SpawnFireball(effect.center); ExtraMeteors++; }
                else
                {
                    PlayPulse(effect.center, effect.radius, new Color(1f, .6f, .2f));
                    DamageArea(effect.center, effect.radius, effect.damage, TowerKind.Cannon, false, false, effect.source);
                }
            }
        }

        void OnResolvedShot(EnemyAgent directTarget, ShotSpec shot, Vector3 impact)
        {
            if (!IsCombatRunning || shot.isSecondary) return;
            OnAugmentNormalShot(directTarget, shot, impact);
            if (!IsCombatRunning) return;
            if (shot.kind == TowerKind.Cannon && Augments.Has(AugmentId.Aftershock))
                delayedEffects.Add(new DelayedEffect { remaining = .6f, center = impact, radius = shot.splashRadius,
                    damage = Mathf.Max(1, Mathf.RoundToInt(shot.damage * .4f)), source = shot.source });
            if (shot.kind != TowerKind.Gun || !Augments.Has(AugmentId.PiercingRounds)) return;
            Vector3 direction = shot.direction;
            if (direction.sqrMagnitude < .01f) return;
            var targets = enemies.Where(e => e && e != directTarget && e.IsAlive)
                .Select(e => new { enemy = e, offset = e.transform.position - impact })
                .Where(e => Vector3.Dot(e.offset, direction) > 0f && Vector3.Dot(e.offset, direction) <= 7f
                    && HorizontalSquared(e.offset - direction * Vector3.Dot(e.offset, direction)) <= .64f)
                .OrderBy(e => Vector3.Dot(e.offset, direction)).Take(2).Select(e => e.enemy).ToArray();
            foreach (var enemy in targets)
                if (IsCombatRunning && enemy && enemy.IsAlive)
                    enemy.TakeDamage(ComputeTowerHitDamage(enemy, Mathf.Max(1, Mathf.RoundToInt(shot.damage * .4f)),
                        TowerKind.Gun, false, shot.source));
        }

        public void TryPolarPulse(TowerAgent tower)
        {
            if (!IsCombatRunning || !tower || !Augments.Has(AugmentId.PolarPulse)) return;
            bool hit = false;
            foreach (var enemy in enemies)
            {
                if (!enemy || !enemy.IsAlive || HorizontalSquared(enemy.transform.position - tower.transform.position)
                    > tower.EffectiveRange * tower.EffectiveRange) continue;
                if (polarImmunity.TryGetValue(enemy, out var last) && last.version == enemy.SpawnVersion && last.until > AssaultTime) continue;
                polarImmunity[enemy] = new TimedSpawn { version = enemy.SpawnVersion, until = AssaultTime + 6f };
                enemy.Freeze(1.5f); hit = true;
            }
            if (hit) { PolarPulses++; PlayPulse(tower.transform.position, tower.EffectiveRange, new Color(.2f, .8f, 1f)); }
        }

        void ApplyFireballDamage(Vector3 center, float radius, int damage)
        {
            float squared = radius * radius;
            var steam = new List<Vector3>();
            var convergence = new List<Vector3>();
            foreach (var enemy in enemies.ToArray())
            {
                if (!IsCombatRunning) break;
                if (!enemy || !enemy.IsAlive) continue;
                float distance = HorizontalSquared(enemy.transform.position - center);
                if (distance > squared) continue;
                bool frosted = enemy.IsFrosted || enemy.IsFrozen;
                if (frosted) QueueConvergence(enemy, convergence);
                if (frosted && (Augments.Has(AugmentId.ElementalBoil) || Augments.Has(AugmentId.SteamCircuit))
                    && (!steamCooldowns.TryGetValue(enemy, out var last) || last.version != enemy.SpawnVersion || last.until <= AssaultTime))
                {
                    steamCooldowns[enemy] = new TimedSpawn { version = enemy.SpawnVersion, until = AssaultTime + 5f };
                    steam.Add(enemy.transform.position);
                    enemy.ConsumeFrost();
                }
                int tickDamage = damage + (Augments.Has(AugmentId.FocusedFlame) && distance <= squared * .25f ? 2 : 0)
                    + (Augments.Has(AugmentId.ScorchingEdges) && enemy.DamageReduction > 0f ? 4 : 0);
                enemy.TakeDamage(AugmentSpellDamage(tickDamage, SpellKind.Fireball));
            }
            foreach (var point in steam)
            {
                if (!IsCombatRunning) break;
                SteamBursts++;
                float steamRadius = Augments.Has(AugmentId.ElementalBoil) ? 2.5f : 2f;
                int steamDamage = (Augments.Has(AugmentId.ElementalBoil) ? 30 : 0) + (Augments.Has(AugmentId.SteamCircuit) ? 24 : 0);
                PlayPulse(point + Vector3.up * .3f, steamRadius, new Color(.8f, .9f, 1f));
                SynergyDamageArea(point, steamRadius, steamDamage);
                RefreshEconomyWindow();
                if (Augments.Has(AugmentId.SteamCircuit) && steamPayments < 1)
                { steamPayments++; GrantAugmentMana(1); }
            }
            foreach (var point in convergence)
            {
                if (!IsCombatRunning) break;
                ConvergenceBursts++; PlayPulse(point + Vector3.up * .3f, 3f, new Color(.75f, .45f, 1f));
                SynergyDamageArea(point, 3f, 100);
            }
        }

        bool TrySecondWind()
        {
            if (secondWindUsed || !Augments.Has(AugmentId.SecondWind)) return false;
            secondWindUsed = true;
            BaseHealth = Mathf.Min(8, MaxBaseHealth);
            Vector3 core = map.routes[0].waypoints[map.routes[0].waypoints.Length - 1].position;
            foreach (var enemy in enemies.ToArray())
                if (enemy && HorizontalSquared(enemy.transform.position - core) <= 36f)
                {
                    enemies.Remove(enemy);
                    if (enemyOwners.TryGetValue(enemy, out var pool)) pool.Release(enemy);
                }
            PlayPulse(core, 6f, new Color(.4f, 1f, .9f));
            Notify("Second Wind saved the core! Nearby enemies were banished.");
            return true;
        }

        void ClearAdvancedCombat()
        {
            ClearThirdLevelCombat();
            ClearAugmentSynergyRuntime();
            delayedEffects.Clear(); polarImmunity.Clear(); steamCooldowns.Clear();
            foreach (var tower in towers.Values) if (tower && tower.Barracks) tower.Barracks.Shutdown();
        }

        void ResetAdvancedCombat()
        {
            ResetThirdLevelCombat();
            ResetAugmentSynergies();
            baseHealthPenalty = 0; freeFireballCharges = 0; furnaceTimer = 15f; secondWindUsed = false;
            retrofittedTowers.Clear(); delayedEffects.Clear(); polarImmunity.Clear(); steamCooldowns.Clear();
            FrostCasts = PaidFireballs = FreeFireballs = ExtraMeteors = SteamBursts = PolarPulses = 0;
        }
    }
}
