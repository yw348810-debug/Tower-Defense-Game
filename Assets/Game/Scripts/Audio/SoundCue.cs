namespace TowerDefense.Audio
{
    /// <summary>Gameplay events, independent of clip names or AudioSource ownership.</summary>
    public enum SoundCue
    {
        UIClick, UIConfirm, UICancel,
        Build, Upgrade, AugmentRefresh, AugmentPick,
        GunShot, FrostShot, CannonShot, ProjectileHit, Explosion, Shatter,
        SoldierHit, SoldierDeath,
        Fireball, FrostNova, Reinforcements, Battlecry,
        WaveStart, EnemyLeak, BaseDanger, BossArrival, Victory, Defeat
    }
}
