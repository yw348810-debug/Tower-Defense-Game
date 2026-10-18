using TowerDefense.Maps;
using UnityEngine;

namespace TowerDefense.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class TowerAgent : MonoBehaviour
    {
        public Transform yawPivot;
        public Transform muzzle;
        public Transform upgradedMuzzle;
        public Transform finalMuzzle;
        public TowerKind kind;
        [Min(0)] public int upgradeCost = 60;
        [Min(0)] public int secondUpgradeCost = 80;
        public GameObject baseVisual;
        public GameObject upgradedVisual;
        public GameObject finalVisual;
        [Min(0.1f)] public float range = 5.5f;
        [Min(0.05f)] public float fireInterval = 0.55f;
        [Min(1)] public int damage = 10;
        [Min(0f)] public float turnSpeed = 540f;

        public BuildSlot Slot { get; private set; }
        public int UpgradeLevel { get; private set; }
        public int Level => UpgradeLevel + 1;
        public bool IsUpgraded => UpgradeLevel > 0;
        public bool IsMaxLevel => UpgradeLevel >= 2;
        public int UpgradeCost => IsMaxLevel ? 0 : session != null ? session.GetUpgradeCost(this)
            : Mathf.Max(0, UpgradeLevel == 0 ? upgradeCost : secondUpgradeCost);
        public float EffectiveRange => range + (session != null ? session.Augments.RangeBonus
            + session.GetPrismaticRangeBonus(this) + session.GetBattleRangeBonus() : 0f);
        public float EffectiveFireInterval => Mathf.Max(.05f, fireInterval
            / (session != null ? session.GetBattleAttackSpeedMultiplier() * session.AugmentAttackSpeedMultiplier(this) : 1f));
        public BarracksAgent Barracks { get; private set; }
        public SupportTowerAgent Support { get; private set; }
        public string DisplayName => kind switch
        {
            TowerKind.Frost => "Frost Tower",
            TowerKind.Cannon => "Blast Tower",
            TowerKind.Barracks => "Barracks",
            TowerKind.Support => "Sanctuary Tower",
            _ => "Rapidfire Tower"
        };

        private LevelSession session;
        private float cooldown;
        private bool statsCaptured;
        private float baseRange;
        private float baseFireInterval;
        private int baseDamage;
        private int rhythmShotCount;
        private float polarCooldown = 6f;

        public void Initialize(LevelSession levelSession, BuildSlot slot)
        {
            if (!statsCaptured)
            {
                baseRange = range;
                baseFireInterval = fireInterval;
                baseDamage = damage;
                statsCaptured = true;
            }
            range = baseRange;
            fireInterval = baseFireInterval;
            damage = baseDamage;
            UpgradeLevel = 0;
            SetUpgradeVisuals();
            session = levelSession;
            Slot = slot;
            cooldown = 0f;
            rhythmShotCount = 0;
            polarCooldown = 6f;
            if (yawPivot == null)
                yawPivot = transform;
            if (kind == TowerKind.Barracks)
            {
                Barracks = GetComponent<BarracksAgent>();
                if (Barracks != null) Barracks.Initialize(session, this);
            }
            else if (kind == TowerKind.Support)
            {
                Support = GetComponent<SupportTowerAgent>();
                if (Support != null) Support.Initialize(session, this);
            }
        }

        // Payment is performed by LevelSession before calling this method.
        public void ApplyUpgrade()
        {
            if (IsMaxLevel)
                return;

            if (!statsCaptured)
            {
                baseRange = range;
                baseFireInterval = fireInterval;
                baseDamage = damage;
                statsCaptured = true;
            }
            UpgradeLevel++;
            float damageMultiplier = UpgradeLevel == 1 ? 1.65f : 1.65f * 1.45f;
            float intervalMultiplier = UpgradeLevel == 1 ? .85f : .85f * .9f;
            damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
            range = baseRange + (UpgradeLevel == 1 ? .6f : 1.1f);
            if (kind == TowerKind.Support) range = UpgradeLevel == 1 ? 5.2f : 6f;
            fireInterval = Mathf.Max(.05f, baseFireInterval * intervalMultiplier);
            SetUpgradeVisuals();
            if (Barracks != null) Barracks.SetTier(Level);
            if (Support != null) Support.SetTier(Level);
        }

        public ShotSpec GetShotSpec()
        {
            ShotSpec shot = new ShotSpec
            {
                kind = kind,
                level = Level,
                damage = damage,
                sourcePosition = transform.position,
                source = this,
                color = new Color(1f, 0.72f, 0.18f)
            };
            if (kind == TowerKind.Frost)
            {
                shot.slowMultiplier = 0.6f;
                shot.slowDuration = 2.5f + (session != null ? session.Augments.FrostDurationBonus : 0f);
                shot.color = new Color(0.25f, 0.85f, 1f);
                if (IsMaxLevel)
                {
                    shot.splashRadius = 1.7f;
                    shot.splashDamageMultiplier = .65f;
                    shot.freezeDuration = 1f;
                }
            }
            else if (kind == TowerKind.Cannon)
            {
                shot.splashRadius = UpgradeLevel == 0 ? 2.4f : UpgradeLevel == 1 ? 2.8f : 3.2f;
                shot.splashRadius += session != null ? session.Augments.BlastRadiusBonus : 0f;
                shot.shatterRadius = UpgradeLevel == 0 ? 3f : UpgradeLevel == 1 ? 3.3f : 4f;
                shot.shatterDamage = UpgradeLevel == 0 ? 20 : UpgradeLevel == 1 ? 33 : 64;
                shot.color = new Color(1f, 0.32f, 0.16f);
            }
            else if (kind == TowerKind.Gun && IsMaxLevel)
            {
                shot.splashRadius = 1.25f;
                shot.splashDamageMultiplier = .45f;
                shot.color = new Color(1f, .88f, .38f);
            }
            return shot;
        }

        private void SetUpgradeVisuals()
        {
            GameObject selected = UpgradeLevel == 2 && finalVisual != null ? finalVisual
                : UpgradeLevel > 0 && upgradedVisual != null ? upgradedVisual : baseVisual;
            if (baseVisual != null) baseVisual.SetActive(baseVisual == selected);
            if (upgradedVisual != null) upgradedVisual.SetActive(upgradedVisual == selected);
            if (finalVisual != null) finalVisual.SetActive(finalVisual == selected);
        }

        private void Update()
        {
            if (session == null || !session.IsCombatRunning || kind == TowerKind.Barracks
                || kind == TowerKind.Support)
                return;

            if (kind == TowerKind.Frost && session.Augments.Has(AugmentId.PolarPulse))
            {
                polarCooldown -= Time.deltaTime;
                if (polarCooldown <= 0f) { polarCooldown = 6f; session.TryPolarPulse(this); }
            }

            cooldown = Mathf.Max(0f, cooldown - Time.deltaTime * session.GetBattleAttackSpeedMultiplier()
                * session.AugmentAttackSpeedMultiplier(this));
            EnemyAgent target = FindTarget();
            if (target == null)
                return;

            Vector3 direction = target.AimPosition - yawPivot.position;
            if (direction.x * direction.x + direction.z * direction.z > 0.0001f)
            {
                Vector3 angles = yawPivot.eulerAngles;
                float desiredYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                angles.y = Mathf.MoveTowardsAngle(angles.y, desiredYaw, turnSpeed * Time.deltaTime);
                yawPivot.rotation = Quaternion.Euler(angles);
            }

            if (cooldown > 0f)
                return;

            // One shot per update; a long frame cannot create a burst of overdue shots.
            cooldown = Mathf.Max(0.05f, fireInterval);
            Transform activeMuzzle = UpgradeLevel == 2 && finalMuzzle != null ? finalMuzzle
                : UpgradeLevel > 0 && upgradedMuzzle != null ? upgradedMuzzle : muzzle;
            Vector3 origin = activeMuzzle != null ? activeMuzzle.position : transform.position + Vector3.up;
            ShotSpec shot = GetShotSpec();
            session.FireProjectile(origin, target, shot);
            if (kind == TowerKind.Gun && session.Augments.GunEcho && ++rhythmShotCount >= 3)
            {
                rhythmShotCount = 0;
                // Only ordinary shots advance the rhythm; the echo cannot produce another echo.
                shot.damage = Mathf.Max(1, Mathf.RoundToInt(shot.damage * .6f));
                shot.isSecondary = true;
                session.FireProjectile(origin, target, shot);
            }
        }

        private EnemyAgent FindTarget()
        {
            EnemyAgent best = null;
            float closestToBase = float.PositiveInfinity;
            float effectiveRange = EffectiveRange;
            float rangeSquared = effectiveRange * effectiveRange;
            var enemies = session.ActiveEnemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive)
                    continue;

                Vector3 offset = enemy.transform.position - transform.position;
                if (offset.x * offset.x + offset.z * offset.z > rangeSquared)
                    continue;

                float remaining = enemy.RemainingDistance;
                if (remaining < closestToBase)
                {
                    closestToBase = remaining;
                    best = enemy;
                }
            }
            return best;
        }

        private void OnDisable()
        {
            session = null;
            Slot = null;
            cooldown = 0f;
            rhythmShotCount = 0;
            if (Barracks != null) Barracks.Shutdown();
            if (Support != null) Support.Shutdown();
        }
    }
}
