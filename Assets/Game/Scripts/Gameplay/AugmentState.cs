using System.Collections.Generic;

namespace TowerDefense.Gameplay
{
    /// <summary>Run-local modifiers and trigger counters. Shared tower prefabs are never mutated.</summary>
    public sealed class AugmentState
    {
        readonly HashSet<AugmentId> active = new HashSet<AugmentId>();
        readonly List<AugmentDefinition> selected = new List<AugmentDefinition>();
        public IReadOnlyList<AugmentDefinition> Selected { get; }
        public int SubsidizedBuildsRemaining { get; private set; }
        public bool CombinedArmsClaimed { get; private set; }
        int scavengerKills, medicKills, medicPayments;

        public AugmentState() => Selected = selected.AsReadOnly();
        public bool Has(AugmentId id) => active.Contains(id);
        public float RangeBonus => Has(AugmentId.LongRange) ? .5f : 0f;
        public float FrostDurationBonus => Has(AugmentId.LastingFrost) ? .75f : 0f;
        public float BlastRadiusBonus => Has(AugmentId.BlastCalibration) ? .25f : 0f;
        public float UpgradeDiscount => Has(AugmentId.FieldWorkshop) ? .2f : 0f;
        public float FireballDurationBonus => Has(AugmentId.ExtendedBurn) ? 1f : 0f;
        public float RespawnMultiplier => Has(AugmentId.FastReinforcements) ? .75f : 1f;
        public float SoldierHealthMultiplier => 1f + (Has(AugmentId.VeteranTraining) ? .25f : 0f)
            + (Has(AugmentId.VanguardCommand) ? .35f : 0f) + (Has(AugmentId.GuardianBond) ? .15f : 0f);
        public float SoldierDamageMultiplier => 1f + (Has(AugmentId.VeteranTraining) ? .25f : 0f)
            + (Has(AugmentId.VanguardCommand) ? .25f : 0f);
        public bool GunEcho => Has(AugmentId.GunRhythm);
        public int BuildDiscount => SubsidizedBuildsRemaining > 0 ? 15 : 0;
        public bool RetrofitPending { get; private set; }
        internal void ConsumeRetrofit() => RetrofitPending = false;

        internal bool Add(AugmentId id)
        {
            if (!active.Add(id)) return false;
            selected.Add(AugmentCatalog.Get(id));
            if (id == AugmentId.ConstructionGrant) SubsidizedBuildsRemaining = 3;
            if (id == AugmentId.UltimateRetrofit) RetrofitPending = true;
            return true;
        }

        internal void OnBuilt()
        {
            if (SubsidizedBuildsRemaining > 0) SubsidizedBuildsRemaining--;
        }

        internal bool ClaimCombinedArms()
        {
            if (!Has(AugmentId.CombinedArms) || CombinedArmsClaimed) return false;
            CombinedArmsClaimed = true;
            return true;
        }

        internal int OnKill(out int healing)
        {
            int mana = 0;
            healing = 0;
            if (Has(AugmentId.ManaScavenger) && ++scavengerKills % 8 == 0) mana = 1;
            if (Has(AugmentId.FieldMedic) && ++medicKills % 15 == 0 && medicPayments < 6)
            {
                medicPayments++;
                healing = 1;
            }
            return mana;
        }

        internal void Reset()
        {
            active.Clear(); selected.Clear(); SubsidizedBuildsRemaining = 0;
            CombinedArmsClaimed = false; scavengerKills = medicKills = medicPayments = 0;
            RetrofitPending = false;
        }
    }
}
