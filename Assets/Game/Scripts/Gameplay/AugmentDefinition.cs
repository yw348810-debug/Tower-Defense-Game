namespace TowerDefense.Gameplay
{
    public enum AugmentTier { Silver, Gold, Prismatic }

    public enum AugmentId
    {
        LuckyLoot, ConstructionGrant, ManaScavenger, LongRange, LastingFrost,
        BlastCalibration, EmergencyShield, StoredMana, FocusedFlame, CombinedArms,
        HeavyHunter, GunRhythm, FrostMark, ShatterResonance, ExtendedBurn,
        FireDividend, FieldWorkshop, TypeSpecialization, BurningMark, FieldMedic,
        VeteranTraining, FastReinforcements,
        UltimateRetrofit, PiercingRounds, PolarPulse, Aftershock, TwinMeteor,
        ManaFurnace, LoneGuardian, LastStandEngine, SecondWind, ElementalBoil, VanguardCommand,
        EmberLedger, FrostBattery, PowderKeg, Crossfire, HeroicGuard, SpellPrimer, PatientAim,
        SanctuaryRoots, ScorchingEdges, SteamCircuit, FinishingBlow, ArcaneInvestment, UpgradeRelay,
        WarDrums, GuardianBond, ExplosiveFarewell, ColdFront, CinderHarvest, SiegeFormation,
        GrandConvergence, Masterworks, ArchmageCycle, EternalLegion, SanctuaryReactor, Warpath,
        PhoenixPact, GlacialCataclysm, ArcaneSupremacy
    }

    /// <summary>Shared, immutable display data. Each run owns its effects in AugmentState.</summary>
    public sealed class AugmentDefinition
    {
        public AugmentId Id { get; }
        public AugmentTier Tier { get; }
        public string Name { get; }
        public string Description { get; }
        public bool IsGeneric { get; }
        public int MinimumLevel { get; }
        public string ChineseName { get; }
        public string ChineseDescription { get; }
        public string SynergyHint { get; }
        public string ChineseSynergyHint { get; }

        public AugmentDefinition(AugmentId id, AugmentTier tier, string name, string description, bool generic = false, int minimumLevel = 1,
            string chineseName = null, string chineseDescription = null, string synergyHint = null, string chineseSynergyHint = null)
        {
            Id = id; Tier = tier; Name = name; Description = description; IsGeneric = generic;
            MinimumLevel = minimumLevel;
            ChineseName = chineseName ?? name; ChineseDescription = chineseDescription ?? description;
            SynergyHint = synergyHint ?? ""; ChineseSynergyHint = chineseSynergyHint ?? "";
        }
    }
}
