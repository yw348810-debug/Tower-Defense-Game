using System;
using System.Collections.Generic;
using System.Linq;
using TowerDefense.Maps;
using UnityEngine;
using TowerDefense.Audio;
using UnityEngine.Pool;

namespace TowerDefense.Gameplay
{
    public enum SessionPhase { Preparation, Combat, Victory, Defeat }
    public enum EnemyKind { Normal, Fast, Heavy, Swarm, Breaker, Flying, Shaman, BossWarden, BossTitan }

    [Serializable]
    public sealed class EnemyProfile
    {
        public EnemyKind kind;
        public EnemyAgent prefab;
        [Min(1)] public int health = 36;
        [Min(.1f)] public float speed = 2.4f;
        [Min(0)] public int reward = 4;
        [Min(0)] public int manaReward = 1;
        [Min(1)] public int leakDamage = 1;
        [Range(0f, .9f)] public float damageReduction;
        [Min(1)] public int meleeDamage = 10;
        [Min(.2f)] public float meleeInterval = 1.1f;
        public bool unblockable;
        [Min(0f)] public float flightHeight;
        public bool boss;
        [Range(.1f, 1f)] public float controlDurationMultiplier = 1f;
        [Range(0f, 1f)] public float enrageHealthFraction = .4f;
    }

    [Serializable]
    public sealed class SpawnBatch
    {
        public string routeId = "A";
        public EnemyKind enemyKind;
        public bool spawnInPairs = true;
        [Min(0)] public float delay;
        [Min(1)] public int count = 8;
        [Min(.05f)] public float interval = 1.2f;
    }

    [Serializable]
    public sealed class WavePlan
    {
        public string label;
        [Min(0)] public float startTime;
        public bool majorWave;
        public SpawnBatch[] batches = Array.Empty<SpawnBatch>();
    }

    /// <summary>Owns the continuous assault timeline, economy and pooled combat objects.</summary>
    public sealed partial class LevelSession : MonoBehaviour
    {
        [Min(1)] public int levelIndex = 1;
        public string nextLevelScene;
        public LevelMap map;
        public EnemyAgent enemyPrefab;
        public TowerAgent towerPrefab;
        public ProjectileAgent projectilePrefab;
        public TowerAgent[] towerPrefabs = Array.Empty<TowerAgent>();
        public int[] towerCosts = { 60, 70, 90 };
        public EnemyProfile[] enemyProfiles = Array.Empty<EnemyProfile>();
        public WavePlan[] waves = Array.Empty<WavePlan>();
        [Header("Augments")]
        public bool enableAugments = true;
        [Tooltip("0 generates a new seed each run. A nonzero seed reproduces offers and loot rolls.")]
        public int augmentSeed;
        public int[] augmentChoiceWaveIndices = { 0, 3 };
        public AugmentTier[] augmentChoiceTiers = { AugmentTier.Silver, AugmentTier.Gold };
        [Header("Fireball Spell")]
        [Min(1)] public int fireballCost = 12;
        [Min(.1f)] public float fireballRadius = 3.2f;
        [Min(.1f)] public float fireballDuration = 3f;
        [Min(.05f)] public float fireballTickInterval = .5f;
        [Min(1)] public int fireballTickDamage = 10;
        public Rect spellBounds = new Rect(-21f, -13f, 42f, 26f);
        [Min(0)] public float laneOffset = .72f;
        [Header("Frost Spell")]
        public bool frostSpellUnlocked;
        [Min(1)] public int frostSpellCost = 18;
        [Min(.1f)] public float frostSpellRadius = 3.8f;
        [Min(.1f)] public float frostSpellDuration = 3.5f;
        [Min(.1f)] public float frostSpellCooldown = 10f;

        // Kept for the original scene setup and older serialized scene assets.
        [Min(1)] public int towerCost = 60;
        [Min(1)] public int enemyHealth = 36;
        [Min(.1f)] public float enemySpeed = 2.4f;
        [Min(0)] public int killReward = 4;
        [Min(1)] public int leakDamage = 1;

        public event Action Changed;
        public int Gold { get => IsCheatMode ? UnlimitedResource : goldBank; private set { if (!IsCheatMode) goldBank = Mathf.Max(0, value); } }
        public int Mana { get => IsCheatMode ? UnlimitedResource : manaBank; private set { if (!IsCheatMode) manaBank = Mathf.Max(0, value); } }
        public int FireballCost => fireballCost;
        public float FireballRadius => fireballRadius + (GetSpellLevel(SpellKind.Fireball) - 1) * .4f;
        public float FireballDuration => fireballDuration + Augments.FireballDurationBonus + (GetSpellLevel(SpellKind.Fireball) - 1) * .5f;
        public int BaseHealth { get => IsCheatMode ? UnlimitedResource : healthBank; private set { if (!IsCheatMode) healthBank = Mathf.Max(0, value); } }
        public int BaseShield { get; private set; }
        public int MaxBaseHealth => IsCheatMode ? UnlimitedResource : GetFiniteMaxHealth(Difficulty);
        public AugmentState Augments { get; } = new AugmentState();
        public bool IsChoosingAugment { get; private set; }
        public int AugmentChoiceNumber { get; private set; }
        public int RunAugmentSeed { get; private set; }
        public IReadOnlyList<AugmentDefinition> AugmentOffers => augmentOfferView ??= augmentOffers.AsReadOnly();
        public IReadOnlyList<AugmentDefinition> SelectedAugments => Augments.Selected;
        public int ActiveAugmentChoiceCount => enableAugments && augmentChoiceWaveIndices != null && augmentChoiceTiers != null
            ? Mathf.Min(augmentChoiceWaveIndices.Length, augmentChoiceTiers.Length) : 0;
        public int AvailableTowerCount => levelIndex >= 3 ? 5 : levelIndex >= 2 ? 4 : 3;
        public float MidpointAugmentTime => ActiveAugmentChoiceCount > 1 && augmentChoiceWaveIndices[1] < WaveCount
            ? waves[augmentChoiceWaveIndices[1]].startTime : -1f;
        public int WaveNumber { get; private set; }
        public int WaveCount => waves?.Length ?? 0;
        public int TowerCost => GetTowerCost(TowerKind.Gun);
        public SessionPhase Phase { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsCombatRunning => ready && Phase == SessionPhase.Combat && !IsPaused && !IsChoosingAugment && !IsMenuOpen;
        public string StatusMessage { get; private set; }
        public float GameSpeed { get; private set; } = 1f;
        public IReadOnlyList<EnemyAgent> ActiveEnemies => enemies;
        public int Kills { get; private set; }
        public int Leaks { get; private set; }
        public int ShotsFired { get; private set; }
        public int ShattersTriggered { get; private set; }
        public int UpgradesPurchased { get; private set; }
        public int SpellsCast { get; private set; }
        public int SpellDamageTicks { get; private set; }
        public int ActiveSpellCount => fireballs.Count;
        public int BuiltTowerCount => towers.Count;
        public int ActiveProjectileCount => projectiles.Count;
        public float AssaultTime { get; private set; }
        public float AssaultDuration { get; private set; } = 1f;
        public float AssaultProgress => Phase == SessionPhase.Victory ? 1f
            : Mathf.Clamp01(AssaultTime / Mathf.Max(1f, AssaultDuration));
        public float NextWaveIn => WaveNumber < WaveCount && waves[WaveNumber] != null
            ? Mathf.Max(0f, waves[WaveNumber].startTime - AssaultTime) : 0f;

        sealed class SpawnJob
        {
            public EnemyRoute route;
            public SpawnBatch batch;
            public EnemyProfile profile;
            public float startTime;
            public int spawned;
        }

        readonly List<EnemyAgent> enemies = new List<EnemyAgent>();
        readonly HashSet<ProjectileAgent> projectiles = new HashSet<ProjectileAgent>();
        readonly HashSet<CombatPulse> pulses = new HashSet<CombatPulse>();
        readonly HashSet<FireballArea> fireballs = new HashSet<FireballArea>();
        readonly Dictionary<BuildSlot, TowerAgent> towers = new Dictionary<BuildSlot, TowerAgent>();
        readonly List<SpawnJob> jobs = new List<SpawnJob>();
        readonly Dictionary<EnemyKind, EnemyProfile> profiles = new Dictionary<EnemyKind, EnemyProfile>();
        readonly Dictionary<EnemyKind, ObjectPool<EnemyAgent>> enemyPools = new Dictionary<EnemyKind, ObjectPool<EnemyAgent>>();
        readonly Dictionary<EnemyAgent, ObjectPool<EnemyAgent>> enemyOwners = new Dictionary<EnemyAgent, ObjectPool<EnemyAgent>>();
        readonly Dictionary<EnemyAgent, EnemyProfile> enemyDefinitions = new Dictionary<EnemyAgent, EnemyProfile>();
        readonly List<AugmentDefinition> augmentOffers = new List<AugmentDefinition>(3);
        IReadOnlyList<AugmentDefinition> augmentOfferView;
        readonly int[] towerTypeCounts = new int[5];
        System.Random augmentRandom, lootRandom;
        bool choiceWasPaused;
        int nextAugmentChoiceIndex;
        float choiceGameSpeed;
        SessionPhase choicePreviousPhase;
        ObjectPool<ProjectileAgent> projectilePool;
        ObjectPool<CombatPulse> pulsePool;
        ObjectPool<FireballArea> fireballPool;
        Transform enemyRoot, towerRoot, projectileRoot, impactRoot, spellRoot;
        bool ready, shuttingDown;

        void Awake() => InitializeBattle();

#if UNITY_EDITOR
        void OnEnable()
        {
            // Runtime pools and dictionaries cannot survive an Editor domain reload.
            if (!Application.isPlaying || enemyPools.Count != 0 || shuttingDown) return;
            if (GetConfigurationErrors().Count != 0) return;
            ready = false;
            foreach (string name in new[] { "Enemies", "Towers", "Projectiles", "Impacts", "Spells" })
            {
                var oldRoot = transform.Find(name);
                if (oldRoot) { oldRoot.gameObject.SetActive(false); Destroy(oldRoot.gameObject); }
            }
            enemies.Clear(); projectiles.Clear(); pulses.Clear(); fireballs.Clear(); towers.Clear(); jobs.Clear();
            profiles.Clear(); enemyOwners.Clear(); enemyDefinitions.Clear();
            InitializeBattle();
        }
#endif

        void InitializeBattle()
        {
            if (ready) return;
            var errors = GetConfigurationErrors();
            if (errors.Count != 0)
            {
                Phase = SessionPhase.Defeat;
                StatusMessage = "Level configuration is incomplete.";
                Debug.LogError(string.Join("\n", errors), this);
                return;
            }

            enemyRoot = CreateGroup("Enemies");
            towerRoot = CreateGroup("Towers");
            projectileRoot = CreateGroup("Projectiles");
            impactRoot = CreateGroup("Impacts");
            spellRoot = CreateGroup("Spells");
            profiles.Clear();
            if (enemyProfiles != null && enemyProfiles.Length > 0)
                foreach (var profile in enemyProfiles) profiles.Add(profile.kind, profile);
            else
                profiles.Add(EnemyKind.Normal, new EnemyProfile
                {
                    kind = EnemyKind.Normal, prefab = enemyPrefab, health = enemyHealth,
                    speed = enemySpeed, reward = killReward, leakDamage = leakDamage
                });

            foreach (var profile in profiles.Values)
            {
                ObjectPool<EnemyAgent> pool = null;
                pool = new ObjectPool<EnemyAgent>(
                    () =>
                    {
                        var enemy = Instantiate(profile.prefab, enemyRoot);
                        enemyOwners.Add(enemy, pool);
                        enemyDefinitions.Add(enemy, profile);
                        return enemy;
                    },
                    enemy => enemy.gameObject.SetActive(true),
                    enemy => enemy.gameObject.SetActive(false),
                    enemy => { enemyOwners.Remove(enemy); enemyDefinitions.Remove(enemy); if (enemy) Destroy(enemy.gameObject); },
                    true, 32, 256);
                enemyPools.Add(profile.kind, pool);
            }
            projectilePool = new ObjectPool<ProjectileAgent>(
                () => Instantiate(projectilePrefab, projectileRoot),
                projectile => projectile.gameObject.SetActive(true),
                projectile => projectile.gameObject.SetActive(false),
                projectile => { if (projectile) Destroy(projectile.gameObject); }, true, 32, 256);
            pulsePool = new ObjectPool<CombatPulse>(
                () =>
                {
                    var go = new GameObject("CombatPulse");
                    go.transform.SetParent(impactRoot, false);
                    return go.AddComponent<CombatPulse>();
                },
                pulse => pulse.gameObject.SetActive(true),
                pulse => pulse.gameObject.SetActive(false),
                pulse => { if (pulse) Destroy(pulse.gameObject); }, true, 16, 128);
            fireballPool = new ObjectPool<FireballArea>(
                () =>
                {
                    var go = new GameObject("Fireball");
                    go.transform.SetParent(spellRoot, false);
                    return go.AddComponent<FireballArea>();
                },
                spell => spell.gameObject.SetActive(true),
                spell => spell.gameObject.SetActive(false),
                spell => { if (spell) Destroy(spell.gameObject); }, true, 8, 32);
            ready = true;
            // Each scene owns its menu even after an Editor script reload clears static hooks.
            var menu = GetComponent<TowerDefense.UI.PauseMenu>();
            if (!menu) menu = gameObject.AddComponent<TowerDefense.UI.PauseMenu>();
            menu.session = this;
            TowerDefense.UI.GameSettings.Changed -= ApplyDifficultyChange;
            TowerDefense.UI.GameSettings.Changed += ApplyDifficultyChange;
            RestartLevel();
        }

        Transform CreateGroup(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }

        public List<string> GetConfigurationErrors()
        {
            var errors = map ? map.GetValidationErrors() : new List<string> { "Missing LevelMap." };
            if (!projectilePrefab) errors.Add("Missing projectile prefab.");
            if (fireballCost < 1 || !Positive(fireballRadius) || !Positive(fireballDuration) ||
                !Positive(fireballTickInterval) || fireballTickDamage < 1 ||
                !Positive(spellBounds.width) || !Positive(spellBounds.height) || !Finite(laneOffset) || laneOffset < 0)
                errors.Add("Invalid fireball or lane configuration.");
            if (!GetTowerPrefab(TowerKind.Gun)) errors.Add("Missing rapidfire tower prefab.");
            if (towerPrefabs != null && towerPrefabs.Length > 0)
                for (int i = 0; i < AvailableTowerCount; i++)
                {
                    if (i >= towerPrefabs.Length || !towerPrefabs[i]) errors.Add("Missing tower prefab: " + (TowerKind)i);
                    if (towerCosts == null || i >= towerCosts.Length || towerCosts[i] < 1) errors.Add("Invalid tower cost: " + (TowerKind)i);
                }
            else if (towerCost < 1) errors.Add("Invalid legacy tower cost.");
            if (levelIndex >= 3)
            {
                if (!reinforcementPrefab) errors.Add("Missing reinforcement soldier prefab.");
                var support = GetTowerPrefab(TowerKind.Support)?.GetComponent<SupportTowerAgent>();
                if (!support) errors.Add("Missing support aura tower component.");
            }
            if (levelIndex >= 2)
            {
                var barracks = GetTowerPrefab(TowerKind.Barracks)?.GetComponent<BarracksAgent>();
                if (!barracks || !barracks.soldierPrefab || !barracks.heroPrefab)
                    errors.Add("Barracks require separate soldier and hero prefabs.");
            }
            if (frostSpellUnlocked && (frostSpellCost < 1 || !Positive(frostSpellRadius)
                || !Positive(frostSpellDuration) || !Positive(frostSpellCooldown))) errors.Add("Invalid Frost Nova tuning.");
            if (enableAugments)
            {
                if (ActiveAugmentChoiceCount < 1 || augmentChoiceWaveIndices.Length != augmentChoiceTiers.Length)
                    errors.Add("Missing or mismatched augment choice schedule.");
                else for (int i = 0; i < ActiveAugmentChoiceCount; i++)
                    if (augmentChoiceWaveIndices[i] < 0 || augmentChoiceWaveIndices[i] >= WaveCount
                        || (i == 0 && augmentChoiceWaveIndices[i] != 0)
                        || (i > 0 && augmentChoiceWaveIndices[i] <= augmentChoiceWaveIndices[i - 1])
                        || !Enum.IsDefined(typeof(AugmentTier), augmentChoiceTiers[i])
                        || (levelIndex < 2 && augmentChoiceTiers[i] == AugmentTier.Prismatic))
                        errors.Add("Invalid augment choice schedule.");
            }

            var available = new HashSet<EnemyKind>();
            if (enemyProfiles != null && enemyProfiles.Length > 0)
                foreach (var profile in enemyProfiles)
                {
                    if (profile == null) { errors.Add("Missing enemy profile."); continue; }
                    if (!Enum.IsDefined(typeof(EnemyKind), profile.kind) || !available.Add(profile.kind))
                        errors.Add("Invalid or duplicate enemy kind: " + profile.kind);
                    if (!profile.prefab || profile.health < 1 || !Positive(profile.speed) || profile.reward < 0 || profile.manaReward < 0 ||
                        profile.leakDamage < 1 || !Finite(profile.damageReduction) || profile.damageReduction < 0 || profile.damageReduction > .9f)
                        errors.Add("Invalid enemy profile: " + profile.kind);
                    if (profile.boss && (!Positive(profile.controlDurationMultiplier) || profile.controlDurationMultiplier > 1f
                        || !Finite(profile.enrageHealthFraction) || profile.enrageHealthFraction < 0f || profile.enrageHealthFraction > 1f))
                        errors.Add("Invalid boss tuning: " + profile.kind);
                }
            else
            {
                available.Add(EnemyKind.Normal);
                if (!enemyPrefab || enemyHealth < 1 || !Positive(enemySpeed) || killReward < 0 || leakDamage < 1)
                    errors.Add("Invalid legacy enemy tuning or prefab.");
            }

            if (waves == null || waves.Length == 0) errors.Add("No waves configured.");
            else
            {
                float previousTime = -1f;
                foreach (var wave in waves)
                {
                    if (wave == null) { errors.Add("Missing wave."); continue; }
                    if (!Finite(wave.startTime) || wave.startTime < 0 || wave.startTime < previousTime)
                        errors.Add("Wave times must be finite, nonnegative and ordered.");
                    previousTime = wave.startTime;
                    if (wave.batches == null || wave.batches.Length == 0) { errors.Add("Empty wave."); continue; }
                    foreach (var batch in wave.batches)
                    {
                        if (batch == null || batch.count < 1 || !Positive(batch.interval) || !Finite(batch.delay) || batch.delay < 0)
                        { errors.Add("Invalid spawn batch."); continue; }
                        if (!available.Contains(batch.enemyKind)) errors.Add("Missing enemy kind: " + batch.enemyKind);
                        if (!map || map.routes == null || !map.routes.Any(route => route && route.routeId == batch.routeId))
                            errors.Add("Unknown route: " + batch.routeId);
                    }
                }
            }
            return errors;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Positive(float value) => Finite(value) && value > 0f;

        void BeginAugmentChoice(AugmentTier tier)
        {
            if (!ready || IsChoosingAugment || (tier == AugmentTier.Prismatic && levelIndex < 2)
                || SelectedAugments.Count >= ActiveAugmentChoiceCount) return;
            var eligible = AugmentCatalog.All.Where(card => card.MinimumLevel <= levelIndex && card.Tier == tier
                && !Augments.Has(card.Id) && IsAugmentEligible(card.Id)).ToList();
            var generic = eligible.Where(card => card.IsGeneric).ToList();
            if (eligible.Count < 3 || generic.Count == 0)
            {
                Debug.LogError("The augment pool must offer three eligible cards, including a general-purpose card.", this);
                return;
            }
            augmentOffers.Clear();
            var first = generic[augmentRandom.Next(generic.Count)];
            augmentOffers.Add(first); eligible.Remove(first);
            while (augmentOffers.Count < 3)
            {
                int index = augmentRandom.Next(eligible.Count);
                augmentOffers.Add(eligible[index]); eligible.RemoveAt(index);
            }
            for (int i = augmentOffers.Count - 1; i > 0; i--)
            {
                int index = augmentRandom.Next(i + 1);
                var swap = augmentOffers[i]; augmentOffers[i] = augmentOffers[index]; augmentOffers[index] = swap;
            }
            ResetAugmentRefreshChoice();
            choicePreviousPhase = Phase;
            choiceWasPaused = IsPaused;
            choiceGameSpeed = GameSpeed;
            AugmentChoiceNumber = SelectedAugments.Count + 1;
            IsChoosingAugment = true;
            Time.timeScale = 0f;
            Notify(tier == AugmentTier.Silver ? "Choose a Silver augment before the assault begins."
                : "Choose a " + tier + " augment to strengthen your defense.");
        }

        public bool TryChooseAugment(int offerIndex)
        {
            if (!ready || !IsChoosingAugment || IsMenuOpen || offerIndex < 0 || offerIndex >= augmentOffers.Count) return false;
            AugmentDefinition chosen = augmentOffers[offerIndex];
            ApplyAugment(chosen.Id);
            bool opening = choicePreviousPhase == SessionPhase.Preparation;
            nextAugmentChoiceIndex = SelectedAugments.Count;
            IsChoosingAugment = false;
            augmentOffers.Clear();
            Phase = SessionPhase.Combat;
            IsPaused = choiceWasPaused;
            GameSpeed = choiceGameSpeed;
            Time.timeScale = IsPaused ? 0f : GameSpeed;
            if (opening) StartDueWaves();
            Notify(chosen.Name + " activated. " + (opening ? "The assault has begun!" : "The assault continues!"));
            GameAudio.Play(SoundCue.AugmentPick);
            return true;
        }

        void ApplyAugment(AugmentId id)
        {
            if (!Augments.Add(id)) return;
            if (id == AugmentId.EmergencyShield) BaseShield += 3;
            if (id == AugmentId.StoredMana) Mana += 12;
            OnAugmentApplied(id);
            CheckCombinedArms();
            Changed?.Invoke();
        }

        void CheckCombinedArms()
        {
            if (towerTypeCounts[0] == 0 || towerTypeCounts[1] == 0 || towerTypeCounts[2] == 0 || !Augments.ClaimCombinedArms()) return;
            Gold += 30; Mana += 6;
        }

        bool IsAugmentEligible(AugmentId id)
        {
            if (!IsSynergyAugmentEligible(id)) return false;
            int emptySlots = map.buildSlots.Count(slot => slot && !HasTower(slot));
            bool CanOwn(TowerKind kind) => towerTypeCounts[(int)kind] > 0 || emptySlots > 0 && GetTowerPrefab(kind);
            bool CanOwnBoth(TowerKind a, TowerKind b)
            {
                int missing = (towerTypeCounts[(int)a] == 0 ? 1 : 0) + (towerTypeCounts[(int)b] == 0 ? 1 : 0);
                return CanOwn(a) && CanOwn(b) && emptySlots >= missing;
            }
            switch (id)
            {
                case AugmentId.ConstructionGrant: return emptySlots > 0;
                case AugmentId.LastingFrost: return CanOwn(TowerKind.Frost);
                case AugmentId.BlastCalibration: return CanOwn(TowerKind.Cannon);
                case AugmentId.CombinedArms:
                    return CanOwn(TowerKind.Gun) && CanOwn(TowerKind.Frost) && CanOwn(TowerKind.Cannon)
                        && emptySlots >= towerTypeCounts.Take(3).Count(count => count == 0);
                case AugmentId.VeteranTraining:
                case AugmentId.FastReinforcements:
                case AugmentId.VanguardCommand:
                    return levelIndex >= 2 && CanOwn(TowerKind.Barracks);
                case AugmentId.GunRhythm:
                case AugmentId.PiercingRounds: return CanOwn(TowerKind.Gun);
                case AugmentId.PolarPulse: return CanOwn(TowerKind.Frost);
                case AugmentId.Aftershock: return CanOwn(TowerKind.Cannon);
                case AugmentId.UltimateRetrofit: return emptySlots > 0 || towers.Values.Any(t => t && !t.IsMaxLevel);
                case AugmentId.LastStandEngine: return BaseHealth > 5 && MaxBaseHealth > 5;
                case AugmentId.LoneGuardian:
                    return towers.Values.Any(t => t && IsTowerIsolated(t)) || map.buildSlots.Any(slot => slot && !HasTower(slot)
                        && towers.Values.All(t => !t || HorizontalSquared(t.transform.position - slot.transform.position) >= 100f));
                case AugmentId.FrostMark: return CanOwnBoth(TowerKind.Gun, TowerKind.Frost);
                case AugmentId.ShatterResonance: return CanOwnBoth(TowerKind.Cannon, TowerKind.Frost);
                case AugmentId.FieldWorkshop: return emptySlots > 0 || towers.Values.Any(tower => tower && !tower.IsMaxLevel) || HasUpgradeableSpell();
                case AugmentId.TypeSpecialization:
                    for (int i = 0; i < towerTypeCounts.Length; i++)
                        if (GetTowerPrefab((TowerKind)i) && towerTypeCounts[i] + emptySlots >= 3) return true;
                    return false;
                case AugmentId.HeavyHunter:
                    return enemies.Any(enemy => enemy && enemy.IsAlive && (enemyDefinitions[enemy].kind == EnemyKind.Heavy
                            || enemyDefinitions[enemy].kind == EnemyKind.Breaker))
                        || jobs.Any(job => (job.profile.kind == EnemyKind.Heavy || job.profile.kind == EnemyKind.Breaker)
                            && job.spawned < job.batch.count);
                default: return true;
            }
        }

        void Update()
        {
            if (!IsCombatRunning) return;
            float nextTime = AssaultTime + Time.deltaTime;
            if (nextAugmentChoiceIndex < ActiveAugmentChoiceCount
                && augmentChoiceWaveIndices[nextAugmentChoiceIndex] < WaveCount
                && nextTime >= waves[augmentChoiceWaveIndices[nextAugmentChoiceIndex]].startTime)
            {
                AssaultTime = waves[augmentChoiceWaveIndices[nextAugmentChoiceIndex]].startTime;
                BeginAugmentChoice(augmentChoiceTiers[nextAugmentChoiceIndex]);
                return;
            }
            AssaultTime = nextTime;
            UpdateAdvancedCombat(Time.deltaTime);
            if (!IsCombatRunning) return;
            StartDueWaves();
            bool spawnedAny = false;
            foreach (var job in jobs)
                while (job.spawned < job.batch.count && AssaultTime >= job.startTime +
                    (job.batch.spawnInPairs ? job.spawned / 2 : job.spawned) * job.batch.interval)
                {
                    var enemy = enemyPools[job.profile.kind].Get();
                    enemy.DamageReduction = job.profile.damageReduction;
                    enemy.Initialize(job.route, DifficultyHealth(job.profile), DifficultySpeed(job.profile),
                        DifficultyReward(job.profile.reward), job.profile.leakDamage, ResolveEnemy,
                        job.spawned % 2 == 0 ? -laneOffset : laneOffset);
                    enemy.ConfigureMelee(this, DifficultyMelee(job.profile), job.profile.meleeInterval, job.profile.unblockable);
                    enemy.ConfigureBoss(job.profile.boss, job.profile.controlDurationMultiplier, job.profile.enrageHealthFraction);
                    if (job.profile.boss)
                    {
                        BossesSpawned++;
                        GameAudio.Play(SoundCue.BossArrival);
                        Notify("Boss approaching! Use support auras, reinforcements and upgraded spells.");
                    }
                    enemies.Add(enemy);
                    job.spawned++;
                    spawnedAny = true;
                }
            CheckAssaultFinished();
            if (spawnedAny) Changed?.Invoke();
        }

        void StartDueWaves()
        {
            while (WaveNumber < WaveCount && AssaultTime >= waves[WaveNumber].startTime)
            {
                var wave = waves[WaveNumber++];
                GameAudio.Play(SoundCue.WaveStart);
                Notify(wave.majorWave ? "A huge wave is approaching!" : "Wave " + WaveNumber + " incoming. Protect both entrances!");
            }
        }

        TowerAgent GetTowerPrefab(TowerKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= AvailableTowerCount) return null;
            if (towerPrefabs != null && index < towerPrefabs.Length && towerPrefabs[index]) return towerPrefabs[index];
            return kind == TowerKind.Gun ? towerPrefab : null;
        }

        public int GetTowerCost(TowerKind kind)
        {
            int index = (int)kind;
            if (index < 0 || index >= AvailableTowerCount) return int.MaxValue;
            int baseCost;
            if (towerPrefabs == null || towerPrefabs.Length == 0)
                baseCost = kind == TowerKind.Gun ? towerCost : int.MaxValue;
            else baseCost = towerCosts != null && index < towerCosts.Length ? towerCosts[index] : int.MaxValue;
            return baseCost == int.MaxValue ? baseCost : Mathf.Max(1, baseCost - Augments.BuildDiscount);
        }

        public TowerAgent GetTower(BuildSlot slot) => slot && towers.TryGetValue(slot, out var tower) ? tower : null;
        public bool HasTower(BuildSlot slot) => GetTower(slot) != null;
        bool CanUseSlot(BuildSlot slot) => ready && slot && map && map.buildSlots != null && map.buildSlots.Contains(slot) &&
            !IsChoosingAugment && !IsMenuOpen && Phase != SessionPhase.Victory && Phase != SessionPhase.Defeat;

        public bool TryBuildTower(BuildSlot slot) => TryBuildTower(slot, TowerKind.Gun);

        public bool TryBuildTower(BuildSlot slot, TowerKind kind)
        {
            if (!CanUseSlot(slot)) return false;
            if (HasTower(slot)) { Notify("This pad already has a tower. Upgrade it instead."); return false; }
            var prefab = GetTowerPrefab(kind);
            int cost = GetTowerCost(kind);
            if (!prefab || cost < 1) return false;
            if (kind == TowerKind.Barracks && (!prefab.GetComponent<BarracksAgent>()
                || !TryGetRallyPoint(slot.transform.position, out Vector3 rally)
                || HorizontalSquared(rally - slot.transform.position) > 49f))
            { Notify("Barracks need a nearby road to rally their troops."); return false; }
            if (Gold < cost) { Notify("Not enough gold. This tower costs " + cost + "."); return false; }
            var tower = Instantiate(prefab, slot.transform.position, Quaternion.identity, towerRoot);
            tower.name = kind + "_" + slot.slotId;
            tower.Initialize(this, slot);
            towers.Add(slot, tower);
            towerTypeCounts[(int)kind]++;
            Gold -= cost;
            Augments.OnBuilt();
            CheckCombinedArms();
            OnAugmentTowerBuilt(tower);
            GameAudio.Play(SoundCue.Build, slot.transform.position);
            Notify(tower.DisplayName + " built on " + slot.slotId + ".");
            return true;
        }

        public bool TryUpgradeTower(BuildSlot slot)
        {
            if (!CanUseSlot(slot)) return false;
            var tower = GetTower(slot);
            if (!tower) return false;
            if (tower.IsMaxLevel) { Notify("This tower is already fully upgraded."); return false; }
            int cost = tower.UpgradeCost;
            if (Gold < cost) { Notify("Not enough gold. This upgrade costs " + cost + "."); return false; }
            int previousLevel = tower.UpgradeLevel;
            if (!TryApplyRetrofitUpgrade(tower)) tower.ApplyUpgrade();
            if (tower.UpgradeLevel <= previousLevel || tower.UpgradeLevel > 2) return false;
            Gold -= cost;
            UpgradesPurchased += tower.UpgradeLevel - previousLevel;
            OnAugmentTowerUpgraded(tower, cost);
            GameAudio.Play(SoundCue.Upgrade, slot.transform.position);
            PlayPulse(slot.transform.position, 1.5f, new Color(1f, .8f, .2f));
            Notify(tower.kind == TowerKind.Barracks && tower.Barracks
                ? "Barracks upgraded to level " + tower.Level + ": " + tower.Barracks.RecruitSummary + "."
                : tower.DisplayName + " upgraded. Damage, range and firing speed increased.");
            return true;
        }

        public bool IsSpellPointValid(Vector3 position)
        {
            if (!map || !Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return false;
            Vector3 local = map.transform.InverseTransformPoint(position);
            return spellBounds.Contains(new Vector2(local.x, local.z));
        }

        public bool TryCastFireball(Vector3 groundPoint) => CastFireballInternal(groundPoint, true);

        void ResolveFireballTick(Vector3 center, float radius, int damage)
        {
            if (!IsCombatRunning) return;
            SpellDamageTicks++;
            ApplyFireballDamage(center, radius, damage);
        }

        void ReturnFireball(FireballArea spell)
        {
            if (!shuttingDown && fireballs.Remove(spell)) fireballPool.Release(spell);
        }

        // All waves run on the timeline; retained so older editor tools still compile.
        public bool StartNextWave() => false;

        public void FireProjectile(Vector3 origin, EnemyAgent target, int damage) => FireProjectile(origin, target,
            new ShotSpec { kind = TowerKind.Gun, damage = damage, color = new Color(1f, .72f, .18f) });

        public void FireProjectile(Vector3 origin, EnemyAgent target, ShotSpec shot)
        {
            if (!IsCombatRunning || !target || !target.IsAlive) return;
            shot.sourcePosition = origin;
            shot.direction = target.transform.position - origin;
            shot.direction.y = 0f;
            shot.direction.Normalize();
            var projectile = projectilePool.Get();
            projectiles.Add(projectile);
            projectile.Launch(target, origin, shot, ReturnProjectile, ResolveShot);
            GameAudio.Play(shot.kind == TowerKind.Frost ? SoundCue.FrostShot
                : shot.kind == TowerKind.Cannon ? SoundCue.CannonShot : SoundCue.GunShot, origin);
            ShotsFired++;
        }

        void ResolveShot(EnemyAgent target, ShotSpec shot)
        {
            if (!IsCombatRunning || !target || !target.IsAlive) return;
            Vector3 center = target.transform.position;
            GameAudio.Play(shot.kind == TowerKind.Cannon ? SoundCue.Explosion
                : shot.kind == TowerKind.Frost ? SoundCue.Shatter : SoundCue.ProjectileHit, center);
            if (shot.kind == TowerKind.Cannon)
            {
                // Only the direct target consumes frost. Area hits never trigger another shatter.
                bool shatter = target.ConsumeFrost();
                if (shatter)
                {
                    ShattersTriggered++;
                    GameAudio.Play(SoundCue.Shatter, center);
                }
                PlayPulse(center, shot.splashRadius, shot.color);
                DamageArea(center, shot.splashRadius, shot.damage, shot.kind, false, false, shot.source);
                if (shatter && Phase == SessionPhase.Combat)
                {
                    PlayPulse(center, shot.shatterRadius, new Color(.2f, .85f, 1f));
                    DamageArea(center, shot.shatterRadius, shot.shatterDamage, shot.kind, true, false, shot.source);
                }
            }
            else
            {
                // Capture the area before the direct hit: lethal damage can recycle the target.
                var areaTargets = shot.splashRadius > 0f ? enemies.ToArray() : null;
                ApplyProjectileHit(target, shot, shot.damage);
                if (areaTargets != null)
                {
                    int splashDamage = Mathf.Max(1, Mathf.RoundToInt(shot.damage
                        * (shot.splashDamageMultiplier > 0f ? shot.splashDamageMultiplier : 1f)));
                    float radiusSquared = shot.splashRadius * shot.splashRadius;
                    foreach (EnemyAgent enemy in areaTargets)
                    {
                        if (!IsCombatRunning) break;
                        if (!enemy || enemy == target || !enemy.IsAlive
                            || HorizontalSquared(enemy.transform.position - center) > radiusSquared) continue;
                        // Secondary area targets receive the same status, but cannot start another shot.
                        ApplyProjectileHit(enemy, shot, splashDamage);
                    }
                    PlayPulse(center, shot.splashRadius, shot.color);
                }
            }
            OnResolvedShot(target, shot, center);
        }

        void DamageArea(Vector3 center, float radius, int damage)
            => DamageArea(center, radius, damage, null, false, false);

        void DamageArea(Vector3 center, float radius, int damage, TowerKind? towerKind,
            bool shatter = false, bool fireball = false, TowerAgent source = null)
        {
            float squaredRadius = Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            // A damage callback may remove its target or finish the whole assault.
            foreach (var enemy in enemies.ToArray())
            {
                if (!enemy || !enemy.IsAlive) continue;
                Vector3 offset = enemy.transform.position - center;
                float squaredDistance = offset.x * offset.x + offset.z * offset.z;
                if (squaredDistance > squaredRadius) continue;
                int hitDamage = towerKind.HasValue
                    ? ComputeTowerHitDamage(enemy, damage, towerKind.Value, shatter, source) : damage;
                if (fireball && Augments.Has(AugmentId.FocusedFlame) && squaredDistance <= squaredRadius * .25f)
                    hitDamage += 2;
                enemy.TakeDamage(hitDamage);
            }
        }

        int GetTowerHitDamage(EnemyAgent target, int damage, TowerKind kind, bool shatter)
            => ComputeTowerHitDamage(target, damage, kind, shatter, null);

        int ComputeTowerHitDamage(EnemyAgent target, int damage, TowerKind kind, bool shatter, TowerAgent source)
        {
            // All damage modifiers share one additive budget, then EnemyAgent applies armor once.
            float bonus = GetPrismaticTowerBonus(source) + GetBattleDamageMultiplier() - 1f;
            if (Augments.Has(AugmentId.HeavyHunter) && enemyDefinitions.TryGetValue(target, out var profile)
                && (profile.kind == EnemyKind.Heavy || profile.kind == EnemyKind.Breaker))
                bonus += .3f;
            if (kind == TowerKind.Gun && (target.IsFrosted || target.IsFrozen) && Augments.Has(AugmentId.FrostMark)) bonus += .3f;
            if (shatter && Augments.Has(AugmentId.ShatterResonance)) bonus += .5f;
            int typeIndex = (int)kind;
            if (Augments.Has(AugmentId.TypeSpecialization) && typeIndex >= 0 && typeIndex < towerTypeCounts.Length && towerTypeCounts[typeIndex] >= 3)
                bonus += .25f;
            if (Augments.Has(AugmentId.BurningMark))
                foreach (var spell in fireballs)
                    if (spell && spell.ContainsBurningPoint(target.transform.position)) { bonus += .25f; break; }
            bonus += AugmentTowerDamageBonus(target, kind, source, shatter);
            return Mathf.Max(1, Mathf.RoundToInt(damage * (1f + bonus)));
        }

        void ReturnProjectile(ProjectileAgent projectile)
        {
            if (!shuttingDown && projectiles.Remove(projectile)) projectilePool.Release(projectile);
        }

        void PlayPulse(Vector3 center, float radius, Color color)
        {
            if (!ready || pulsePool == null || Phase == SessionPhase.Victory || Phase == SessionPhase.Defeat) return;
            var pulse = pulsePool.Get();
            pulses.Add(pulse);
            pulse.Play(center, radius, color, ReturnPulse);
        }

        void ReturnPulse(CombatPulse pulse)
        {
            if (!shuttingDown && pulses.Remove(pulse)) pulsePool.Release(pulse);
        }

        void ResolveEnemy(EnemyAgent enemy, bool killed)
        {
            if (shuttingDown || !enemies.Remove(enemy)) return;
            if (killed)
            {
                if (enemy.IsBoss) BossesKilled++;
                int reward = enemy.Reward;
                if (Augments.Has(AugmentId.LuckyLoot) && lootRandom.NextDouble() < .2)
                    reward += Mathf.RoundToInt(enemy.Reward * .5f);
                Gold += reward;
                Mana += DifficultyManaReward(enemyDefinitions[enemy].manaReward) + Augments.OnKill(out int healing);
                BaseHealth = Mathf.Min(MaxBaseHealth, BaseHealth + healing);
                Kills++;
                OnAugmentEnemyKilled(enemy);
            }
            else
            {
                if (enemy.IsBoss) BossesLeaked++;
                int blocked = Mathf.Min(BaseShield, enemy.LeakDamage);
                BaseShield -= blocked;
                BaseHealth = Mathf.Max(0, BaseHealth - (enemy.LeakDamage - blocked));
                Leaks++;
                GameAudio.Play(SoundCue.EnemyLeak);
                if (BaseHealth > 0 && BaseHealth <= Mathf.CeilToInt(MaxBaseHealth * .25f))
                    GameAudio.Play(SoundCue.BaseDanger);
            }
            if (enemyOwners.TryGetValue(enemy, out var pool)) pool.Release(enemy);
            if (BaseHealth == 0 && !TrySecondWind())
            {
                Phase = SessionPhase.Defeat;
                ClearCombat();
                RestoreSpeed();
                GameAudio.Play(SoundCue.Defeat);
                Notify("The core has fallen. Try a different placement and tower mix.");
                return;
            }
            CheckAssaultFinished();
            Changed?.Invoke();
        }

        void CheckAssaultFinished()
        {
            if (Phase != SessionPhase.Combat || WaveNumber < WaveCount || enemies.Count != 0 || jobs.Any(job => job.spawned < job.batch.count)) return;
            Phase = SessionPhase.Victory;
            ClearCombat();
            RestoreSpeed();
            GameAudio.Play(SoundCue.Victory);
            Notify("Assault cleared. The core is safe!");
        }

        public void TogglePause()
        {
            if (!ready || Phase != SessionPhase.Combat || IsChoosingAugment || IsMenuOpen) return;
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : GameSpeed;
            Changed?.Invoke();
        }

        public void ToggleSpeed()
        {
            if (!ready || Phase != SessionPhase.Combat || IsChoosingAugment || IsMenuOpen) return;
            GameSpeed = GameSpeed > 1f ? 1f : 2f;
            Time.timeScale = IsPaused ? 0f : GameSpeed;
            Changed?.Invoke();
        }

        void RestoreSpeed() { IsPaused = false; GameSpeed = 1f; Time.timeScale = 1f; }
        void Notify(string message) { StatusMessage = message; Changed?.Invoke(); }

        void ClearCombat()
        {
            GameAudio.StopCombatSounds();
            var visuals = GetComponent<CombatVisuals>();
            if (visuals) visuals.ClearBursts();
            ClearAdvancedCombat();
            jobs.Clear();
            foreach (var spell in fireballs.ToArray()) ReturnFireball(spell);
            foreach (var projectile in projectiles.ToArray()) ReturnProjectile(projectile);
            foreach (var pulse in pulses.ToArray()) ReturnPulse(pulse);
            foreach (var enemy in enemies.ToArray())
                if (enemy && enemyOwners.TryGetValue(enemy, out var pool)) pool.Release(enemy);
            enemies.Clear();
        }

        public void RestartLevel()
        {
            if (!ready) return;
            ResetSystemMenu();
            ResetAdvancedCombat();
            ResetTowerMastery();
            IsChoosingAugment = false;
            augmentOffers.Clear(); Augments.Reset();
            Array.Clear(towerTypeCounts, 0, towerTypeCounts.Length);
            nextAugmentChoiceIndex = 0; AugmentChoiceNumber = 0; BaseShield = 0;
            RunAugmentSeed = augmentSeed != 0 ? augmentSeed : Guid.NewGuid().GetHashCode();
            augmentRandom = new System.Random(RunAugmentSeed);
            // Combat kill counts cannot perturb the later offer roll.
            lootRandom = new System.Random(unchecked(RunAugmentSeed ^ 0x51F15EED));
            Phase = SessionPhase.Preparation;
            ClearCombat();
            foreach (var tower in towers.Values)
                if (tower) { tower.gameObject.SetActive(false); Destroy(tower.gameObject); }
            towers.Clear();
            ResetDifficultyEconomy(); WaveNumber = 0;
            Kills = 0; Leaks = 0; ShotsFired = 0; ShattersTriggered = 0; UpgradesPurchased = 0;
            SpellsCast = 0; SpellDamageTicks = 0;
            frostSpellCooldownRemaining = 0f;
            AssaultTime = 0f; AssaultDuration = 1f;
            RestoreSpeed();
            foreach (var wave in waves)
                foreach (var batch in wave.batches)
                {
                    float start = wave.startTime + batch.delay;
                    jobs.Add(new SpawnJob
                    {
                        route = map.routes.First(route => route.routeId == batch.routeId),
                        batch = batch, profile = profiles[batch.enemyKind], startTime = start
                    });
                    int lastGroup = batch.spawnInPairs ? (batch.count - 1) / 2 : batch.count - 1;
                    AssaultDuration = Mathf.Max(AssaultDuration, start + lastGroup * batch.interval + 1f);
                }
            if (ActiveAugmentChoiceCount > 0) BeginAugmentChoice(augmentChoiceTiers[0]);
            else
            {
                Phase = SessionPhase.Combat;
                StartDueWaves();
                Notify("The assault has begun. Build and upgrade while enemies approach.");
            }
        }

        void OnDestroy()
        {
            TowerDefense.UI.GameSettings.Changed -= ApplyDifficultyChange;
            shuttingDown = true;
            ready = false;
            Time.timeScale = 1f;
            // Children are destroyed with the session; callbacks cannot award teardown rewards.
            enemies.Clear(); projectiles.Clear(); pulses.Clear(); fireballs.Clear(); towers.Clear(); jobs.Clear();
            profiles.Clear(); enemyPools.Clear(); enemyOwners.Clear(); enemyDefinitions.Clear();
            Changed = null;
        }
    }
}
