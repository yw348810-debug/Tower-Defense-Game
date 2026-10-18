using System;
using UnityEngine;
using TowerDefense.Audio;
using UnityEngine.Rendering;

namespace TowerDefense.Gameplay
{
    /// <summary>A pooled falling fireball followed by a three-second burning area.</summary>
    public sealed class FireballArea : MonoBehaviour
    {
        const float FallDuration = .25f;
        const int Segments = 64, FlameCount = 18;
        LineRenderer border;
        Transform orb, core, disc, glowDisc;
        readonly Transform[] flames = new Transform[FlameCount];
        Material orange, yellow, groundTint, emberGround;
        CombatVisuals effects;
        TrailRenderer meteorTrail;
        ParticleSystem embers;
        Mesh circleMesh, flameMesh;
        float elapsed, duration, radius, interval, groundOffset, meteorSize;
        int visualLevel = 1;
        int tickDamage, nextTick, tickCount, playVersion;
        bool burning, impactShown;
        Vector3 center;
        Action<Vector3, float, int> damage;
        Action<FireballArea> returned;

        public bool IsBurning => isActiveAndEnabled && burning && elapsed >= FallDuration
            && elapsed < FallDuration + duration;
        public int TicksApplied => nextTick;
        public float RemainingBurnTime => burning ? Mathf.Max(0, FallDuration + duration - elapsed) : 0;
        public int VisualLevel => visualLevel;
        public float VisualGroundHeight => center.y + groundOffset;

        public bool ContainsBurningPoint(Vector3 point)
        {
            if (!IsBurning) return false;
            Vector3 offset = point - center;
            return offset.x * offset.x + offset.z * offset.z <= radius * radius;
        }

        void Awake()
        {
            effects = CombatVisuals.For(this);
            orange = MakeMaterial("Fireball Orange", new Color(1f, .29f, .035f, .92f));
            yellow = MakeMaterial("Fireball Core", new Color(1f, .86f, .2f, 1f));
            groundTint = CombatVisuals.MakeMaterial("Charred Ground", new Color(.12f, .055f, .025f, .6f), effects.SoftTexture);
            emberGround = CombatVisuals.MakeMaterial("Burning Ground Glow", new Color(1f, .18f, .025f, .27f),
                effects.SoftTexture, true, 2f);
            border = gameObject.AddComponent<LineRenderer>();
            border.sharedMaterial = orange;
            border.useWorldSpace = false; border.loop = true;
            border.positionCount = Segments; border.widthMultiplier = .055f;
            border.shadowCastingMode = ShadowCastingMode.Off; border.receiveShadows = false;
            circleMesh = CreateDisc();
            flameMesh = CreateFlame();
            disc = MeshObject("Burn Radius", circleMesh, groundTint);
            glowDisc = MeshObject("Hot Ground", circleMesh, emberGround);
            glowDisc.localPosition = Vector3.up * .009f;
            orb = Sphere("Falling Fireball", orange, .7f);
            meteorTrail = orb.gameObject.AddComponent<TrailRenderer>();
            meteorTrail.sharedMaterial = effects.RibbonMaterial; meteorTrail.time = .2f;
            meteorTrail.startWidth = .75f; meteorTrail.endWidth = .035f;
            meteorTrail.minVertexDistance = .08f; meteorTrail.numCornerVertices = 2;
            meteorTrail.shadowCastingMode = ShadowCastingMode.Off; meteorTrail.receiveShadows = false;
            core = Sphere("Glowing Core", yellow, .38f);
            core.SetParent(orb, false);
            core.localPosition = new Vector3(0, .02f, -.2f);
            for (int i = 0; i < FlameCount; i++)
                flames[i] = MeshObject("Flame " + i, flameMesh, i % 3 == 0 ? yellow : orange);
            CreateEmbers();
        }

        public void Play(Vector3 position, float size, float seconds, float tickInterval, int hitDamage,
            Action<Vector3, float, int> onDamage, Action<FireballArea> onReturned, int visualLevel = 1)
        {
            center = position; transform.position = center;
            this.visualLevel = Mathf.Clamp(visualLevel, 1, 3);
            var session = effects.GetComponent<LevelSession>();
            float groundHeight = session && session.map ? session.map.transform.position.y + .49f : center.y + .06f;
            groundOffset = Mathf.Max(0f, groundHeight - center.y);
            // Keep the damage center unchanged; only the decal, rim and flames rise above the road mesh.
            disc.localPosition = Vector3.up * groundOffset;
            glowDisc.localPosition = Vector3.up * (groundOffset + .009f);
            embers.transform.localPosition = Vector3.up * (groundOffset + .1f);
            radius = Mathf.Max(.1f, size); duration = Mathf.Max(.05f, seconds);
            interval = Mathf.Max(.05f, tickInterval); tickDamage = Mathf.Max(1, hitDamage);
            tickCount = Mathf.CeilToInt(duration / interval);
            elapsed = 0; nextTick = 0; burning = true; impactShown = false; playVersion++;
            damage = onDamage; returned = onReturned;
            orange.SetColor("_BaseColor", new Color(1f, .29f, .035f, .92f));
            yellow.SetColor("_BaseColor", new Color(1f, .86f, .2f, 1f));
            groundTint.SetColor("_BaseColor", new Color(.12f, .055f, .025f, .54f + this.visualLevel * .055f));
            emberGround.SetColor("_BaseColor", new Color(1f, .18f, .025f, .18f + this.visualLevel * .065f));
            orange.SetFloat("_Intensity", 1.55f + this.visualLevel * .15f);
            yellow.SetFloat("_Intensity", 1.7f + this.visualLevel * .18f);
            border.enabled = false; disc.gameObject.SetActive(false); glowDisc.gameObject.SetActive(false);
            orb.gameObject.SetActive(true);
            meteorSize = this.visualLevel == 1 ? .65f : this.visualLevel == 2 ? 1f : 1.45f;
            orb.localScale = Vector3.one * meteorSize;
            orb.localPosition = new Vector3(-1.5f, 7, -1);
            meteorTrail.startWidth = meteorSize * 1.08f;
            meteorTrail.time = .16f + this.visualLevel * .035f;
            meteorTrail.startColor = new Color(1f, .63f, .12f, .95f);
            meteorTrail.endColor = new Color(1f, .19f, .02f, 0f);
            meteorTrail.Clear(); meteorTrail.emitting = true;
            disc.localScale = Vector3.one * radius;
            glowDisc.localScale = Vector3.one * radius * .88f;
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emberShape = embers.shape; emberShape.radius = radius * .86f;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments;
                border.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, groundOffset + .025f, Mathf.Sin(angle) * radius));
            }
            border.widthMultiplier = .04f + this.visualLevel * .018f;
            foreach (var flame in flames) flame.gameObject.SetActive(false);
        }

        void Update() { if (Time.timeScale > 0f) Advance(Time.deltaTime); }

        // Explicit advancement also allows precise duration/pause boundary checks in the Editor.
        public void Advance(float deltaTime)
        {
            if (!burning || deltaTime <= 0) return;
            elapsed += deltaTime;
            if (elapsed < FallDuration)
            {
                orb.localPosition = Vector3.Lerp(new Vector3(-1.5f, 7, -1), Vector3.up * (groundOffset + .3f),
                    Mathf.Pow(elapsed / FallDuration, 1.5f));
                orb.localScale = Vector3.one * meteorSize * (1f + Mathf.Sin(elapsed * 40) * .08f);
                return;
            }
            if (!impactShown)
            {
                impactShown = true;
                meteorTrail.emitting = false;
                effects.MeteorImpact(center + Vector3.up * groundOffset, radius, visualLevel);
                GameAudio.Play(SoundCue.Explosion, center);
                embers.Play(false);
            }
            orb.gameObject.SetActive(false); border.enabled = true;
            disc.gameObject.SetActive(true); glowDisc.gameObject.SetActive(true);
            float burnTime = elapsed - FallDuration;
            int currentVersion = playVersion;
            while (burning && nextTick < tickCount && burnTime >= nextTick * interval)
            {
                nextTick++;
                damage?.Invoke(center, radius, tickDamage);
                // A final kill may finish the battle and return this object during the callback.
                if (!burning || currentVersion != playVersion) return;
            }
            AnimateFlames(burnTime);
            if (burnTime < duration) return;
            burning = false;
            var callback = returned; returned = null; damage = null;
            callback?.Invoke(this);
        }

        void AnimateFlames(float time)
        {
            float fade = Mathf.Clamp01((duration - time) / .3f);
            groundTint.SetColor("_BaseColor", new Color(.12f, .055f, .025f, (.54f + visualLevel * .055f) * fade));
            emberGround.SetColor("_BaseColor", new Color(1, .18f, .025f,
                (.16f + visualLevel * .065f + .04f * Mathf.Sin(time * 9)) * fade));
            orange.SetColor("_BaseColor", new Color(1, .29f, .035f, .92f * fade));
            yellow.SetColor("_BaseColor", new Color(1, .86f, .2f, fade));
            var emission = embers.emission; emission.rateOverTime = (8f + visualLevel * 8f) * fade;
            for (int i = 0; i < FlameCount; i++)
            {
                flames[i].gameObject.SetActive(true);
                float angle = i * 2.399963f;
                float distance = radius * Mathf.Sqrt((i + .5f) / FlameCount) * .86f;
                float flutter = .75f + .25f * Mathf.Sin(time * 13 + i * 2.1f);
                flames[i].localPosition = new Vector3(Mathf.Cos(angle) * distance, groundOffset + .03f, Mathf.Sin(angle) * distance);
                float width = .12f + visualLevel * .035f;
                flames[i].localScale = new Vector3(width, (.35f + flutter * .35f) * (1f + (visualLevel - 1) * .25f), width) * fade;
                flames[i].localRotation = Quaternion.Euler(Mathf.Sin(time * 9 + i) * 12, i * 43, 0);
            }
        }

        void CreateEmbers()
        {
            var go = new GameObject("Rising Embers"); go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * .1f;
            embers = go.AddComponent<ParticleSystem>();
            embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = embers.main;
            main.loop = true; main.playOnAwake = false; main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.5f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.04f, .12f);
            main.startColor = new Color(1f, .58f, .13f, .9f);
            main.maxParticles = 48; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            embers.useAutoRandomSeed = false; embers.randomSeed = 29;
            var emission = embers.emission; emission.rateOverTime = 14;
            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Circle; shape.rotation = new Vector3(-90, 0, 0);
            var velocity = embers.velocityOverLifetime; velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World; velocity.y = .45f;
            var color = embers.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0),
                new GradientColorKey(new Color(1f, .3f, .08f), 1) },
                new[] { new GradientAlphaKey(.7f, 0), new GradientAlphaKey(0, 1) });
            color.color = gradient;
            var renderer = embers.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = effects.SoftGlowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        Transform Sphere(string name, Material material, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false); go.transform.localScale = Vector3.one * scale;
            SetRenderer(go.GetComponent<Renderer>(), material);
            return go.transform;
        }
        Transform MeshObject(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false); go.GetComponent<MeshFilter>().sharedMesh = mesh;
            SetRenderer(go.GetComponent<MeshRenderer>(), material);
            return go.transform;
        }
        static void SetRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        static Material MakeMaterial(string name, Color color)
        {
            return CombatVisuals.MakeMaterial(name, color, null, false, 1.7f);
        }
        static Mesh CreateDisc()
        {
            var mesh = new Mesh { name = "Spell Circle (Runtime)" };
            var vertices = new Vector3[Segments + 1]; var triangles = new int[Segments * 3];
            var uv = new Vector2[vertices.Length]; var colors = new Color[vertices.Length];
            uv[0] = new Vector2(.5f, .5f); colors[0] = Color.white;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                uv[i + 1] = new Vector2(vertices[i + 1].x * .5f + .5f, vertices[i + 1].z * .5f + .5f);
                colors[i + 1] = Color.white;
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % Segments + 1;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colors; mesh.triangles = triangles;
            mesh.RecalculateBounds(); mesh.RecalculateNormals();
            return mesh;
        }
        static Mesh CreateFlame()
        {
            var mesh = new Mesh { name = "Low Poly Flame (Runtime)" };
            mesh.vertices = new[] { new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(1,0,1),
                new Vector3(-1,0,1), new Vector3(.15f,1,0) };
            mesh.triangles = new[] {0,4,1,1,4,2,2,4,3,3,4,0};
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white, Color.white };
            mesh.RecalculateBounds(); mesh.RecalculateNormals(); return mesh;
        }
        void OnDisable()
        {
            burning = false; damage = null; returned = null;
            if (meteorTrail) { meteorTrail.emitting = false; meteorTrail.Clear(); }
            if (embers) embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void OnDestroy()
        {
            if (orange) Destroy(orange); if (yellow) Destroy(yellow); if (groundTint) Destroy(groundTint);
            if (emberGround) Destroy(emberGround);
            if (circleMesh) Destroy(circleMesh); if (flameMesh) Destroy(flameMesh);
        }
    }
}
