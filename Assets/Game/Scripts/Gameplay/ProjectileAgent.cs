using System;
using UnityEngine;

namespace TowerDefense.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ProjectileAgent : MonoBehaviour
    {
        [Min(0.1f)] public float speed = 26f;
        [Min(0.01f)] public float hitRadius = 0.18f;
        [Min(0.1f)] public float maxLifetime = 3f;

        private EnemyAgent target;
        private int targetVersion;
        private ShotSpec shot;
        private float age;
        private bool flying;
        private Action<ProjectileAgent> onReturned;
        private Action<EnemyAgent, ShotSpec> onHit;
        private TrailRenderer[] trails;
        private Renderer[] renderers;
        private MaterialPropertyBlock colorBlock;
        private CombatVisuals effects;
        private Transform visual;
        private MeshFilter visualMesh;
        private MeshRenderer visualRenderer;
        private TrailRenderer visualTrail;
        private Transform masteryVisual;
        private MeshFilter masteryMesh;
        private MeshRenderer masteryRenderer;
        private TrailRenderer masteryTrail;
        private float launchDistance;
        public int VisualLevel => Mathf.Clamp(shot.level, 1, 3);
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        public void Launch(EnemyAgent enemy, Vector3 position, int hitDamage, Action<ProjectileAgent> returned)
        {
            Launch(enemy, position, new ShotSpec
            {
                kind = TowerKind.Gun,
                damage = Mathf.Max(0, hitDamage),
                color = new Color(1f, 0.72f, 0.18f)
            }, returned, null);
        }

        public void Launch(EnemyAgent enemy, Vector3 position, ShotSpec shotSpec,
            Action<ProjectileAgent> returned, Action<EnemyAgent, ShotSpec> hit)
        {
            transform.position = position;
            target = enemy;
            targetVersion = enemy != null ? enemy.SpawnVersion : 0;
            shot = shotSpec;
            shot.damage = Mathf.Max(0, shot.damage);
            age = 0f;
            onReturned = returned;
            onHit = hit;
            flying = true;
            EnsureVisuals();
            ConfigureVisual();
            ClearTrails();
            SetShotColor(shot.color);
            if (!HasLiveTarget())
                ReturnToPool();
            else
            {
                launchDistance = Mathf.Max(.1f, Vector3.Distance(position, enemy.AimPosition));
                visual.position = position;
                visualTrail.emitting = true;
                if (masteryVisual.gameObject.activeSelf) masteryTrail.emitting = true;
                Vector3 direction = enemy.AimPosition - position;
                if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                UpdateVisual(enemy.AimPosition);
                effects.Muzzle(position, direction, shot.kind, shot.color, VisualLevel);
            }
        }

        private void Update()
        {
            if (!flying || Time.deltaTime <= 0f || Time.timeScale <= 0f)
                return;

            age += Time.deltaTime;
            if (!HasLiveTarget() || age >= maxLifetime)
            {
                ReturnToPool();
                return;
            }

            Vector3 aim = target.AimPosition;
            Vector3 offset = aim - transform.position;
            float distance = offset.magnitude;
            float step = Mathf.Max(0f, speed) * Time.deltaTime;
            if (distance <= step + hitRadius)
            {
                transform.position = aim;
                // Remove this shot first: the final kill may return every remaining projectile.
                EnemyAgent hit = target;
                int hitVersion = targetVersion;
                ShotSpec hitShot = shot;
                Action<EnemyAgent, ShotSpec> hitCallback = onHit;
                effects.Impact(aim, hitShot.kind, hitShot.color, hitShot.level, hitShot.freezeDuration > 0f);
                ReturnToPool();
                if (hit != null && hit.IsAlive && hit.SpawnVersion == hitVersion)
                {
                    if (hitCallback != null)
                        hitCallback.Invoke(hit, hitShot);
                    else
                        hit.TakeDamage(hitShot.damage);
                }
                return;
            }

            transform.rotation = Quaternion.LookRotation(offset, Vector3.up);
            transform.position += offset * (step / distance);
            UpdateVisual(aim);
        }

        private void EnsureVisuals()
        {
            if (!effects) effects = CombatVisuals.For(this);
            if (visual) return;
            // The prefab remains the simulation anchor. Only its new child follows the cannon arc.
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var child = new GameObject("Polished Shot", typeof(MeshFilter), typeof(MeshRenderer), typeof(TrailRenderer));
            visual = child.transform;
            visual.SetParent(transform, false);
            visualMesh = child.GetComponent<MeshFilter>();
            visualRenderer = child.GetComponent<MeshRenderer>();
            visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visualRenderer.receiveShadows = false;
            visualTrail = child.GetComponent<TrailRenderer>();
            visualTrail.sharedMaterial = effects.RibbonMaterial;
            visualTrail.minVertexDistance = .04f;
            visualTrail.numCornerVertices = 2;
            visualTrail.numCapVertices = 2;
            visualTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visualTrail.receiveShadows = false;
            var mastery = new GameObject("Mastery Shot Accent", typeof(MeshFilter), typeof(MeshRenderer), typeof(TrailRenderer));
            masteryVisual = mastery.transform; masteryVisual.SetParent(transform, false);
            masteryMesh = mastery.GetComponent<MeshFilter>();
            masteryRenderer = mastery.GetComponent<MeshRenderer>();
            masteryRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            masteryRenderer.receiveShadows = false;
            masteryTrail = mastery.GetComponent<TrailRenderer>();
            masteryTrail.sharedMaterial = effects.RibbonMaterial; masteryTrail.minVertexDistance = .045f;
            masteryTrail.numCornerVertices = 2; masteryTrail.numCapVertices = 2;
            masteryTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            masteryTrail.receiveShadows = false;
            renderers = new Renderer[] { visualRenderer, masteryRenderer };
            trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        private void ConfigureVisual()
        {
            visual.gameObject.SetActive(true);
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            int level = VisualLevel;
            bool frost = shot.kind == TowerKind.Frost, cannon = shot.kind == TowerKind.Cannon;
            visualMesh.sharedMesh = frost ? effects.CrystalMesh : effects.SphereMesh;
            visualRenderer.sharedMaterial = frost ? effects.IceMaterial : effects.CoreMaterial;
            Vector3 size = cannon ? Vector3.one * .38f : frost ? new Vector3(.24f, .24f, .48f)
                : new Vector3(.15f, .15f, .48f);
            size *= 1f + (level - 1) * (cannon ? .27f : .22f);
            Vector3 scale = transform.lossyScale;
            visual.localScale = new Vector3(size.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                size.y / Mathf.Max(.001f, Mathf.Abs(scale.y)), size.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            visualTrail.time = (cannon ? .22f : frost ? .24f : .13f) + (level - 1) * .015f;
            visualTrail.startWidth = (cannon ? .22f : frost ? .13f : .095f) * (1f + (level - 1) * .2f);
            visualTrail.endWidth = .005f;
            masteryVisual.gameObject.SetActive(level == 3);
            masteryVisual.localRotation = frost ? Quaternion.Euler(0, 18f, 0) : Quaternion.identity;
            masteryMesh.sharedMesh = frost ? effects.CrystalMesh : effects.SphereMesh;
            masteryRenderer.sharedMaterial = frost ? effects.IceMaterial : effects.CoreMaterial;
            Vector3 accentSize = cannon ? Vector3.one * .24f : frost ? new Vector3(.24f, .24f, .4f)
                : new Vector3(.19f, .19f, .59f);
            masteryVisual.localScale = new Vector3(accentSize.x / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                accentSize.y / Mathf.Max(.001f, Mathf.Abs(scale.y)), accentSize.z / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            masteryTrail.time = frost ? .25f : cannon ? .17f : .15f;
            masteryTrail.startWidth = cannon ? .11f : .13f; masteryTrail.endWidth = .004f;
        }

        private void UpdateVisual(Vector3 aim)
        {
            if (!visual) return;
            float progress = Mathf.Clamp01(1f - Vector3.Distance(transform.position, aim) / launchDistance);
            float arc = shot.kind == TowerKind.Cannon
                ? Mathf.Sin(progress * Mathf.PI) * Mathf.Min(2.4f, launchDistance * .24f) : 0f;
            Vector3 position = transform.position + Vector3.up * arc;
            visual.position = position;
            if (VisualLevel == 3)
            {
                if (shot.kind == TowerKind.Gun)
                {
                    visual.position += transform.right * .105f;
                    masteryVisual.position = position - transform.right * .105f;
                }
                else if (shot.kind == TowerKind.Frost)
                    masteryVisual.position = position - transform.forward * .25f
                        + transform.right * Mathf.Sin(age * 28f) * .16f;
                else
                    masteryVisual.position = position + transform.forward * .17f + Vector3.up * .1f;
            }
        }

        private bool HasLiveTarget()
        {
            return target != null && target.IsAlive && target.SpawnVersion == targetVersion;
        }

        private void ReturnToPool()
        {
            if (!flying)
                return;

            Action<ProjectileAgent> callback = onReturned;
            ClearFlight();
            callback?.Invoke(this);
        }

        private void ClearFlight()
        {
            flying = false;
            target = null;
            targetVersion = 0;
            shot = default;
            age = 0f;
            onReturned = null;
            onHit = null;
            if (visualTrail) visualTrail.emitting = false;
            if (visual) visual.gameObject.SetActive(false);
            if (masteryTrail) masteryTrail.emitting = false;
            if (masteryVisual) masteryVisual.gameObject.SetActive(false);
        }

        private void SetShotColor(Color color)
        {
            if (renderers == null)
                renderers = GetComponentsInChildren<Renderer>(true);
            if (colorBlock == null)
                colorBlock = new MaterialPropertyBlock();
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is TrailRenderer)
                    continue;
                renderer.GetPropertyBlock(colorBlock);
                colorBlock.SetColor(BaseColorProperty, color);
                colorBlock.SetColor(ColorProperty, color);
                renderer.SetPropertyBlock(colorBlock);
            }
            foreach (TrailRenderer trail in trails)
            {
                if (trail == null)
                    continue;
                trail.startColor = color;
                Color endColor = color;
                endColor.a = 0.1f;
                trail.endColor = endColor;
            }
            if (masteryRenderer && VisualLevel == 3)
            {
                Color accent = shot.kind == TowerKind.Frost ? new Color(.64f, .94f, 1f)
                    : new Color(1f, .88f, .34f);
                colorBlock.SetColor(BaseColorProperty, accent);
                colorBlock.SetColor(ColorProperty, accent);
                masteryRenderer.SetPropertyBlock(colorBlock);
                masteryTrail.startColor = accent;
                accent.a = 0f; masteryTrail.endColor = accent;
            }
        }

        private void ClearTrails()
        {
            if (trails == null)
                trails = GetComponentsInChildren<TrailRenderer>(true);
            foreach (TrailRenderer trail in trails)
            {
                if (trail != null)
                    trail.Clear();
            }
        }

        private void OnDisable()
        {
            ClearFlight();
            ClearTrails();
        }
    }
}
