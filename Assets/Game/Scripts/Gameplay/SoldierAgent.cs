using System;
using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Audio;

namespace TowerDefense.Gameplay
{
    /// <summary>A barracks recruit with exclusive, versioned melee engagements.</summary>
    [DisallowMultipleComponent]
    public sealed class SoldierAgent : MonoBehaviour
    {
        public Animator animator;
        public Transform healthBar;
        public Transform healthFill;
        public float healthBarWidth = .8f;
        [Min(.1f)] public float moveSpeed = 3.3f;
        [Min(.2f)] public float attackReach = 1.25f;
        public bool IsAlive { get; private set; }
        public bool IsHero { get; private set; }
        public bool IsReinforcement { get; private set; }
        public int SpawnVersion { get; private set; }
        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public int Damage { get; private set; }
        public int VisualTier { get; private set; } = 1;
        public int HeroCleaveCount { get; private set; }
        public TowerAgent OwnerTower => owner != null ? owner.Tower : null;
        public int BlockCapacity => IsHero ? 2 : 1;
        public int BlockedEnemyCount { get { PruneEngagements(); return engagements.Count; } }
        public Vector3 RallyPosition { get; private set; }
        public float EffectiveAttackReach => attackReach + (session != null ? session.GetBattleRangeBonus() : 0f);
        public float EffectiveGuardRadius => guardRadius + (session != null ? session.GetBattleRangeBonus() : 0f);
        public float EffectiveAttackInterval => attackInterval
            / (session != null ? session.GetBattleAttackSpeedMultiplier() : 1f);

        struct Engagement { public EnemyAgent enemy; public int version; }
        readonly List<Engagement> engagements = new List<Engagement>(2);
        LevelSession session;
        BarracksAgent owner;
        Action<SoldierAgent> onDeath;
        EnemyAgent approaching;
        int approachingVersion;
        float attackInterval;
        float cooldown;
        float guardRadius;
        int animationState;
        int heroAttackCount;
        Transform eliteEquipment;
        readonly List<Renderer> eliteRenderers = new List<Renderer>(3);
        MaterialPropertyBlock eliteTint;
        Camera view;
        static readonly int Idle = Animator.StringToHash("Idle");
        static readonly int Move = Animator.StringToHash("Move");
        static readonly int Attack = Animator.StringToHash("Attack");

        public void Initialize(LevelSession levelSession, BarracksAgent barracks, Vector3 rally,
            bool hero, int health, int damage, float interval, float radius, Action<SoldierAgent> died)
        {
            DetachAll();
            SpawnVersion = unchecked(SpawnVersion + 1);
            session = levelSession;
            owner = barracks;
            RallyPosition = rally;
            IsHero = hero;
            IsReinforcement = false;
            heroAttackCount = 0;
            HeroCleaveCount = 0;
            guardRadius = Mathf.Max(1f, radius);
            MaxHealth = Mathf.Max(1, health);
            CurrentHealth = MaxHealth;
            Damage = Mathf.Max(1, damage);
            attackInterval = Mathf.Max(.2f, interval);
            cooldown = .15f;
            onDeath = died;
            IsAlive = true;
            view = Camera.main;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 1f;
                if (animator.runtimeAnimatorController != null && animator.isActiveAndEnabled)
                {
                    animator.Rebind();
                    animationState = 0;
                    Play(Idle);
                    animator.Update(0f);
                }
            }
            SetVisualTier(hero ? 3 : barracks != null ? barracks.Tier : 1);
        }

        public void InitializeReinforcement(LevelSession levelSession, Vector3 rally,
            int health, int damage, float interval, float radius, Action<SoldierAgent> died, bool hero = false)
        {
            Initialize(levelSession, null, rally, hero, health, damage, interval, radius, died);
            IsReinforcement = true;
        }

        public void SetVisualTier(int tier)
        {
            VisualTier = Mathf.Clamp(tier, 1, 3);
            // Distinct equipment supplements the existing troop/hero models without replacing their rig.
            if (VisualTier > 1 && !IsHero && eliteEquipment == null)
            {
                eliteEquipment = new GameObject("Elite Guard Equipment").transform;
                eliteEquipment.SetParent(transform, false);
                AddElitePlate("Left Pauldron", new Vector3(-.24f, .98f, 0f), new Vector3(.2f, .15f, .24f));
                AddElitePlate("Right Pauldron", new Vector3(.24f, .98f, 0f), new Vector3(.2f, .15f, .24f));
                AddElitePlate("Guard Shield", new Vector3(-.32f, .72f, .12f), new Vector3(.3f, .36f, .07f));
            }
            if (eliteEquipment != null) eliteEquipment.gameObject.SetActive(VisualTier > 1 && !IsHero);
            Color color = VisualTier >= 3 ? new Color(1f, .74f, .24f) : new Color(.44f, .78f, .94f);
            eliteTint ??= new MaterialPropertyBlock();
            eliteTint.Clear();
            eliteTint.SetColor("_BaseColor", color);
            eliteTint.SetColor("_Color", color);
            eliteTint.SetColor("_EmissionColor", color * .3f);
            foreach (Renderer renderer in eliteRenderers)
                if (renderer != null) renderer.SetPropertyBlock(eliteTint);
        }

        void AddElitePlate(string title, Vector3 position, Vector3 scale)
        {
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = title;
            plate.transform.SetParent(eliteEquipment, false);
            plate.transform.localPosition = position;
            plate.transform.localScale = scale;
            Collider collision = plate.GetComponent<Collider>();
            if (collision != null) { collision.enabled = false; Destroy(collision); }
            Renderer renderer = plate.GetComponent<Renderer>();
            var visuals = CombatVisuals.For(this);
            if (visuals != null) renderer.sharedMaterial = visuals.CoreMaterial;
            eliteRenderers.Add(renderer);
        }

        public void SetStats(int health, int damage, float interval)
        {
            // Upgrading or gaining a health augment preserves the current health fraction.
            float fraction = MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 1f;
            MaxHealth = Mathf.Max(1, health);
            CurrentHealth = IsAlive ? Mathf.Clamp(Mathf.RoundToInt(MaxHealth * fraction), 1, MaxHealth) : 0;
            Damage = Mathf.Max(1, damage);
            attackInterval = Mathf.Max(.2f, interval);
        }

        public bool TryAcceptEnemy(EnemyAgent enemy)
        {
            if (!IsAlive || enemy == null || !enemy.IsAlive || enemy.IsUnblockable) return false;
            PruneEngagements();
            for (int i = 0; i < engagements.Count; i++)
                if (engagements[i].enemy == enemy && engagements[i].version == enemy.SpawnVersion) return true;
            if (engagements.Count >= BlockCapacity) return false;
            engagements.Add(new Engagement { enemy = enemy, version = enemy.SpawnVersion });
            return true;
        }

        public void ReleaseEnemy(EnemyAgent enemy)
        {
            for (int i = engagements.Count - 1; i >= 0; i--)
                if (engagements[i].enemy == enemy) engagements.RemoveAt(i);
            if (approaching == enemy) { approaching = null; approachingVersion = 0; }
        }

        void Update()
        {
            if (!IsAlive || session == null || !session.IsCombatRunning || Time.deltaTime <= 0f) return;
            PruneEngagements();
            float attackSpeed = session.GetBattleAttackSpeedMultiplier();
            cooldown = Mathf.Max(0f, cooldown - Time.deltaTime * attackSpeed);
            if (animator != null) animator.speed = attackSpeed;
            AcquireNearbyBlockers();
            if (engagements.Count > 0)
            {
                EnemyAgent enemy = engagements[0].enemy;
                Face(enemy.transform.position - transform.position);
                Play(Attack);
                if (cooldown <= 0f)
                {
                    cooldown = attackInterval;
                    PerformAttack(enemy);
                }
                return;
            }

            if (!IsApproachValid()) FindApproach();
            if (IsApproachValid())
            {
                EnemyAgent enemy = approaching;
                float reach = EffectiveAttackReach;
                if (HorizontalSquared(enemy.transform.position - transform.position) <= reach * reach)
                {
                    if (enemy.TryAssignBlocker(this)) { Play(Attack); return; }
                    approaching = null;
                }
                else
                {
                    Vector3 destination = enemy.transform.position;
                    if (session.TryGetRallyPoint(destination, out Vector3 road)) destination = road;
                    MoveToward(destination);
                    return;
                }
            }
            if (HorizontalSquared(RallyPosition - transform.position) > .04f) MoveToward(RallyPosition);
            else Play(Idle);
        }

        void PerformAttack(EnemyAgent enemy)
        {
            if (session == null || !session.IsCombatRunning || !IsAlive || enemy == null || !enemy.IsAlive) return;
            Vector3 impact = enemy.transform.position;
            GameAudio.Play(SoundCue.SoldierHit, impact);
            session.ApplySoldierHit(this, enemy, Damage);
            // The killing hit can end the battle and deactivate this soldier immediately.
            if (!IsAlive || session == null || !session.IsCombatRunning) return;
            bool cleave = IsHero && ++heroAttackCount % 3 == 0;
            if (cleave)
            {
                HeroCleaveCount++;
                session.ApplyHeroCleave(this, enemy, impact, Mathf.Max(1, Mathf.RoundToInt(Damage * .6f)));
            }
            CombatVisuals.For(this)?.SoldierStrike(impact + Vector3.up * .45f, IsHero, VisualTier, cleave);
        }

        void AcquireNearbyBlockers()
        {
            if (engagements.Count >= BlockCapacity) return;
            var enemies = session.ActiveEnemies;
            float radius = EffectiveGuardRadius;
            float reach = EffectiveAttackReach;
            for (int i = 0; i < enemies.Count && engagements.Count < BlockCapacity; i++)
            {
                EnemyAgent enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || enemy.IsUnblockable || enemy.Blocker != null) continue;
                if (HorizontalSquared(enemy.transform.position - RallyPosition) > radius * radius) continue;
                if (HorizontalSquared(enemy.transform.position - transform.position) <= reach * reach)
                    enemy.TryAssignBlocker(this);
            }
        }

        bool IsApproachValid() => approaching != null && approaching.IsAlive
            && approaching.SpawnVersion == approachingVersion && !approaching.IsUnblockable
            && approaching.Blocker == null
            && HorizontalSquared(approaching.transform.position - RallyPosition) <= EffectiveGuardRadius * EffectiveGuardRadius;

        void FindApproach()
        {
            approaching = null;
            float best = float.PositiveInfinity;
            var enemies = session.ActiveEnemies;
            float radius = EffectiveGuardRadius;
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive || enemy.IsUnblockable || enemy.Blocker != null) continue;
                if (HorizontalSquared(enemy.transform.position - RallyPosition) > radius * radius) continue;
                float distance = HorizontalSquared(enemy.transform.position - transform.position);
                if (distance >= best) continue;
                best = distance;
                approaching = enemy;
                approachingVersion = enemy.SpawnVersion;
            }
        }

        void MoveToward(Vector3 target)
        {
            target.y = transform.position.y;
            Vector3 next = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            if (session.TryGetRallyPoint(next, out Vector3 road)) next = road;
            Face(next - transform.position);
            transform.position = next;
            Play(Move);
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive || damage <= 0) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            if (CurrentHealth > 0) return;
            IsAlive = false;
            DetachAll();
            if (session != null && session.IsCombatRunning) GameAudio.Play(SoundCue.SoldierDeath, transform.position);
            if (session != null && session.IsCombatRunning) session.OnAugmentSoldierDied(this);
            Action<SoldierAgent> callback = onDeath;
            onDeath = null;
            callback?.Invoke(this);
        }

        public void Heal(int health)
        {
            if (!IsAlive || health <= 0) return;
            int before = CurrentHealth;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + health);
            if (session != null && CurrentHealth > before)
                session.OnAugmentAllyHealed(this, CurrentHealth - before);
        }

        void PruneEngagements()
        {
            for (int i = engagements.Count - 1; i >= 0; i--)
            {
                Engagement held = engagements[i];
                if (held.enemy == null || !held.enemy.IsAlive || held.enemy.SpawnVersion != held.version
                    || held.enemy.Blocker != this) engagements.RemoveAt(i);
            }
        }

        void DetachAll()
        {
            while (engagements.Count > 0)
            {
                EnemyAgent enemy = engagements[engagements.Count - 1].enemy;
                engagements.RemoveAt(engagements.Count - 1);
                if (enemy != null) enemy.ReleaseBlocker(this);
            }
            approaching = null;
            approachingVersion = 0;
        }

        void Play(int state)
        {
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled
                || animationState == state || !animator.HasState(0, state)) return;
            animator.CrossFade(state, .08f, 0);
            animationState = state;
        }

        void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction);
        }

        static float HorizontalSquared(Vector3 value) => value.x * value.x + value.z * value.z;

        void LateUpdate()
        {
            if (healthBar != null && view != null) healthBar.rotation = view.transform.rotation;
            if (healthFill == null) return;
            float fraction = MaxHealth > 0 ? Mathf.Clamp01((float)CurrentHealth / MaxHealth) : 0f;
            healthFill.localScale = new Vector3(healthBarWidth * fraction, .07f, .025f);
            healthFill.localPosition = new Vector3(-healthBarWidth * (1f - fraction) * .5f, 0, -.025f);
        }

        void OnDisable()
        {
            IsAlive = false;
            CurrentHealth = 0;
            DetachAll();
            onDeath = null;
            session = null;
            owner = null;
            IsReinforcement = false;
            if (animator != null) animator.speed = 1f;
        }
    }
}
