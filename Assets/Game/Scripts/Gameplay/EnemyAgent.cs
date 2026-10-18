using System;
using System.Collections.Generic;
using TowerDefense.Maps;
using UnityEngine;

namespace TowerDefense.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class EnemyAgent : MonoBehaviour
    {
        public Animator animator;
        public Vector3 aimOffset = new Vector3(0f, 0.7f, 0f);
        [Range(0f, 0.9f)] public float DamageReduction;

        public bool IsAlive { get; private set; }
        public int SpawnVersion { get; private set; }
        public int CurrentHealth { get; private set; }
        public int MaxHealth { get; private set; }
        public int Reward { get; private set; }
        public int LeakDamage { get; private set; }
        public float LateralOffset { get; private set; }
        public bool IsFrosted => IsAlive && frostRemaining > 0f;
        public bool IsFrozen => IsAlive && freezeRemaining > 0f;
        public bool WasFrozenWhenKilled { get; private set; }
        public bool WasFrostedWhenKilled { get; private set; }
        public bool WasBlockedWhenKilled { get; private set; }
        public float VulnerabilityWhenKilled { get; private set; }
        public bool IsUnblockable { get; private set; }
        public bool IsBoss { get; private set; }
        public bool IsEnraged { get; private set; }
        public float VulnerabilityBonus => IsAlive && vulnerabilityRemaining > 0f ? vulnerabilityBonus : 0f;
        public SoldierAgent Blocker { get; private set; }
        public Vector3 AimPosition => transform.TransformPoint(aimOffset);
        public float RemainingDistance => IsAlive && nextWaypoint < points.Length
            ? Vector3.Distance(transform.position, points[nextWaypoint]) + distanceAfterPoint[nextWaypoint]
            : 0f;

        private static readonly int MoveState = Animator.StringToHash("Move");
        private static readonly int AttackState = Animator.StringToHash("Attack");
        private Vector3[] points = Array.Empty<Vector3>();
        private float[] distanceAfterPoint = Array.Empty<float>();
        private int nextWaypoint;
        private float moveSpeed;
        private Action<EnemyAgent, bool> onResolved;
        private float frostRemaining;
        private float frostMultiplier = 1f;
        private float freezeRemaining;
        private float vulnerabilityBonus;
        private float vulnerabilityRemaining;
        private struct VulnerabilityStatus { public float bonus; public float remaining; }
        private readonly List<VulnerabilityStatus> vulnerabilityStatuses = new List<VulnerabilityStatus>(3);
        private float controlDurationMultiplier = 1f;
        private float rageHealthThreshold = .4f;
        private LevelSession session;
        private int meleeDamage = 8;
        private float meleeInterval = 1.1f;
        private float meleeCooldown;
        private int blockerVersion;
        private int currentAnimation;
        private List<RendererTint> rendererTints;
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private sealed class RendererTint
        {
            public Renderer renderer;
            public int materialIndex;
            public int colorProperty;
            public Color originalColor;
            public MaterialPropertyBlock originalBlock;
            public MaterialPropertyBlock frostBlock;
        }

        /// <summary>Retunes a living pooled enemy without restarting its route, effects or melee state.</summary>
        public void ApplyDifficultyStats(int health, float speed, int reward, int damage)
        {
            float fraction = MaxHealth > 0 ? CurrentHealth / (float)MaxHealth : 1f;
            MaxHealth = Mathf.Max(1, health);
            CurrentHealth = IsAlive ? Mathf.Clamp(Mathf.RoundToInt(MaxHealth * fraction), 1, MaxHealth) : 0;
            moveSpeed = Mathf.Max(0f, speed);
            Reward = Mathf.Max(0, reward);
            meleeDamage = Mathf.Max(1, damage);
        }

        public void Initialize(EnemyRoute route, int health, float speed, int reward,
            int leakDamage, Action<EnemyAgent, bool> resolved, float lateralOffset = 0f)
        {
            if (route == null || route.waypoints == null || route.waypoints.Length < 2)
                throw new ArgumentException("An enemy requires a route with at least two waypoints.", nameof(route));
            for (int i = 0; i < route.waypoints.Length; i++)
            {
                if (route.waypoints[i] == null)
                    throw new ArgumentException("Enemy route contains a missing waypoint.", nameof(route));
            }

            if (points.Length != route.waypoints.Length)
            {
                points = new Vector3[route.waypoints.Length];
                distanceAfterPoint = new float[points.Length];
            }
            LateralOffset = float.IsNaN(lateralOffset) || float.IsInfinity(lateralOffset) ? 0f : lateralOffset;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 center = route.waypoints[i].position;
                if (Mathf.Abs(LateralOffset) < .0001f) { points[i] = center; continue; }
                Vector3 incoming = HorizontalDirection(route, i, -1);
                Vector3 outgoing = HorizontalDirection(route, i, 1);
                if (incoming.sqrMagnitude < .0001f) incoming = outgoing;
                if (outgoing.sqrMagnitude < .0001f) outgoing = incoming;
                Vector3 rightIn = Vector3.Cross(Vector3.up, incoming);
                Vector3 rightOut = Vector3.Cross(Vector3.up, outgoing);
                Vector3 miter = rightIn + rightOut;
                if (miter.sqrMagnitude < .0001f)
                    points[i] = center + rightOut * LateralOffset;
                else
                {
                    miter.Normalize();
                    float denominator = Vector3.Dot(miter, rightOut);
                    float length = denominator > .001f ? LateralOffset / denominator : LateralOffset;
                    // Limit sharp bends so an outer lane cannot leave the road corridor.
                    length = Mathf.Clamp(length, -Mathf.Abs(LateralOffset) * 2f, Mathf.Abs(LateralOffset) * 2f);
                    points[i] = center + miter * length;
                }
            }
            distanceAfterPoint[points.Length - 1] = 0f;
            for (int i = points.Length - 2; i >= 0; i--)
                distanceAfterPoint[i] = distanceAfterPoint[i + 1] + Vector3.Distance(points[i], points[i + 1]);

            SpawnVersion = unchecked(SpawnVersion + 1);
            DetachBlocker();
            session = null;
            IsUnblockable = false;
            IsBoss = false;
            IsEnraged = false;
            WasFrozenWhenKilled = WasFrostedWhenKilled = WasBlockedWhenKilled = false;
            VulnerabilityWhenKilled = 0f;
            controlDurationMultiplier = 1f;
            rageHealthThreshold = .4f;
            vulnerabilityBonus = 0f;
            vulnerabilityRemaining = 0f;
            vulnerabilityStatuses.Clear();
            meleeDamage = 8;
            meleeInterval = 1.1f;
            meleeCooldown = 0f;
            freezeRemaining = 0f;
            ResetFrost();
            MaxHealth = Mathf.Max(1, health);
            CurrentHealth = MaxHealth;
            Reward = Mathf.Max(0, reward);
            LeakDamage = Mathf.Max(0, leakDamage);
            moveSpeed = Mathf.Max(0f, speed);
            onResolved = resolved;
            nextWaypoint = 1;
            transform.position = points[0];
            FaceDirection(points[1] - points[0]);
            IsAlive = true;

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 1f;
                if (animator.runtimeAnimatorController != null && animator.isActiveAndEnabled)
                {
                    animator.Rebind();
                    if (animator.HasState(0, MoveState))
                        animator.Play(MoveState, 0, 0f);
                    animator.Update(0f);
                    currentAnimation = MoveState;
                }
            }
        }

        private static Vector3 HorizontalDirection(EnemyRoute route, int at, int step)
        {
            Vector3 origin = route.waypoints[at].position;
            for (int i = at + step; i >= 0 && i < route.waypoints.Length; i += step)
            {
                Vector3 direction = step > 0 ? route.waypoints[i].position - origin : origin - route.waypoints[i].position;
                direction.y = 0f;
                if (direction.sqrMagnitude > .0001f) return direction.normalized;
            }
            return Vector3.zero;
        }

        private void Update()
        {
            if (!IsAlive || Time.deltaTime <= 0f || Time.timeScale <= 0f
                || session != null && !session.IsCombatRunning)
                return;

            if (vulnerabilityRemaining > 0f)
            {
                float previousBonus = vulnerabilityBonus;
                vulnerabilityBonus = 0f;
                vulnerabilityRemaining = 0f;
                for (int i = vulnerabilityStatuses.Count - 1; i >= 0; i--)
                {
                    VulnerabilityStatus status = vulnerabilityStatuses[i];
                    status.remaining -= Time.deltaTime;
                    if (status.remaining <= 0f) { vulnerabilityStatuses.RemoveAt(i); continue; }
                    vulnerabilityStatuses[i] = status;
                    vulnerabilityBonus = Mathf.Max(vulnerabilityBonus, status.bonus);
                    vulnerabilityRemaining = Mathf.Max(vulnerabilityRemaining, status.remaining);
                }
                if (!Mathf.Approximately(previousBonus, vulnerabilityBonus)) RefreshStatusVisual();
            }

            if (freezeRemaining > 0f)
            {
                freezeRemaining = Mathf.Max(0f, freezeRemaining - Time.deltaTime);
                RefreshStatusVisual();
            }

            if (frostRemaining > 0f)
            {
                frostRemaining -= Time.deltaTime;
                if (frostRemaining <= 0f)
                    ResetFrost();
            }
            if (IsFrozen) return;

            if (Blocker != null)
            {
                Vector3 blockerDistance = Blocker.transform.position - transform.position;
                blockerDistance.y = 0f;
                float engagementReach = Mathf.Max(2.5f, Blocker.EffectiveAttackReach + .4f);
                if (!Blocker.IsAlive || Blocker.SpawnVersion != blockerVersion
                    || blockerDistance.sqrMagnitude > engagementReach * engagementReach)
                    DetachBlocker();
                else
                {
                    FaceDirection(Blocker.transform.position - transform.position);
                    PlayAnimation(AttackState);
                    meleeCooldown = Mathf.Max(0f, meleeCooldown - Time.deltaTime);
                    if (meleeCooldown <= 0f)
                    {
                        meleeCooldown = Mathf.Max(.2f, meleeInterval);
                        Blocker.TakeDamage(IsEnraged ? Mathf.RoundToInt(meleeDamage * 1.25f) : meleeDamage);
                    }
                    return;
                }
            }
            PlayAnimation(MoveState);
            float travel = moveSpeed * frostMultiplier * (IsEnraged ? 1.25f : 1f) * Time.deltaTime;
            // Consume the whole movement budget even if a fast enemy crosses several corners.
            while (nextWaypoint < points.Length)
            {
                Vector3 offset = points[nextWaypoint] - transform.position;
                float distance = offset.magnitude;
                if (distance > 0.0001f)
                {
                    FaceDirection(offset);
                    if (distance > travel)
                    {
                        transform.position += offset * (travel / distance);
                        return;
                    }
                    travel -= distance;
                }

                transform.position = points[nextWaypoint];
                nextWaypoint++;
            }

            Resolve(false);
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive || damage <= 0)
                return;

            int receivedDamage = Mathf.Max(1,
                Mathf.RoundToInt(damage * (1f + VulnerabilityBonus)
                    * (1f - Mathf.Clamp(DamageReduction, 0f, 0.9f))));
            CurrentHealth = Mathf.Max(0, CurrentHealth - receivedDamage);
            if (IsBoss && !IsEnraged && CurrentHealth > 0 && CurrentHealth <= MaxHealth * rageHealthThreshold)
            {
                IsEnraged = true;
                RefreshStatusVisual();
            }
            if (CurrentHealth == 0)
                Resolve(true);
        }

        public void ApplyFrost(float multiplier, float duration)
        {
            if (!IsAlive || duration <= 0f)
                return;

            // Refresh one status instead of multiplying repeated slow effects.
            frostMultiplier = Mathf.Min(frostMultiplier, Mathf.Clamp(multiplier, 0.1f, 1f));
            frostRemaining = Mathf.Max(frostRemaining, duration * controlDurationMultiplier);
            RefreshStatusVisual();
        }

        public void ConfigureMelee(LevelSession owner, int damage, float interval, bool ignoresBlockers = false)
        {
            session = owner;
            meleeDamage = Mathf.Max(1, damage);
            meleeInterval = Mathf.Max(.2f, interval);
            IsUnblockable = ignoresBlockers;
            if (IsUnblockable) DetachBlocker();
        }

        public void Freeze(float seconds)
        {
            if (!IsAlive || seconds <= 0f) return;
            freezeRemaining = Mathf.Max(freezeRemaining, seconds * controlDurationMultiplier);
            RefreshStatusVisual();
        }

        public void ConfigureBoss(bool boss, float controlMultiplier, float rageThreshold)
        {
            IsBoss = boss;
            IsEnraged = false;
            controlDurationMultiplier = boss ? Mathf.Clamp(controlMultiplier, .1f, 1f) : 1f;
            rageHealthThreshold = Mathf.Clamp(rageThreshold, .1f, .9f);
            RefreshStatusVisual();
        }

        public void ApplyVulnerability(float bonus, float seconds)
        {
            if (!IsAlive || bonus <= 0f || seconds <= 0f) return;
            bonus = Mathf.Clamp(bonus, 0f, 1f);
            bool refreshed = false;
            for (int i = 0; i < vulnerabilityStatuses.Count; i++)
            {
                VulnerabilityStatus status = vulnerabilityStatuses[i];
                if (!Mathf.Approximately(status.bonus, bonus)) continue;
                status.remaining = Mathf.Max(status.remaining, seconds);
                vulnerabilityStatuses[i] = status;
                refreshed = true;
                break;
            }
            if (!refreshed) vulnerabilityStatuses.Add(new VulnerabilityStatus { bonus = bonus, remaining = seconds });
            vulnerabilityBonus = Mathf.Max(vulnerabilityBonus, bonus);
            vulnerabilityRemaining = Mathf.Max(vulnerabilityRemaining, seconds);
            RefreshStatusVisual();
        }

        public bool TryAssignBlocker(SoldierAgent ally)
        {
            if (!IsAlive || IsUnblockable || ally == null || !ally.IsAlive) return false;
            if (Blocker != null) return Blocker == ally && blockerVersion == ally.SpawnVersion;
            if (!ally.TryAcceptEnemy(this)) return false;
            Blocker = ally;
            blockerVersion = ally.SpawnVersion;
            // Give a recruit time to establish its stance before the first enemy swing.
            meleeCooldown = Mathf.Max(.25f, meleeInterval * .5f);
            return true;
        }

        public void ReleaseBlocker(SoldierAgent ally)
        {
            if (Blocker == ally) DetachBlocker();
        }

        private void DetachBlocker()
        {
            SoldierAgent ally = Blocker;
            Blocker = null;
            blockerVersion = 0;
            if (ally != null) ally.ReleaseEnemy(this);
        }

        private void PlayAnimation(int state)
        {
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled
                || currentAnimation == state || !animator.HasState(0, state)) return;
            animator.CrossFade(state, .08f, 0);
            currentAnimation = state;
        }

        private void RefreshStatusVisual()
        {
            if (animator != null) animator.speed = IsFrozen ? 0f : frostMultiplier * (IsEnraged ? 1.25f : 1f);
            SetFrostTint(IsFrozen || frostRemaining > 0f || vulnerabilityRemaining > 0f || IsEnraged);
        }

        public bool ConsumeFrost()
        {
            if (!IsFrosted)
                return false;

            ResetFrost();
            return true;
        }

        private void ResetFrost()
        {
            frostRemaining = 0f;
            frostMultiplier = 1f;
            RefreshStatusVisual();
        }

        private void SetFrostTint(bool frosted)
        {
            if (rendererTints == null)
            {
                if (!frosted)
                    return;

                rendererTints = new List<RendererTint>();
                foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        Material material = materials[i];
                        if (material == null)
                            continue;
                        int property = material.HasProperty(BaseColorProperty) ? BaseColorProperty
                            : material.HasProperty(ColorProperty) ? ColorProperty : -1;
                        if (property == -1)
                            continue;
                        var original = new MaterialPropertyBlock();
                        var frost = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(original, i);
                        renderer.GetPropertyBlock(frost, i);
                        Color originalColor = original.HasProperty(property)
                            ? original.GetColor(property) : material.GetColor(property);
                        rendererTints.Add(new RendererTint
                        {
                            renderer = renderer,
                            materialIndex = i,
                            colorProperty = property,
                            originalColor = originalColor,
                            originalBlock = original,
                            frostBlock = frost
                        });
                    }
                }
            }

            foreach (RendererTint tint in rendererTints)
            {
                if (tint.renderer == null)
                    continue;
                if (frosted)
                {
                    Color highlight = IsFrozen || frostRemaining > 0f ? new Color(.15f, .75f, 1f)
                        : vulnerabilityRemaining > 0f ? new Color(.95f, .22f, .75f) : new Color(1f, .2f, .1f);
                    Color cold = Color.Lerp(tint.originalColor, highlight, IsFrozen ? .85f : .5f);
                    cold.a = tint.originalColor.a;
                    tint.frostBlock.SetColor(tint.colorProperty, cold);
                    tint.renderer.SetPropertyBlock(tint.frostBlock, tint.materialIndex);
                }
                else
                    tint.renderer.SetPropertyBlock(tint.originalBlock, tint.materialIndex);
            }
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void Resolve(bool killed)
        {
            if (!IsAlive)
                return;

            // Resolve callbacks run after live status cleanup; retain this spawn's death context.
            WasFrozenWhenKilled = killed && IsFrozen;
            WasFrostedWhenKilled = killed && IsFrosted;
            WasBlockedWhenKilled = killed && Blocker != null;
            VulnerabilityWhenKilled = killed ? VulnerabilityBonus : 0f;
            IsAlive = false;
            DetachBlocker();
            freezeRemaining = 0f;
            vulnerabilityBonus = 0f;
            vulnerabilityRemaining = 0f;
            vulnerabilityStatuses.Clear();
            IsEnraged = false;
            ResetFrost();
            Action<EnemyAgent, bool> callback = onResolved;
            onResolved = null;
            callback?.Invoke(this, killed);
        }

        private void OnDisable()
        {
            IsAlive = false;
            DetachBlocker();
            freezeRemaining = 0f;
            vulnerabilityBonus = 0f;
            vulnerabilityRemaining = 0f;
            vulnerabilityStatuses.Clear();
            IsBoss = false;
            IsEnraged = false;
            controlDurationMultiplier = 1f;
            ResetFrost();
            session = null;
            IsUnblockable = false;
            onResolved = null;
            CurrentHealth = 0;
            LateralOffset = 0f;
            nextWaypoint = 0;
            if (animator != null)
            {
                animator.speed = 1f;
                if (animator.runtimeAnimatorController != null && animator.isInitialized)
                    animator.Rebind();
            }
        }
    }
}
