using TowerDefense.UI;
using UnityEngine;

namespace TowerDefense.Gameplay
{
    public sealed partial class LevelSession
    {
        // A safe numeric affordance for existing purchase checks. Cheat writes never change
        // the finite banks, so resources are inexhaustible and switching back restores them.
        const int UnlimitedResource = 1000000000;
        int goldBank, manaBank, healthBank;
        int easyGoldAllowanceGranted;
        GameDifficulty finiteHealthDifficulty = GameDifficulty.Hard;
        public GameDifficulty Difficulty { get; private set; } = GameDifficulty.Hard;
        public bool IsCheatMode => Difficulty == GameDifficulty.Cheat;

        bool IsEasy => Difficulty == GameDifficulty.Easy;
        int DifficultyHealth(EnemyProfile profile) => Mathf.Max(1, Mathf.RoundToInt(profile.health * (IsEasy ? .55f : 1f)));
        float DifficultySpeed(EnemyProfile profile) => profile.speed * (IsEasy ? .8f : 1f);
        int DifficultyMelee(EnemyProfile profile) => Mathf.Max(1, Mathf.RoundToInt(profile.meleeDamage * (IsEasy ? .65f : 1f)));
        int DifficultyReward(int reward) => Mathf.RoundToInt(reward * (IsEasy ? 1.25f : 1f));
        int DifficultyManaReward(int reward) => Mathf.CeilToInt(reward * (IsEasy ? 1.5f : 1f));
        int GetFiniteMaxHealth(GameDifficulty mode) => map
            ? Mathf.Max(1, map.baseHealth * (mode == GameDifficulty.Easy ? 2 : 1) - baseHealthPenalty) : 0;

        void ResetDifficultyEconomy()
        {
            Difficulty = GameSettings.Difficulty;
            finiteHealthDifficulty = IsEasy ? GameDifficulty.Easy : GameDifficulty.Hard;
            easyGoldAllowanceGranted = IsEasy ? Mathf.RoundToInt(map.initialGold * 1.8f) - map.initialGold : 0;
            goldBank = map.initialGold + easyGoldAllowanceGranted;
            manaBank = 0;
            healthBank = GetFiniteMaxHealth(finiteHealthDifficulty);
        }

        void ApplyDifficultyChange()
        {
            if (!ready || shuttingDown || Difficulty == GameSettings.Difficulty) return;
            Difficulty = GameSettings.Difficulty;
            if (!IsCheatMode)
            {
                float healthFraction = healthBank / (float)Mathf.Max(1, GetFiniteMaxHealth(finiteHealthDifficulty));
                healthBank = Mathf.Clamp(Mathf.RoundToInt(GetFiniteMaxHealth(Difficulty) * healthFraction), healthBank > 0 ? 1 : 0, GetFiniteMaxHealth(Difficulty));
                finiteHealthDifficulty = Difficulty;
                // Grant the easier opening budget once per run, even when selected mid-battle.
                if (IsEasy)
                {
                    int allowance = Mathf.RoundToInt(map.initialGold * 1.8f) - map.initialGold;
                    goldBank += Mathf.Max(0, allowance - easyGoldAllowanceGranted);
                    easyGoldAllowanceGranted = allowance;
                }
            }
            foreach (var enemy in enemies)
                if (enemy && enemy.IsAlive && enemyDefinitions.TryGetValue(enemy, out var profile))
                    enemy.ApplyDifficultyStats(DifficultyHealth(profile), DifficultySpeed(profile),
                        DifficultyReward(profile.reward), DifficultyMelee(profile));
            Changed?.Invoke();
        }
    }
}
