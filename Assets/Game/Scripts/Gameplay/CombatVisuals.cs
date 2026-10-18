using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerDefense.Gameplay
{
    /// <summary>Scene-owned, bounded visual effects. It never applies damage or changes combat time.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatVisuals : MonoBehaviour
    {
        public const int BurstCapacity = 48;
        readonly List<Burst> active = new List<Burst>(BurstCapacity);
        readonly Stack<Burst> available = new Stack<Burst>(BurstCapacity);
        readonly List<Burst> all = new List<Burst>(BurstCapacity);
        LevelSession session;
        Transform root;
        Texture2D softTexture, ribbonTexture;
        Material glow, smoke, crystal, ribbon, solid;
        Mesh sphereMesh, crystalMesh;
        float lastAssaultTime;
        int emissionSequence;

        public int ActiveBurstCount => active.Count;
        public int PeakBurstCount { get; private set; }
        public Material RibbonMaterial => ribbon;
        public Material CoreMaterial => solid;
        public Material IceMaterial => crystal;
        internal Material SoftGlowMaterial => glow;
        internal Texture2D SoftTexture => softTexture;
        public Mesh SphereMesh => sphereMesh;
        public Mesh CrystalMesh => crystalMesh;

        public static CombatVisuals For(Component source)
        {
            if (!source) return null;
            var level = source.GetComponentInParent<LevelSession>();
            GameObject owner = level ? level.gameObject : source.transform.root.gameObject;
            var effects = owner.GetComponent<CombatVisuals>();
            return effects ? effects : owner.AddComponent<CombatVisuals>();
        }

        void Awake()
        {
            session = GetComponent<LevelSession>();
            lastAssaultTime = session ? session.AssaultTime : 0f;
            root = new GameObject("Combat Effects (Pooled)").transform;
            root.SetParent(transform, false);
            softTexture = MakeSoftTexture();
            ribbonTexture = MakeRibbonTexture();
            glow = MakeMaterial("Combat Soft Glow", Color.white, softTexture, true, 2.1f);
            smoke = MakeMaterial("Combat Soft Smoke", Color.white, softTexture, false, 1f);
            crystal = MakeMaterial("Combat Ice Shards", Color.white, null, false, 1.6f);
            ribbon = MakeMaterial("Combat Light Ribbon", Color.white, ribbonTexture, true, 2f);
            solid = MakeMaterial("Combat Projectile Core", Color.white, null, false, 2.3f);
            sphereMesh = MakeSphere();
            crystalMesh = MakeCrystal();
        }

        public void Muzzle(Vector3 position, Vector3 direction, TowerKind kind, Color tint, int level = 1)
        {
            level = Mathf.Clamp(level, 1, 3);
            Burst burst = Acquire(position, .34f);
            burst.SetupParticles(kind == TowerKind.Frost);
            Vector3 forward = direction.sqrMagnitude > .01f ? direction.normalized : Vector3.forward;
            EmitFlash(burst, position, tint, (kind == TowerKind.Cannon ? .65f : .38f) * (1f + (level - 1) * .2f), .09f);
            int count = (kind == TowerKind.Gun ? 5 : kind == TowerKind.Frost ? 4 : 7) + (level - 1) * 2;
            for (int i = 0; i < count; i++)
            {
                Vector3 spray = Hemisphere(i, emissionSequence) * .9f + forward * 2.2f;
                Emit(burst.sparks, position, spray, kind == TowerKind.Frost ? .085f : .055f,
                    .16f + Unit(i, emissionSequence) * .12f, Bright(tint), i * 53f);
            }
            if (kind == TowerKind.Cannon)
                Emit(burst.smoke, position, forward * .45f + Vector3.up * .3f, .42f, .32f,
                    new Color(.26f, .24f, .22f, .32f), 0);
        }

        public void Impact(Vector3 position, TowerKind kind, Color tint, int level = 1, bool freezing = false)
        {
            level = Mathf.Clamp(level, 1, 3);
            float lifetime = kind == TowerKind.Cannon ? .85f : .5f;
            Burst burst = Acquire(position, lifetime);
            bool icy = kind == TowerKind.Frost;
            burst.SetupParticles(icy);
            EmitFlash(burst, position, tint, (kind == TowerKind.Cannon ? 1.2f : .58f) * (1f + (level - 1) * .12f), .14f);
            int count = (kind == TowerKind.Gun ? 7 : icy ? 11 : 16) + (level - 1) * 3;
            for (int i = 0; i < count; i++)
            {
                Vector3 spray = Hemisphere(i, emissionSequence) * (kind == TowerKind.Cannon ? 3f : 1.6f);
                Emit(burst.sparks, position, spray, (icy ? .16f : .065f) * (1f + (level - 1) * .15f),
                    .2f + Unit(i, emissionSequence) * .28f, Bright(tint), i * 67f);
            }
            if (kind == TowerKind.Cannon)
            {
                for (int i = 0; i < 3 + level * 2; i++)
                    Emit(burst.smoke, position + Hemisphere(i, emissionSequence) * .18f,
                        Hemisphere(i, emissionSequence) * .55f + Vector3.up * .35f,
                        .55f + i * .08f, .7f, new Color(.2f, .19f, .18f, .42f), i * 47f);
                burst.SetupRings(GroundPoint(position), .65f + level * .2f, tint, .32f);
            }
            else if (level == 3)
                burst.SetupRings(GroundPoint(position), icy && freezing ? 1.7f : icy ? .85f : 1.25f,
                    icy ? new Color(.4f, .85f, 1f) : new Color(1f, .65f, .12f), icy ? .4f : .26f);
        }

        public void MeteorImpact(Vector3 position, float radius, int level = 1)
        {
            level = Mathf.Clamp(level, 1, 3);
            Burst burst = Acquire(position + Vector3.up * .1f, .8f + level * .15f);
            burst.SetupParticles(false);
            Color fire = new Color(1f, .36f, .06f);
            EmitFlash(burst, position + Vector3.up * .25f, new Color(1f, .8f, .3f), 1.4f + level * .45f, .17f);
            for (int i = 0; i < 16 + level * 8; i++)
            {
                Vector3 direction = Hemisphere(i, emissionSequence);
                Emit(burst.sparks, position + Vector3.up * .25f,
                    direction * (2f + level * .55f + Unit(i, emissionSequence) * 1.8f), .07f + level * .012f,
                    .4f + Unit(i + 13, emissionSequence) * .38f, Bright(fire), i * 37f);
            }
            for (int i = 0; i < 2 + level * 2; i++)
                Emit(burst.smoke, position + Hemisphere(i, emissionSequence) * .4f,
                    Hemisphere(i, emissionSequence) * .8f + Vector3.up * .45f, .8f + i * .06f,
                    1f, new Color(.16f, .13f, .1f, .38f), i * 49f);
            burst.SetupRings(position + Vector3.up * .025f, radius, fire, .36f + level * .06f);
        }

        public void AbilityBurst(Vector3 position, float radius, Color tint, int level = 1)
        {
            level = Mathf.Clamp(level, 1, 3);
            Burst burst = Acquire(position, .7f);
            bool icy = tint.b > .7f && tint.g > .65f;
            burst.SetupParticles(icy);
            int count = Mathf.Clamp(Mathf.RoundToInt(radius * 4f) + (level - 1) * 4, 8, 32);
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.399963f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), .35f + Unit(i, emissionSequence), Mathf.Sin(angle));
                Vector3 start = position + new Vector3(direction.x, .05f, direction.z) * radius * .55f;
                Emit(burst.sparks, start, direction * .85f, icy ? .14f : .065f,
                    .3f + Unit(i, emissionSequence) * .32f, tint, i * 31f);
            }
        }

        public void FrostNova(Vector3 position, float radius, int level = 1)
        {
            level = Mathf.Clamp(level, 1, 3);
            position = GroundPoint(position);
            Burst burst = Acquire(position, .6f + level * .2f);
            burst.SetupParticles(true);
            Color ice = new Color(.3f, .76f, 1f, .8f);
            burst.SetupRings(position + Vector3.up * .02f, radius, ice, .4f + level * .15f);
            if (level >= 2) burst.SetupSigil(position + Vector3.up * .025f, radius * .6f, ice, level, 0f, false);
            int count = 8 + level * 8;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Emit(burst.sparks, position + radial * radius * (.3f + Unit(i, emissionSequence) * .6f),
                    radial * .25f + Vector3.up * (1f + level * .2f), .16f + level * .045f,
                    .38f + Unit(i, emissionSequence) * .4f, ice, i * 53f);
            }
            EmitFlash(burst, position + Vector3.up * .15f, ice, 1f + level * .35f, .14f);
        }

        public void ReinforcementArrival(Vector3 position, int level = 1)
        {
            level = Mathf.Clamp(level, 1, 3);
            position = GroundPoint(position);
            Burst burst = Acquire(position, .6f + level * .25f);
            burst.SetupParticles(false);
            Color rally = level == 3 ? new Color(1f, .79f, .28f, .65f) : new Color(.2f, 1f, .68f, .65f);
            burst.SetupRings(position + Vector3.up * .03f, .7f + level * .3f, rally, burst.lifetime);
            burst.SetupSigil(position + Vector3.up * .03f, .55f + level * .2f, rally, level, .9f + level * .5f, false);
            for (int i = 0; i < 6 + level * 4; i++)
                Emit(burst.sparks, position + Hemisphere(i, emissionSequence) * .35f,
                    Vector3.up * (1f + Unit(i, emissionSequence)) + Hemisphere(i, emissionSequence) * .25f,
                    .05f + level * .012f, .5f + Unit(i, emissionSequence) * .4f, rally, i * 43f);
        }

        public void BattlecryAura(Transform target, int level = 1, float seconds = 2.2f)
        {
            if (!target || !target.gameObject.activeInHierarchy) return;
            level = Mathf.Clamp(level, 1, 3);
            Vector3 position = target.position + Vector3.up * .075f;
            Burst burst = Acquire(position, Mathf.Clamp(seconds, .3f, 3.5f));
            burst.SetupParticles(false);
            float radius = target.GetComponent<TowerAgent>() ? 1.2f : .52f;
            radius *= 1f + (level - 1) * .14f;
            Color gold = new Color(1f, .73f, .15f, .48f);
            burst.SetupRings(position, radius, gold, burst.lifetime);
            burst.SetupSigil(position, radius * .8f, gold, level, level == 3 ? 1.35f : 0f, true);
            burst.Follow(target, Vector3.up * .075f);
            for (int i = 0; i < 3 + level * 2; i++)
                Emit(burst.sparks, position + Hemisphere(i, emissionSequence) * radius * .4f,
                    Vector3.up * .7f, .05f, .45f, gold, i * 53f);
        }

        public void SanctuaryPulse(Vector3 position, int tier)
        {
            tier = Mathf.Clamp(tier, 1, 3);
            position = GroundPoint(position);
            Burst burst = Acquire(position, .8f);
            burst.SetupParticles(false);
            Color jade = new Color(.2f, 1f, .68f, .42f);
            burst.SetupRings(position + Vector3.up * .025f, .8f + tier * .3f, jade, .7f);
            if (tier >= 2)
                burst.SetupSigil(position + Vector3.up * .035f, .75f + tier * .15f, jade,
                    tier, tier == 3 ? 1.4f : 0f, false);
            for (int i = 0; i < 3 + tier * 2; i++)
                Emit(burst.sparks, position + Hemisphere(i, emissionSequence) * .5f,
                    Vector3.up * .65f, .035f + tier * .01f, .55f, jade, i * 43f);
        }

        public void SoldierStrike(Vector3 position, bool hero, int tier, bool cleave)
        {
            tier = Mathf.Clamp(tier, 1, 3);
            Burst burst = Acquire(position, cleave ? .38f : .22f);
            burst.SetupParticles(false);
            Color tint = hero ? new Color(1f, .8f, .27f) : new Color(.45f, .83f, .79f);
            int count = cleave ? 12 : hero ? 5 : 3;
            for (int i = 0; i < count; i++)
                Emit(burst.sparks, position, Hemisphere(i, emissionSequence) * (cleave ? 1.7f : .8f),
                    hero ? .06f : .035f, .15f + Unit(i, emissionSequence) * .16f, tint, i * 43f);
            if (cleave) burst.SetupRings(GroundPoint(position), 1.6f, tint, .3f);
        }

        Burst Acquire(Vector3 position, float lifetime)
        {
            if (active.Count >= BurstCapacity)
            {
                // Recycling the oldest visual prevents a large assault from growing without a bound.
                Release(active[0]);
                active.RemoveAt(0);
            }
            Burst burst = available.Count > 0 ? available.Pop() : CreateBurst();
            burst.gameObject.SetActive(true);
            burst.transform.position = position;
            burst.age = 0f;
            burst.lifetime = lifetime;
            burst.Clear();
            active.Add(burst);
            emissionSequence++;
            PeakBurstCount = Mathf.Max(PeakBurstCount, active.Count);
            return burst;
        }

        Burst CreateBurst()
        {
            var burst = new Burst(this, root, all.Count);
            all.Add(burst);
            return burst;
        }

        void LateUpdate()
        {
            if (session)
            {
                if (session.AssaultTime < lastAssaultTime || session.Phase == SessionPhase.Victory
                    || session.Phase == SessionPhase.Defeat)
                    ClearBursts();
                lastAssaultTime = session.AssaultTime;
            }
            if (Time.deltaTime <= 0f || Time.timeScale <= 0f) return;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Burst burst = active[i];
                if (!burst.FollowIsValid)
                {
                    active.RemoveAt(i); Release(burst); continue;
                }
                burst.age += Time.deltaTime;
                burst.AnimateRings();
                if (burst.age < burst.lifetime) continue;
                active.RemoveAt(i);
                Release(burst);
            }
        }

        void Release(Burst burst)
        {
            burst.Clear();
            burst.gameObject.SetActive(false);
            available.Push(burst);
        }

        public void ClearBursts()
        {
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
            active.Clear();
        }

        Vector3 GroundPoint(Vector3 position)
        {
            if (session && session.map) position.y = session.map.transform.position.y + .49f;
            return position;
        }

        static Color Bright(Color color) => Color.Lerp(color, Color.white, .28f);
        static float Unit(int index, int seed)
        {
            uint value = unchecked((uint)(index * 374761393 + seed * 668265263));
            value = (value ^ (value >> 13)) * 1274126177u;
            return (value & 65535u) / 65535f;
        }
        static Vector3 Hemisphere(int index, int seed)
        {
            float angle = index * 2.399963f + Unit(index + 7, seed) * .4f;
            float height = .25f + Unit(index, seed) * .85f;
            return new Vector3(Mathf.Cos(angle), height, Mathf.Sin(angle)).normalized;
        }

        static void Emit(ParticleSystem particles, Vector3 position, Vector3 velocity,
            float size, float lifetime, Color tint, float rotation)
        {
            var values = new ParticleSystem.EmitParams
            {
                position = position, velocity = velocity, startSize = size,
                startLifetime = lifetime, startColor = tint, rotation = rotation
            };
            particles.Emit(values, 1);
        }

        void EmitFlash(Burst burst, Vector3 position, Color tint, float size, float duration)
        {
            Emit(burst.flash, position, Vector3.zero, size, duration, Bright(tint), 0f);
        }

        internal static Material MakeMaterial(string name, Color tint, Texture texture = null,
            bool additive = false, float intensity = 1f)
        {
            Shader shader = Shader.Find("TowerDefense/CombatSoft");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", tint);
            if (texture) material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        internal static LineRenderer MakeRing(string name, Transform parent, Material material, int segments = 48)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.widthMultiplier = .07f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.textureMode = LineTextureMode.Tile;
            return line;
        }

        internal static void DrawRing(LineRenderer line, float radius, Color color, float width)
        {
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        ParticleSystem MakeParticles(string name, Transform parent, Material material, bool dusty)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.maxParticles = 64;
            main.gravityModifier = dusty ? 0f : .45f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            particles.useAutoRandomSeed = false;
            particles.randomSeed = 17;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.85f, .28f), new GradientAlphaKey(0, 1) });
            colors.color = gradient;
            if (dusty)
            {
                var size = particles.sizeOverLifetime; size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .65f, 1, 1.4f));
            }
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            return particles;
        }

        static Texture2D MakeSoftTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Combat Soft Radial", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
            }
            texture.SetPixels(colors); texture.Apply(false, true);
            return texture;
        }

        static Texture2D MakeRibbonTexture()
        {
            const int size = 16;
            var texture = new Texture2D(2, size, TextureFormat.RGBA32, false)
            { name = "Combat Soft Ribbon", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var colors = new Color[size * 2];
            for (int y = 0; y < size; y++)
            {
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs((y + .5f) / size * 2f - 1f)), .7f);
                colors[y * 2] = colors[y * 2 + 1] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(colors); texture.Apply(false, true);
            return texture;
        }

        static Mesh MakeSphere()
        {
            const int rows = 8, columns = 12;
            var vertices = new Vector3[(rows + 1) * (columns + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rows * columns * 6];
            for (int row = 0; row <= rows; row++) for (int column = 0; column <= columns; column++)
            {
                float latitude = row * Mathf.PI / rows, longitude = column * Mathf.PI * 2f / columns;
                int i = row * (columns + 1) + column;
                vertices[i] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude),
                    Mathf.Sin(latitude) * Mathf.Sin(longitude)) * .5f;
                uv[i] = new Vector2(column / (float)columns, row / (float)rows);
            }
            int t = 0;
            for (int row = 0; row < rows; row++) for (int column = 0; column < columns; column++)
            {
                int a = row * (columns + 1) + column, b = a + columns + 1;
                triangles[t++] = a; triangles[t++] = a + 1; triangles[t++] = b;
                triangles[t++] = a + 1; triangles[t++] = b + 1; triangles[t++] = b;
            }
            var mesh = new Mesh { name = "Combat Sphere (Shared)" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            var colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh MakeCrystal()
        {
            var mesh = new Mesh { name = "Combat Ice Crystal (Shared)" };
            mesh.vertices = new[] { new Vector3(0,0,.85f), new Vector3(0,0,-.65f),
                new Vector3(.3f,0,0), new Vector3(-.3f,0,0), new Vector3(0,.3f,0), new Vector3(0,-.3f,0) };
            mesh.triangles = new[] { 0,2,4,0,4,3,0,3,5,0,5,2,1,4,2,1,3,4,1,5,3,1,2,5 };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        void OnDisable() { ClearBursts(); }
        void OnDestroy()
        {
            if (glow) Destroy(glow); if (smoke) Destroy(smoke); if (crystal) Destroy(crystal);
            if (ribbon) Destroy(ribbon); if (solid) Destroy(solid);
            if (softTexture) Destroy(softTexture); if (ribbonTexture) Destroy(ribbonTexture);
            if (sphereMesh) Destroy(sphereMesh); if (crystalMesh) Destroy(crystalMesh);
            if (root) Destroy(root.gameObject);
        }

        sealed class Burst
        {
            public readonly GameObject gameObject;
            public readonly Transform transform;
            public readonly ParticleSystem sparks, smoke, flash;
            readonly ParticleSystemRenderer sparkRenderer;
            readonly LineRenderer firstRing, secondRing, sigil;
            readonly LineRenderer[] beams = new LineRenderer[3];
            readonly CombatVisuals owner;
            Color ringTint;
            float ringRadius, ringDuration;
            Color sigilTint;
            float sigilRadius, beamHeight;
            int sigilLevel;
            bool holdRings, following;
            Transform followTarget;
            SoldierAgent followSoldier;
            int followVersion;
            Vector3 followOffset;
            public float age, lifetime;
            public bool FollowIsValid => !following || (followTarget && followTarget.gameObject.activeInHierarchy
                && (!followSoldier || (followSoldier.IsAlive && followSoldier.SpawnVersion == followVersion)));
            public Burst(CombatVisuals effects, Transform parent, int index)
            {
                owner = effects;
                gameObject = new GameObject("Burst " + index);
                transform = gameObject.transform; transform.SetParent(parent, false);
                sparks = effects.MakeParticles("Sparks / Ice Shards", transform, effects.glow, false);
                smoke = effects.MakeParticles("Soft Smoke", transform, effects.smoke, true);
                flash = effects.MakeParticles("Impact Flash", transform, effects.glow, false);
                sparkRenderer = sparks.GetComponent<ParticleSystemRenderer>();
                firstRing = MakeRing("Shockwave", transform, effects.ribbon);
                secondRing = MakeRing("Trailing Shockwave", transform, effects.ribbon);
                sigil = MakeRing("Spell Hex Sigil", transform, effects.ribbon, 6);
                for (int i = 0; i < beams.Length; i++)
                {
                    beams[i] = MakeRing("Summoning Light " + i, transform, effects.ribbon, 2);
                    beams[i].loop = false;
                }
            }
            public void SetupParticles(bool ice)
            {
                sparkRenderer.renderMode = ice ? ParticleSystemRenderMode.Mesh : ParticleSystemRenderMode.Billboard;
                if (ice) sparkRenderer.mesh = owner.crystalMesh;
                sparkRenderer.sharedMaterial = ice ? owner.crystal : owner.glow;
                sparks.Play(false); smoke.Play(false); flash.Play(false);
            }
            public void SetupRings(Vector3 position, float radius, Color tint, float duration)
            {
                firstRing.transform.position = position;
                secondRing.transform.position = position + Vector3.up * .015f;
                ringRadius = radius; ringTint = tint; ringDuration = duration;
                firstRing.enabled = secondRing.enabled = true;
                AnimateRings();
            }
            public void SetupSigil(Vector3 position, float radius, Color tint, int level, float height, bool hold)
            {
                sigilRadius = radius; sigilTint = tint; sigilLevel = level; beamHeight = height; holdRings = hold;
                sigil.transform.position = position;
                sigil.enabled = level >= 2;
                for (int i = 0; i < beams.Length; i++)
                {
                    beams[i].transform.position = position;
                    beams[i].enabled = height > 0f && (level == 3 || i == 0);
                }
                AnimateRings();
            }
            public void Follow(Transform target, Vector3 offset)
            {
                following = true; followTarget = target; followOffset = offset;
                followSoldier = target.GetComponent<SoldierAgent>();
                followVersion = followSoldier ? followSoldier.SpawnVersion : 0;
            }
            public void AnimateRings()
            {
                if (following && followTarget) transform.position = followTarget.position + followOffset;
                float fade = Mathf.Clamp01((lifetime - age) / .22f);
                if (sigil.enabled)
                {
                    Color rune = sigilTint; rune.a *= fade * .52f;
                    DrawRing(sigil, sigilRadius, rune, .02f + sigilLevel * .009f);
                    sigil.transform.localRotation = Quaternion.Euler(0, age * (holdRings ? 20f : 45f), 0);
                }
                float beamFade = Mathf.Clamp01(1f - age / Mathf.Min(lifetime, .45f + sigilLevel * .2f));
                for (int i = 0; i < beams.Length; i++)
                {
                    if (!beams[i].enabled) continue;
                    float angle = i * Mathf.PI * 2f / 3f;
                    Vector3 start = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * sigilRadius * .4f;
                    beams[i].SetPosition(0, start);
                    beams[i].SetPosition(1, start + Vector3.up * beamHeight);
                    Color bottom = sigilTint; bottom.a = .28f * beamFade;
                    Color top = sigilTint; top.a = 0f;
                    beams[i].startColor = bottom; beams[i].endColor = top;
                    beams[i].widthMultiplier = .045f + sigilLevel * .018f;
                }
                if (!firstRing.enabled) return;
                if (holdRings)
                {
                    Color halo = ringTint; halo.a *= .43f * fade;
                    DrawRing(firstRing, ringRadius * (1f + Mathf.Sin(age * 5f) * .025f), halo, .035f);
                    halo.a *= .65f;
                    DrawRing(secondRing, ringRadius * .7f, halo, .024f);
                    return;
                }
                float time = Mathf.Clamp01(age / ringDuration);
                if (time >= 1f) { firstRing.enabled = secondRing.enabled = false; return; }
                Color tint = ringTint; tint.a = (1f - time) * .65f;
                DrawRing(firstRing, Mathf.Lerp(.15f, ringRadius, 1f - (1f - time) * (1f - time)), tint, .11f * (1f - time) + .015f);
                float delayed = Mathf.Clamp01((time - .13f) / .87f);
                tint.a = Mathf.Sin(delayed * Mathf.PI) * .3f;
                DrawRing(secondRing, Mathf.Lerp(.15f, ringRadius * .88f, delayed), tint, .045f);
            }
            public void Clear()
            {
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                firstRing.enabled = secondRing.enabled = false;
                sigil.enabled = false;
                foreach (var beam in beams) beam.enabled = false;
                following = holdRings = false;
                followTarget = null; followSoldier = null;
                sigil.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
