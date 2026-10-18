using UnityEngine;
using TowerDefense.Audio;
using UnityEngine.SceneManagement;

namespace TowerDefense.Gameplay
{
    public sealed partial class LevelSession
    {
        float frostSpellCooldownRemaining;

        public bool FreeFireballReady => freeFireballCharges > 0;
        public bool CanCastFireball => IsCombatRunning && (FreeFireballReady || Mana >= FireballCost);
        public bool FrostSpellUnlocked => frostSpellUnlocked;
        public int FrostSpellCost => frostSpellCost;
        public float FrostSpellRadius => frostSpellRadius + (GetSpellLevel(SpellKind.FrostNova) - 1) * .5f;
        public float FrostSpellDuration => frostSpellDuration + GetSpellLevel(SpellKind.FrostNova) - 1;
        public float FrostSpellCooldownRemaining => frostSpellCooldownRemaining;
        public bool HasNextLevel => !string.IsNullOrWhiteSpace(nextLevelScene)
            && Application.CanStreamedLevelBeLoaded(nextLevelScene);

        public int GetUpgradeCost(TowerAgent tower)
        {
            if (!tower || tower.IsMaxLevel) return 0;
            if (Augments.RetrofitPending) return 0;
            int baseCost = tower.UpgradeLevel == 0 ? tower.upgradeCost : tower.secondUpgradeCost;
            return Mathf.Max(0, Mathf.CeilToInt(baseCost * (1f - Augments.UpgradeDiscount)));
        }

        public bool TryCastFrostNova(Vector3 groundPoint)
        {
            if (!ready || !FrostSpellUnlocked || Phase != SessionPhase.Combat || IsChoosingAugment || IsMenuOpen) return false;
            if (IsPaused) { Notify("Resume the assault to cast Frost Nova."); return false; }
            if (!IsSpellPointValid(groundPoint)) { Notify("Choose a point inside the map."); return false; }
            if (frostSpellCooldownRemaining > 0f) { Notify("Frost Nova is still recharging."); return false; }
            if (Mana < FrostSpellCost) { Notify("Need " + FrostSpellCost + " mana to cast Frost Nova."); return false; }

            float radiusSquared = FrostSpellRadius * FrostSpellRadius;
            foreach (var enemy in enemies)
            {
                if (!enemy || !enemy.IsAlive) continue;
                Vector3 offset = enemy.transform.position - groundPoint;
                if (offset.x * offset.x + offset.z * offset.z <= radiusSquared)
                    enemy.Freeze(FrostSpellDuration);
            }
            Mana -= FrostSpellCost;
            frostSpellCooldownRemaining = Mathf.Max(1f, frostSpellCooldown - GetSpellLevel(SpellKind.FrostNova) + 1);
            SpellsCast++;
            FrostCasts++;
            GameAudio.Play(SoundCue.FrostNova, groundPoint);
            OnAugmentSpellCast(SpellKind.FrostNova, groundPoint);
            Vector3 center = new Vector3(groundPoint.x, map.transform.position.y + .49f, groundPoint.z);
            CombatVisuals.For(this).FrostNova(center, FrostSpellRadius, GetSpellLevel(SpellKind.FrostNova));
            Notify("Frost Nova cast! Enemies frozen for " + FrostSpellDuration.ToString("0.#") + " seconds.");
            return true;
        }

        public bool TryGetRallyPoint(Vector3 position, out Vector3 rallyPoint)
        {
            rallyPoint = position;
            if (!map || map.routes == null || !Finite(position.x) || !Finite(position.y) || !Finite(position.z))
                return false;
            float closestSquared = float.PositiveInfinity;
            foreach (var route in map.routes)
            {
                if (!route || route.waypoints == null) continue;
                for (int i = 1; i < route.waypoints.Length; i++)
                {
                    if (!route.waypoints[i - 1] || !route.waypoints[i]) continue;
                    Vector3 start = route.waypoints[i - 1].position;
                    Vector3 end = route.waypoints[i].position;
                    Vector3 segment = end - start;
                    segment.y = 0f;
                    float lengthSquared = segment.sqrMagnitude;
                    Vector3 offset = position - start;
                    offset.y = 0f;
                    float t = lengthSquared > .0001f ? Mathf.Clamp01(Vector3.Dot(offset, segment) / lengthSquared) : 0f;
                    Vector3 candidate = Vector3.Lerp(start, end, t);
                    Vector3 distance = position - candidate;
                    float squared = distance.x * distance.x + distance.z * distance.z;
                    if (squared >= closestSquared) continue;
                    closestSquared = squared;
                    rallyPoint = candidate;
                }
            }
            if (float.IsPositiveInfinity(closestSquared)) return false;
            // Keep each recruit's lateral formation inside the widened road corridor.
            if (closestSquared <= 1.21f)
                rallyPoint = new Vector3(position.x, rallyPoint.y, position.z);
            return true;
        }

        public void ApplySoldierHit(SoldierAgent soldier, EnemyAgent target, int damage)
        {
            if (!IsCombatRunning || !soldier || !soldier.IsAlive || !target || !target.IsAlive || damage <= 0)
                return;
            if (!soldier.IsReinforcement && (!soldier.OwnerTower || soldier.OwnerTower.kind != TowerKind.Barracks)) return;
            int healthBefore = target.CurrentHealth;
            target.TakeDamage(ComputeTowerHitDamage(target, damage, TowerKind.Barracks, false, soldier.OwnerTower));
            OnAugmentSoldierHit(soldier, target, Mathf.Max(0, healthBefore - target.CurrentHealth));
        }

        public void LoadNextLevel()
        {
            if (Phase != SessionPhase.Victory || !HasNextLevel) return;
            Time.timeScale = 1f;
            SceneManager.LoadScene(nextLevelScene);
        }

        public void LoadFirstLevel()
        {
            if (!Application.CanStreamedLevelBeLoaded("Level01_ForestOutpost")) return;
            Time.timeScale = 1f;
            SceneManager.LoadScene("Level01_ForestOutpost");
        }
    }
}
