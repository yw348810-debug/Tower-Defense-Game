using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TowerDefense.Audio;

namespace TowerDefense.Gameplay
{
    public enum SpellKind { Fireball, FrostNova, Reinforcements, Battlecry }

    public sealed partial class LevelSession
    {
        [Header("Third Level Spells")]
        public bool reinforcementsUnlocked;
        public bool battlecryUnlocked;
        public SoldierAgent reinforcementPrefab;
        readonly int[] spellLevels = { 1, 1, 1, 1 };
        readonly bool[] augmentRefreshed = new bool[3];
        readonly HashSet<AugmentId> choiceSeenCards = new HashSet<AugmentId>();
        readonly List<ReinforcementSquad> reinforcementSquads = new List<ReinforcementSquad>();
        float reinforcementCooldownRemaining, battlecryCooldownRemaining, battlecryRemaining;

        sealed class ReinforcementSquad
        {
            public float remaining;
            public readonly List<SoldierAgent> members = new List<SoldierAgent>();
        }
        public bool SpellUpgradeUnlocked => levelIndex >= 2;
        public bool AugmentRefreshUnlocked => levelIndex >= 1;
        public bool ReinforcementsUnlocked => levelIndex >= 3 && reinforcementsUnlocked;
        public bool BattlecryUnlocked => levelIndex >= 3 && battlecryUnlocked;
        public int ReinforcementCost => 20;
        public int BattlecryCost => 24;
        public float ReinforcementCooldownRemaining => reinforcementCooldownRemaining;
        public float BattlecryCooldownRemaining => battlecryCooldownRemaining;
        public float BattlecryRemaining => battlecryRemaining;
        public float ReinforcementDuration => 18f + (GetSpellLevel(SpellKind.Reinforcements) - 1) * 4f + AugmentReinforcementDurationBonus;
        public int ReinforcementCount => 2 + GetSpellLevel(SpellKind.Reinforcements);
        public int ReinforcementHeroCount => GetSpellLevel(SpellKind.Reinforcements) >= 3 ? 1 : 0;
        public float ReinforcementRadius => 3f + (GetSpellLevel(SpellKind.Reinforcements) - 1) * .5f;
        public float BattlecryDuration => 6f + GetSpellLevel(SpellKind.Battlecry) * 2f + AugmentBattlecryDurationBonus;
        public bool CanCastReinforcements => ReinforcementsUnlocked && IsCombatRunning && reinforcementPrefab
            && Mana >= ReinforcementCost && reinforcementCooldownRemaining <= 0f && reinforcementSquads.Count < 2;
        public bool CanCastBattlecry => BattlecryUnlocked && IsCombatRunning && Mana >= BattlecryCost
            && battlecryCooldownRemaining <= 0f;
        public int ReinforcementsCast { get; private set; }
        public int BattlecriesCast { get; private set; }
        public int SpellUpgradesPurchased { get; private set; }
        public int AugmentRefreshesUsed { get; private set; }
        public int BossesSpawned { get; private set; }
        public int BossesKilled { get; private set; }
        public int BossesLeaked { get; private set; }
        public int ActiveReinforcementCount => reinforcementSquads.Sum(g => g.members.Count(s => s && s.IsAlive));

        public int GetSpellLevel(SpellKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < spellLevels.Length && SpellUpgradeUnlocked ? spellLevels[index] : 1;
        }

        bool IsSpellUnlocked(SpellKind kind) => kind switch
        {
            SpellKind.Fireball => true,
            SpellKind.FrostNova => FrostSpellUnlocked,
            SpellKind.Reinforcements => ReinforcementsUnlocked,
            SpellKind.Battlecry => BattlecryUnlocked,
            _ => false
        };

        public int GetSpellUpgradeCost(SpellKind kind)
        {
            if (!SpellUpgradeUnlocked || !IsSpellUnlocked(kind) || GetSpellLevel(kind) >= 3) return 0;
            int level = GetSpellLevel(kind);
            return AugmentSpellUpgradeCost(kind switch
            {
                SpellKind.Fireball => level == 1 ? 80 : 140,
                SpellKind.FrostNova => level == 1 ? 90 : 150,
                SpellKind.Reinforcements => level == 1 ? 90 : 150,
                SpellKind.Battlecry => level == 1 ? 100 : 160,
                _ => 0
            });
        }

        public bool CanUpgradeSpell(SpellKind kind) => ready && SpellUpgradeUnlocked && IsSpellUnlocked(kind)
            && Phase != SessionPhase.Victory && Phase != SessionPhase.Defeat && !IsChoosingAugment && !IsMenuOpen
            && GetSpellLevel(kind) < 3 && Gold >= GetSpellUpgradeCost(kind);

        public bool TryUpgradeSpell(SpellKind kind)
        {
            if (!CanUpgradeSpell(kind)) return false;
            int cost = GetSpellUpgradeCost(kind);
            if (cost <= 0) return false;
            Gold -= cost;
            spellLevels[(int)kind]++;
            SpellUpgradesPurchased++;
            GameAudio.Play(SoundCue.Upgrade);
            OnAugmentSpellUpgraded(kind, cost);
            Notify(kind + " upgraded to level " + GetSpellLevel(kind) + ". " + GetSpellSummary(kind));
            return true;
        }

        public string GetSpellSummary(SpellKind kind)
        {
            int level = GetSpellLevel(kind);
            return kind switch
            {
                SpellKind.Fireball => $"{FireballDuration:0.#}s burn / R {FireballRadius:0.#} / {fireballTickDamage + (level - 1) * 4} per tick",
                SpellKind.FrostNova => $"{FrostSpellDuration:0.#}s freeze / R {FrostSpellRadius:0.#}",
                SpellKind.Reinforcements => level >= 3
                    ? $"1 champion + {ReinforcementCount - 1} veterans / {ReinforcementDuration:0}s / {ReinforcementHealth(level)} guard HP"
                    : $"{ReinforcementCount} {(level == 2 ? "veterans" : "recruits")} / {ReinforcementDuration:0}s / {ReinforcementHealth(level)} HP",
                SpellKind.Battlecry => $"{BattlecryDuration:0}s / DMG +{Mathf.RoundToInt((BattleDamageAt(level) - 1f) * 100)}% / SPEED +{Mathf.RoundToInt((BattleSpeedAt(level) - 1f) * 100)}% / R +{BattleRangeAt(level):0.#}",
                _ => ""
            };
        }

        static int ReinforcementHealth(int level) => level == 1 ? 120 : level == 2 ? 170 : 230;
        static int ReinforcementDamage(int level) => level == 1 ? 14 : level == 2 ? 20 : 28;
        static float BattleDamageAt(int level) => level == 1 ? 1.25f : level == 2 ? 1.35f : 1.5f;
        static float BattleSpeedAt(int level) => level == 1 ? 1.25f : level == 2 ? 1.4f : 1.55f;
        static float BattleRangeAt(int level) => level == 1 ? .8f : level == 2 ? 1.2f : 1.6f;
        public float GetBattleDamageMultiplier() => battlecryRemaining > 0f ? BattleDamageAt(GetSpellLevel(SpellKind.Battlecry)) : 1f;
        public float GetBattleAttackSpeedMultiplier() => battlecryRemaining > 0f ? BattleSpeedAt(GetSpellLevel(SpellKind.Battlecry)) : 1f;
        public float GetBattleRangeBonus() => battlecryRemaining > 0f ? BattleRangeAt(GetSpellLevel(SpellKind.Battlecry)) : 0f;

        public IEnumerable<SoldierAgent> GetFriendlyUnits()
        {
            foreach (var tower in towers.Values)
                if (tower && tower.Barracks)
                    foreach (var troop in tower.Barracks.Troops)
                        if (troop && troop.IsAlive) yield return troop;
            foreach (var squad in reinforcementSquads)
                foreach (var troop in squad.members)
                    if (troop && troop.IsAlive) yield return troop;
        }

        public bool TryCastReinforcements(Vector3 groundPoint)
        {
            if (!CanCastReinforcements || !IsSpellPointValid(groundPoint)
                || !TryGetRallyPoint(groundPoint, out var rally)) return false;
            var squad = new ReinforcementSquad { remaining = ReinforcementDuration };
            reinforcementSquads.Add(squad);
            int level = GetSpellLevel(SpellKind.Reinforcements);
            for (int i = 0; i < ReinforcementCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ReinforcementCount;
                Vector3 position = rally + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * .8f;
                if (TryGetRallyPoint(position, out var road)) position = road;
                bool champion = level >= 3 && i == 0;
                var barracks = GetTowerPrefab(TowerKind.Barracks)?.GetComponent<BarracksAgent>();
                var prefab = champion && barracks && barracks.heroPrefab ? barracks.heroPrefab : reinforcementPrefab;
                var troop = Instantiate(prefab, position, Quaternion.identity, spellRoot);
                troop.name = (champion ? "Reinforcement_Champion_" : level >= 2 ? "Reinforcement_Veteran_" : "Reinforcement_Recruit_") + (i + 1);
                squad.members.Add(troop);
                troop.InitializeReinforcement(this, position,
                    Mathf.RoundToInt((champion ? 420 : ReinforcementHealth(level)) * Augments.SoldierHealthMultiplier),
                    Mathf.RoundToInt((champion ? 44 : ReinforcementDamage(level)) * Augments.SoldierDamageMultiplier), champion ? .7f : .8f,
                    3f + (level - 1) * .5f, dead =>
                    {
                        squad.members.Remove(dead);
                        if (dead) { dead.gameObject.SetActive(false); Destroy(dead.gameObject); }
                    }, champion);
                troop.SetVisualTier(level);
                CombatVisuals.For(this).ReinforcementArrival(position, level);
            }
            Mana -= ReinforcementCost;
            reinforcementCooldownRemaining = 22f - level * 2f;
            ReinforcementsCast++; SpellsCast++;
            GameAudio.Play(SoundCue.Reinforcements, rally);
            OnAugmentSpellCast(SpellKind.Reinforcements, rally);
            PlayPulse(rally, 2f, new Color(.2f, 1f, .7f));
            Notify(ReinforcementCount + " reinforcements deployed to the nearby road for " + ReinforcementDuration + " seconds.");
            return true;
        }

        public bool TryCastBattlecry()
        {
            if (!CanCastBattlecry) return false;
            Mana -= BattlecryCost;
            battlecryRemaining = BattlecryDuration;
            int level = GetSpellLevel(SpellKind.Battlecry);
            battlecryCooldownRemaining = level == 1 ? 30f : level == 2 ? 28f : 25f;
            BattlecriesCast++; SpellsCast++;
            GameAudio.Play(SoundCue.Battlecry);
            OnAugmentSpellCast(SpellKind.Battlecry, map.baseAnchor.position);
            foreach (var tower in towers.Values)
                if (tower) CombatVisuals.For(this).BattlecryAura(tower.transform, level, BattlecryDuration);
            foreach (var troop in GetFriendlyUnits())
                if (troop) CombatVisuals.For(this).BattlecryAura(troop.transform, level, BattlecryDuration);
            Notify("Battlecry! All towers and allies gain damage, attack speed and range.");
            return true;
        }

        void UpdateThirdLevelCombat(float delta)
        {
            reinforcementCooldownRemaining = Mathf.Max(0f, reinforcementCooldownRemaining - delta);
            battlecryCooldownRemaining = Mathf.Max(0f, battlecryCooldownRemaining - delta);
            bool wasActive = battlecryRemaining > 0f;
            battlecryRemaining = Mathf.Max(0f, battlecryRemaining - delta);
            if (wasActive && battlecryRemaining <= 0f) Changed?.Invoke();
            for (int i = reinforcementSquads.Count - 1; i >= 0; i--)
            {
                var squad = reinforcementSquads[i];
                squad.remaining -= delta;
                if (squad.remaining > 0f && squad.members.Count > 0) continue;
                foreach (var troop in squad.members.ToArray())
                    if (troop) { troop.gameObject.SetActive(false); Destroy(troop.gameObject); }
                squad.members.Clear(); reinforcementSquads.RemoveAt(i);
            }
        }

        void ResetAugmentRefreshChoice()
        {
            Array.Clear(augmentRefreshed, 0, augmentRefreshed.Length);
            choiceSeenCards.Clear();
            foreach (var card in augmentOffers) choiceSeenCards.Add(card.Id);
        }

        List<AugmentDefinition> RefreshCandidates(int index)
        {
            if (!AugmentRefreshUnlocked || !IsChoosingAugment || IsMenuOpen || index < 0 || index >= augmentOffers.Count
                || augmentRefreshed[index]) return new List<AugmentDefinition>();
            bool needGeneric = !augmentOffers.Where((card, slot) => slot != index).Any(card => card.IsGeneric);
            var tier = augmentOffers[index].Tier;
            var eligible = AugmentCatalog.All.Where(card => card.MinimumLevel <= levelIndex && card.Tier == tier
                && !Augments.Has(card.Id) && augmentOffers.All(visible => visible.Id != card.Id) && IsAugmentEligible(card.Id)
                && (!needGeneric || card.IsGeneric)).ToList();
            var fresh = eligible.Where(card => !choiceSeenCards.Contains(card.Id)).ToList();
            // A very specialized full board can leave only four eligible cards. Reuse discarded
            // cards if necessary so every slot still has its promised refresh, without duplicate offers.
            return fresh.Count > 0 ? fresh : eligible;
        }

        public bool CanRefreshAugment(int index) => RefreshCandidates(index).Count > 0;

        public bool TryRefreshAugment(int index)
        {
            var candidates = RefreshCandidates(index);
            if (candidates.Count == 0) return false;
            var card = candidates[augmentRandom.Next(candidates.Count)];
            augmentOffers[index] = card; choiceSeenCards.Add(card.Id);
            augmentRefreshed[index] = true; AugmentRefreshesUsed++;
            GameAudio.Play(SoundCue.AugmentRefresh);
            Notify("Card refreshed. Each slot can be refreshed once during this choice.");
            return true;
        }

        void ClearThirdLevelCombat()
        {
            foreach (var squad in reinforcementSquads)
            {
                foreach (var troop in squad.members.ToArray())
                    if (troop) { troop.gameObject.SetActive(false); Destroy(troop.gameObject); }
                squad.members.Clear();
            }
            reinforcementSquads.Clear();
            battlecryRemaining = 0f;
        }

        void ResetThirdLevelCombat()
        {
            ClearThirdLevelCombat();
            for (int i = 0; i < spellLevels.Length; i++) spellLevels[i] = 1;
            Array.Clear(augmentRefreshed, 0, augmentRefreshed.Length); choiceSeenCards.Clear();
            reinforcementCooldownRemaining = battlecryCooldownRemaining = 0f;
            ReinforcementsCast = BattlecriesCast = SpellUpgradesPurchased = AugmentRefreshesUsed = 0;
            BossesSpawned = BossesKilled = BossesLeaked = 0;
        }
    }
}
