namespace TowerDefense.Audio
{
    /// <summary>Local mix levels and limits. Master gain is applied once by AudioListener.</summary>
    internal readonly struct SoundMix
    {
        internal readonly float Volume;
        internal readonly float Cooldown;
        internal readonly int Priority;
        internal readonly bool Combat;
        internal readonly bool VaryPitch;

        SoundMix(float volume, float cooldown, int priority, bool combat = false, bool varyPitch = false)
        {
            Volume = volume;
            Cooldown = cooldown;
            Priority = priority;
            Combat = combat;
            VaryPitch = varyPitch;
        }

        internal static SoundMix For(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.UIClick: return new SoundMix(.36f, .065f, 72);
                case SoundCue.UIConfirm: return new SoundMix(.43f, .10f, 24);
                case SoundCue.UICancel: return new SoundMix(.38f, .10f, 24);
                case SoundCue.Build: return new SoundMix(.52f, .10f, 24);
                case SoundCue.Upgrade: return new SoundMix(.54f, .12f, 20);
                case SoundCue.AugmentRefresh: return new SoundMix(.43f, .12f, 24);
                case SoundCue.AugmentPick: return new SoundMix(.53f, .14f, 16);
                case SoundCue.GunShot: return new SoundMix(.22f, .055f, 180, true, true);
                case SoundCue.FrostShot: return new SoundMix(.30f, .075f, 180, true, true);
                case SoundCue.CannonShot: return new SoundMix(.40f, .12f, 160, true, true);
                case SoundCue.ProjectileHit: return new SoundMix(.19f, .07f, 220, true, true);
                case SoundCue.Explosion: return new SoundMix(.39f, .13f, 140, true, true);
                case SoundCue.Shatter: return new SoundMix(.38f, .14f, 130, true, true);
                case SoundCue.SoldierHit: return new SoundMix(.26f, .09f, 200, true, true);
                case SoundCue.SoldierDeath: return new SoundMix(.33f, .18f, 150, true, true);
                case SoundCue.Fireball: return new SoundMix(.56f, .16f, 32, true);
                case SoundCue.FrostNova: return new SoundMix(.54f, .16f, 32, true);
                case SoundCue.Reinforcements: return new SoundMix(.53f, .16f, 32, true);
                case SoundCue.Battlecry: return new SoundMix(.55f, .16f, 32, true);
                case SoundCue.WaveStart: return new SoundMix(.43f, .50f, 12, true);
                case SoundCue.EnemyLeak: return new SoundMix(.44f, .20f, 36, true);
                case SoundCue.BaseDanger: return new SoundMix(.52f, 3f, 8, true);
                case SoundCue.BossArrival: return new SoundMix(.62f, 1f, 4, true);
                case SoundCue.Victory: return new SoundMix(.65f, .75f, 0);
                case SoundCue.Defeat: return new SoundMix(.60f, .75f, 0);
                default: return new SoundMix(.35f, .10f, 128, true);
            }
        }
    }
}
