using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Gameplay
{
    public sealed partial class LevelSession
    {
        struct TowerFreezeLock
        {
            public int version;
            public float until;
        }

        readonly Dictionary<EnemyAgent, TowerFreezeLock> towerFreezeLocks = new Dictionary<EnemyAgent, TowerFreezeLock>();
        float lastTowerFreezeTime;

        void ResetTowerMastery()
        {
            towerFreezeLocks.Clear();
            lastTowerFreezeTime = 0f;
        }

        void ApplyProjectileHit(EnemyAgent enemy, ShotSpec shot, int damage)
        {
            if (!IsCombatRunning || !enemy || !enemy.IsAlive) return;
            enemy.TakeDamage(ComputeTowerHitDamage(enemy, damage, shot.kind, false, shot.source));
            if (!enemy || !enemy.IsAlive || shot.kind != TowerKind.Frost) return;
            enemy.ApplyFrost(shot.slowMultiplier, shot.slowDuration);
            if (shot.freezeDuration <= 0f) return;

            // All max-tier frost towers share this lock. Several towers cannot hold a target forever.
            // Spawn versions also prevent a recycled enemy from inheriting its previous life's lock.
            if (AssaultTime < lastTowerFreezeTime) towerFreezeLocks.Clear();
            lastTowerFreezeTime = AssaultTime;
            if (towerFreezeLocks.TryGetValue(enemy, out TowerFreezeLock held)
                && held.version == enemy.SpawnVersion && held.until > AssaultTime) return;
            towerFreezeLocks[enemy] = new TowerFreezeLock
                { version = enemy.SpawnVersion, until = AssaultTime + 3f };
            enemy.Freeze(shot.freezeDuration);
        }

        public void ApplyHeroCleave(SoldierAgent hero, EnemyAgent directTarget, Vector3 center, int damage)
        {
            if (!IsCombatRunning || !hero || !hero.IsAlive || !hero.IsHero || damage <= 0) return;
            const float radiusSquared = 1.6f * 1.6f;
            // This is secondary damage, never another basic attack: no recursive hero or augment procs.
            foreach (EnemyAgent enemy in enemies.ToArray())
            {
                if (!IsCombatRunning) break;
                if (!enemy || !enemy.IsAlive || enemy == directTarget
                    || HorizontalSquared(enemy.transform.position - center) > radiusSquared) continue;
                enemy.TakeDamage(ComputeTowerHitDamage(enemy, damage, TowerKind.Barracks, false, hero.OwnerTower));
            }
        }
    }
}
