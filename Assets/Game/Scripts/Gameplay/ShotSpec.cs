using UnityEngine;

namespace TowerDefense.Gameplay
{
    public enum TowerKind
    {
        Gun,
        Frost,
        Cannon,
        Barracks,
        Support
    }

    public struct ShotSpec
    {
        public TowerKind kind;
        // Captured at launch so upgrading a tower cannot change an in-flight projectile.
        public int level;
        public int damage;
        public float splashRadius;
        // A zero value retains full damage for the existing Blast area attack.
        public float splashDamageMultiplier;
        public float slowMultiplier;
        public float slowDuration;
        public float freezeDuration;
        public float shatterRadius;
        public int shatterDamage;
        public bool isSecondary;
        public Vector3 sourcePosition;
        public TowerAgent source;
        public Vector3 direction;
        public Color color;
    }
}
