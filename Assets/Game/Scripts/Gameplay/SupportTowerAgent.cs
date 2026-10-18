using UnityEngine;

namespace TowerDefense.Gameplay
{
    /// <summary>Refreshes one vulnerability status and heals living allies in its aura.</summary>
    [DisallowMultipleComponent]
    public sealed class SupportTowerAgent : MonoBehaviour
    {
        public TowerAgent Tower { get; private set; }
        public int Tier { get; private set; } = 1;
        public float AuraRadius => Tower != null ? Tower.EffectiveRange
            : Tier >= 3 ? 6f : Tier == 2 ? 5.2f : 4.5f;
        public float VulnerabilityPercent => Tier >= 3 ? .38f : Tier == 2 ? .22f : .15f;
        public int HealingPerSecond => session != null
            ? session.AugmentSupportHealingBonus(Tier >= 3 ? 22 : Tier == 2 ? 10 : 6)
            : Tier >= 3 ? 22 : Tier == 2 ? 10 : 6;
        public float VulnerabilityDuration => session != null
            ? session.AugmentSupportDurationBonus(Tier >= 3 ? 3f : 2f) : Tier >= 3 ? 3f : 2f;

        LevelSession session;
        float auraCountdown;
        float healCountdown;
        float visualCountdown;

        public void Initialize(LevelSession levelSession, TowerAgent tower)
        {
            session = levelSession;
            Tower = tower;
            SetTier(tower != null ? tower.Level : 1);
            auraCountdown = 0f;
            healCountdown = 1f;
            visualCountdown = 0f;
        }

        public void SetTier(int tier) => Tier = Mathf.Clamp(tier, 1, 3);

        void Update() => Advance(Time.deltaTime);

        public void Advance(float delta)
        {
            if (session == null || Tower == null || !session.IsCombatRunning || delta <= 0f)
                return;

            auraCountdown -= delta;
            healCountdown -= delta;
            visualCountdown -= delta;
            if (auraCountdown > 0f && healCountdown > 0f) return;
            float radiusSquared = AuraRadius * AuraRadius;
            bool affected = false;

            if (auraCountdown <= 0f)
            {
                auraCountdown = .5f;
                var enemies = session.ActiveEnemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    EnemyAgent enemy = enemies[i];
                    if (enemy == null || !enemy.IsAlive
                        || HorizontalSquared(enemy.transform.position - transform.position) > radiusSquared) continue;
                    // Repeated auras refresh a single strongest effect instead of stacking damage multipliers.
                    enemy.ApplyVulnerability(VulnerabilityPercent, VulnerabilityDuration);
                    affected = true;
                }
            }

            if (healCountdown <= 0f)
            {
                healCountdown = 1f;
                foreach (SoldierAgent ally in session.GetFriendlyUnits())
                {
                    if (ally == null || !ally.IsAlive
                        || HorizontalSquared(ally.transform.position - transform.position) > radiusSquared) continue;
                    affected |= ally.CurrentHealth < ally.MaxHealth;
                    ally.Heal(HealingPerSecond);
                }
            }
            if (affected && visualCountdown <= 0f)
            {
                visualCountdown = 2f;
                CombatVisuals.For(this)?.SanctuaryPulse(transform.position, Tier);
            }
        }

        static float HorizontalSquared(Vector3 value) => value.x * value.x + value.z * value.z;

        public void Shutdown()
        {
            session = null;
            Tower = null;
            Tier = 1;
            auraCountdown = 0f;
            healCountdown = 1f;
            visualCountdown = 0f;
        }

        void OnDisable() => Shutdown();
    }
}
