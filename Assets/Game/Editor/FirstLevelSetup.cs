using System;
using System.IO;
using System.Linq;
using TMPro;
using TowerDefense.Gameplay;
using TowerDefense.Maps;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TowerDefense.EditorTools
{
    /// <summary>Builds reusable battle assets and configures the existing first map.</summary>
    public static class FirstLevelSetup
    {
        const string Monsters = "Assets/ThirdParty/Quaternius/UltimateMonsters/";
        const string Kit = "Assets/ThirdParty/Kenney/TowerDefenseKit/Models/";
        const string Prefabs = "Assets/Game/Prefabs";
        const string Icons = "Assets/Game/UI/Icons";
        const string FirstScene = "Assets/Game/Scenes/Level01_ForestOutpost.unity";
        static readonly string[] EnemyPaths = { "BlueDemon_Enemy", "Fast_Runner", "Heavy_Orc" };
        static readonly string[] TowerPaths = { "Rapid_Turret", "Frost_Turret", "Cannon_Turret" };

        [MenuItem("Tools/Tower Defense/Set Up First Playable Level")]
        public static void SetUp() => BuildAssetsAndConfigureFirstLevel();

        [MenuItem("Tools/Tower Defense/Build Richer Battle Assets And Configure First Level")]
        public static void BuildAssetsAndConfigureFirstLevel()
        {
            RequireEditMode();
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save current scene edits before running setup.");
            BuildCombatAssets();
            GenerateIcons();
            ConfigureFirstLevelCore();
            Debug.Log("Richer first level configured: three towers with three tiers, three monsters and seven automatic waves.");
        }

        [MenuItem("Tools/Tower Defense/Build Combat Prefabs Only")]
        public static void BuildCombatAssets()
        {
            RequireEditMode();
            EnsureFolders();
            CreateEnemy("BlueDemon", EnemyPaths[0], 1.7f, false);
            CreateEnemy("RunnerNinja", EnemyPaths[1], 1.55f, true);
            CreateEnemy("HeavyOrcSkull", EnemyPaths[2], 2.8f, false);
            for (int i = 0; i < TowerPaths.Length; i++) CreateTower((TowerKind)i);
            CreateProjectile();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Tower Defense/Configure Existing First Level")]
        public static void ConfigureFirstLevel()
        {
            RequireEditMode();
            if (SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save current scene edits before configuring the first level.");
            ConfigureFirstLevelCore();
        }

        static void ConfigureFirstLevelCore()
        {
            var scene = EditorSceneManager.OpenScene(FirstScene, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var map = roots.SelectMany(g => g.GetComponentsInChildren<LevelMap>(true)).FirstOrDefault();
            if (!map) throw new InvalidOperationException("The first level has no LevelMap.");
            if (map.routes == null || !map.routes.Any(r => r && r.routeId == "B"))
                throw new InvalidOperationException("Save the first level's new route B before configuring mixed waves.");
            var session = roots.SelectMany(g => g.GetComponentsInChildren<LevelSession>(true)).FirstOrDefault();
            if (!session) session = new GameObject("Gameplay").AddComponent<LevelSession>();
            var enemies = EnemyPaths.Select(n => LoadPrefab<EnemyAgent>(n)).ToArray();
            var towers = TowerPaths.Select(n => LoadPrefab<TowerAgent>(n)).ToArray();
            map.initialGold = 180;
            session.map = map; session.enemyPrefab = enemies[0]; session.towerPrefab = towers[0];
            session.projectilePrefab = LoadPrefab<ProjectileAgent>("Turret_Projectile");
            session.towerPrefabs = towers; session.towerCosts = new[] { 60, 70, 90 };
            session.enemyProfiles = new[]
            {
                Profile(EnemyKind.Normal, enemies[0], 54, 2.6f, 6, 1, 0f),
                Profile(EnemyKind.Fast, enemies[1], 38, 4.6f, 6, 1, 0f),
                Profile(EnemyKind.Heavy, enemies[2], 330, 1.9f, 24, 3, .15f)
            };
            session.towerCost = 60; session.enemyHealth = 54; session.enemySpeed = 2.6f;
            session.killReward = 6; session.leakDamage = 1;
            session.enableAugments = true; session.augmentSeed = 0;
            session.waves = BuildTimeline();
            var hud = session.GetComponent<GameHud>();
            if (!hud) hud = session.gameObject.AddComponent<GameHud>();
            hud.session = session;
            hud.gameCamera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (!hud.gameCamera) throw new InvalidOperationException("The first level has no MainCamera.");
            hud.hudFont = FindHudFont();
            hud.towerIcons = TowerPaths.Select(LoadIcon).ToArray();
            hud.enemyIcons = EnemyPaths.Select(LoadIcon).ToArray();
            var errors = session.GetConfigurationErrors();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            EditorUtility.SetDirty(map); EditorUtility.SetDirty(session); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(FirstScene, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != FirstScene)).ToArray();
            AssetDatabase.SaveAssets(); Selection.activeGameObject = session.gameObject;
        }

        static EnemyProfile Profile(EnemyKind kind, EnemyAgent prefab, int health, float speed,
            int reward, int leakDamage, float damageReduction) => new EnemyProfile
        {
            kind = kind, prefab = prefab, health = health, speed = speed,
            reward = reward, leakDamage = leakDamage, damageReduction = damageReduction,
            manaReward = kind == EnemyKind.Heavy ? 4 : 1
        };

        static WavePlan[] BuildTimeline() => new[]
        {
            Wave("First contact", 0f, false, Batch("A", EnemyKind.Normal, 10, 1.8f)),
            Wave("A second entrance", 24f, false,
                Batch("A", EnemyKind.Normal, 8, 1.2f), Batch("B", EnemyKind.Normal, 6, 1.5f, 3f)),
            Wave("The runners", 48f, false,
                Batch("A", EnemyKind.Normal, 10, .85f), Batch("B", EnemyKind.Fast, 8, .9f, 2f)),
            Wave("Armored advance", 72f, true,
                Batch("A", EnemyKind.Heavy, 2, 5f), Batch("A", EnemyKind.Normal, 8, .8f, 3f),
                Batch("B", EnemyKind.Fast, 8, .75f, 1f)),
            Wave("Pressure shifts", 96f, false,
                Batch("A", EnemyKind.Normal, 6, .75f), Batch("A", EnemyKind.Fast, 4, .6f, 4f),
                Batch("B", EnemyKind.Heavy, 2, 4f), Batch("B", EnemyKind.Normal, 6, .8f, 2f),
                Batch("B", EnemyKind.Fast, 4, .65f, 6f)),
            Wave("Both fronts", 120f, false,
                Batch("A", EnemyKind.Heavy, 2, 4f), Batch("B", EnemyKind.Heavy, 2, 4f, 1f),
                Batch("A", EnemyKind.Normal, 8, .7f, 2f), Batch("B", EnemyKind.Normal, 8, .7f, 3f),
                Batch("A", EnemyKind.Fast, 6, .55f, 7f), Batch("B", EnemyKind.Fast, 6, .55f, 8f)),
            Wave("Final assault", 144f, true,
                Batch("A", EnemyKind.Heavy, 3, 5f), Batch("B", EnemyKind.Heavy, 3, 5f, 1f),
                Batch("A", EnemyKind.Normal, 10, .6f, 2f), Batch("B", EnemyKind.Normal, 10, .6f, 3f),
                Batch("A", EnemyKind.Fast, 8, .5f, 8f), Batch("B", EnemyKind.Fast, 8, .5f, 9f))
        };

        static WavePlan Wave(string label, float start, bool major, params SpawnBatch[] batches) => new WavePlan
        { label = label, startTime = start, majorWave = major, batches = batches };

        static SpawnBatch Batch(string route, EnemyKind kind, int count, float interval, float delay = 0f) => new SpawnBatch
        { routeId = route, enemyKind = kind, count = count, interval = interval * .9f, delay = delay };

        static void RequireEditMode()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before setup.");
        }

        static void EnsureFolders()
        {
            foreach (string path in new[] { Prefabs, Icons, "Assets/Game/Animations", "Assets/Game/Materials" })
                Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
        }

        static T LoadPrefab<T>(string name) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + ".prefab");
            var component = prefab ? prefab.GetComponent<T>() : null;
            if (!component) throw new InvalidOperationException("Missing combat prefab: " + name);
            return component;
        }

        static Sprite LoadIcon(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + "/" + name + ".png");
            if (!sprite) throw new InvalidOperationException("Generate the model icon first: " + name);
            return sprite;
        }

        static TMP_FontAsset FindHudFont()
        {
            var font = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.Contains("LiberationSans SDF") ? 0 : 1)
                .Select(p => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p)).FirstOrDefault(f => f);
            if (!font) font = TMP_Settings.defaultFontAsset;
            if (!font) throw new InvalidOperationException("Import TextMeshPro Essential Resources before configuring the HUD.");
            return font;
        }

        static Material Lit(string name, Color color, Texture texture = null, bool unlit = false)
        {
            string path = "Assets/Game/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("The required URP shader is unavailable.");
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader; material.color = color; material.enableInstancing = true;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateEnemy(string modelName, string prefabName, float height, bool runner)
        {
            string modelPath = Monsters + "Models/" + modelName + ".fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Import the monster FBX first: " + modelName);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.importAnimation = true;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.EndsWith("Walk") || clip.name.EndsWith("Idle") || clip.name.EndsWith("Run");
                clip.lockRootPositionXZ = true; clip.lockRootHeightY = true; clip.lockRootRotation = true;
            }
            importer.clipAnimations = clips;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Monsters + "Textures/Atlas_Monsters.png");
            if (!atlas) throw new InvalidOperationException("Monster atlas is missing.");
            var material = Lit("Quaternius_Monster_URP", Color.white, atlas);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Atlas"), material);
            importer.SaveAndReimport();
            string controllerPath = "Assets/Game/Animations/" + modelName + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var move = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Move");
            if (!move) move = machine.AddState("Move");
            var animations = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            move.motion = animations.FirstOrDefault(c => c.name.EndsWith(runner ? "Run" : "Walk"))
                ?? animations.FirstOrDefault(c => c.name.EndsWith("Walk"))
                ?? animations.FirstOrDefault(c => c.name.EndsWith("Run"));
            if (!move.motion) throw new InvalidOperationException("The monster has no movement clip: " + modelName);
            move.speed = runner ? 1.1f : 1.2f; machine.defaultState = move;
            EditorUtility.SetDirty(controller);
            var root = new GameObject(prefabName);
            try
            {
                var actor = root.AddComponent<EnemyAgent>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                visual.name = "Visual";
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                }
                Bounds bounds = VisibleBounds(visual);
                float scale = height / Mathf.Max(.01f, bounds.size.y);
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
                var animator = visual.GetComponent<Animator>(); if (!animator) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                actor.animator = animator; actor.aimOffset = new Vector3(0f, height * .56f, 0f);
                actor.DamageReduction = modelName == "HeavyOrcSkull" ? .15f : 0f;
                var healthRoot = Group("HealthBar", root.transform); healthRoot.localPosition = new Vector3(0f, height + .28f, 0f);
                var bar = healthRoot.gameObject.AddComponent<EnemyHealthBar>(); bar.enemy = actor;
                bar.width = modelName == "HeavyOrcSkull" ? 1.4f : .95f;
                BarCube("Background", healthRoot, new Vector3(0f, 0f, .01f), new Vector3(bar.width + .1f, .12f, .02f),
                    Lit("Health_Background", new Color(.06f, .1f, .13f), null, true));
                bar.fill = BarCube("Health", healthRoot, Vector3.zero, new Vector3(bar.width, .075f, .025f),
                    Lit(modelName == "HeavyOrcSkull" ? "Health_Heavy" : "Health_Fill",
                        modelName == "HeavyOrcSkull" ? new Color(1f, .48f, .24f) : new Color(.25f, .94f, .62f), null, true)).transform;
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + prefabName + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Transform Group(string name, Transform parent)
        {
            var result = new GameObject(name).transform; result.SetParent(parent, false); return result;
        }

        static GameObject KitModel(string name, Transform parent, Vector3 position, float scale, Material material = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".fbx");
            if (!prefab) throw new InvalidOperationException("Missing Kenney model: " + name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = position; instance.transform.localScale = Vector3.one * scale;
            if (material)
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
            return instance;
        }

        static void CreateTower(TowerKind kind)
        {
            var root = new GameObject(TowerPaths[(int)kind]);
            try
            {
                var tower = root.AddComponent<TowerAgent>(); tower.kind = kind;
                tower.damage = kind == TowerKind.Gun ? 10 : kind == TowerKind.Frost ? 6 : 18;
                tower.fireInterval = kind == TowerKind.Gun ? .6f : kind == TowerKind.Frost ? .85f : 1.25f;
                tower.range = kind == TowerKind.Gun ? 5.5f : kind == TowerKind.Frost ? 5.8f : 6f;
                tower.upgradeCost = kind == TowerKind.Gun ? 50 : kind == TowerKind.Frost ? 60 : 75;
                tower.secondUpgradeCost = kind == TowerKind.Gun ? 80 : kind == TowerKind.Frost ? 90 : 110;
                string baseModel = "tower-round-bottom-" + (kind == TowerKind.Gun ? "a" : kind == TowerKind.Frost ? "b" : "c");
                KitModel(baseModel, root.transform, new Vector3(0f, .25f, 0f), 1.4f);
                var pivot = Group("AimPivot", root.transform); pivot.localPosition = new Vector3(0f, 1.09f, 0f);
                var basic = Group("BasicWeapon", pivot); var upgraded = Group("UpgradedWeapon", pivot);
                var final = Group("FinalWeapon", pivot);
                tower.baseVisual = basic.gameObject; tower.upgradedVisual = upgraded.gameObject;
                tower.finalVisual = final.gameObject; tower.yawPivot = pivot;
                var muzzle = Group("Muzzle", pivot); tower.muzzle = muzzle;
                var upgradedMuzzle = Group("UpgradedMuzzle", pivot); tower.upgradedMuzzle = upgradedMuzzle;
                var finalMuzzle = Group("FinalMuzzle", pivot); tower.finalMuzzle = finalMuzzle;
                var armor = Lit("Tower_Upgrade_Armor", new Color(.2f, .29f, .37f));
                var gold = Lit("Tower_Upgrade_Gold", new Color(1f, .7f, .15f));
                foreach (Transform tier in new[] { upgraded, final }) for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * .5f;
                    float radius = tier == final ? .83f : .68f;
                    var plate = BarCube("ArmorPlate_" + i, tier,
                        new Vector3(Mathf.Sin(angle) * radius, -.2f, Mathf.Cos(angle) * radius),
                        tier == final ? new Vector3(.68f, .45f, .22f) : new Vector3(.48f, .3f, .16f), armor);
                    plate.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                    BarCube("GoldRivet_" + i, tier,
                        new Vector3(Mathf.Sin(angle) * (radius + .08f), -.16f, Mathf.Cos(angle) * (radius + .08f)),
                        Vector3.one * (tier == final ? .2f : .14f), gold);
                }
                var crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder); crown.name = "HeavyTurntable";
                UnityEngine.Object.DestroyImmediate(crown.GetComponent<Collider>());
                crown.transform.SetParent(final, false); crown.transform.localPosition = new Vector3(0f, -.22f, 0f);
                crown.transform.localScale = new Vector3(2.05f, .14f, 2.05f);
                crown.GetComponent<Renderer>().sharedMaterial = armor;
                if (kind == TowerKind.Gun)
                {
                    KitModel("weapon-turret", basic, Vector3.zero, 1.8f);
                    KitModel("weapon-turret", upgraded, new Vector3(-.3f, .15f, 0f), 1.7f);
                    KitModel("weapon-turret", upgraded, new Vector3(.3f, .15f, 0f), 1.7f);
                    BarCube("UpperArmor", upgraded, new Vector3(0f, .52f, -.2f), new Vector3(.9f, .14f, .6f), armor);
                    KitModel("weapon-turret", final, new Vector3(-.5f, .2f, 0f), 1.65f);
                    KitModel("weapon-turret", final, new Vector3(.5f, .2f, 0f), 1.65f);
                    KitModel("weapon-turret", final, new Vector3(0f, .48f, .05f), 1.85f);
                    BarCube("TripleGunBrace", final, new Vector3(0f, .73f, -.33f), new Vector3(1.42f, .25f, .58f), armor);
                    BarCube("TargetingStrip", final, new Vector3(0f, .86f, -.04f), new Vector3(.68f, .09f, .12f), gold);
                    muzzle.localPosition = new Vector3(0f, .69f, .58f);
                    upgradedMuzzle.localPosition = new Vector3(0f, .81f, .56f);
                    finalMuzzle.localPosition = new Vector3(0f, 1.19f, .68f);
                }
                else if (kind == TowerKind.Frost)
                {
                    var ice = Lit("Tower_Frost_Crystal", new Color(.18f, .76f, 1f));
                    var paleIce = Lit("Tower_Frost_Upgrade", new Color(.68f, .96f, 1f));
                    KitModel("tower-round-crystals", basic, new Vector3(0f, -.15f, 0f), 1.6f, ice);
                    KitModel("tower-round-crystals", upgraded, new Vector3(0f, -.05f, 0f), 2f, paleIce);
                    KitModel("detail-crystal-large", upgraded, new Vector3(0f, .38f, 0f), 1.35f, ice);
                    for (int i = 0; i < 4; i++)
                    {
                        float angle = i * Mathf.PI * .5f;
                        KitModel("detail-crystal", upgraded,
                            new Vector3(Mathf.Sin(angle) * .65f, -.05f, Mathf.Cos(angle) * .65f), .85f, paleIce);
                    }
                    KitModel("tower-round-crystals", final, new Vector3(0f, -.12f, 0f), 2.1f, paleIce);
                    KitModel("detail-crystal-large", final, new Vector3(0f, .42f, 0f), 2.05f, ice);
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI / 3f;
                        KitModel("detail-crystal", final,
                            new Vector3(Mathf.Sin(angle), .38f, Mathf.Cos(angle)), .9f, paleIce);
                        BarCube("CrystalBrace_" + i, final,
                            new Vector3(Mathf.Sin(angle) * .88f, .17f, Mathf.Cos(angle) * .88f), Vector3.one * .16f, gold);
                    }
                    muzzle.localPosition = new Vector3(0f, .8f, .1f);
                    upgradedMuzzle.localPosition = new Vector3(0f, 1.15f, .1f);
                    finalMuzzle.localPosition = new Vector3(0f, 1.55f, .1f);
                }
                else
                {
                    KitModel("weapon-cannon", basic, Vector3.zero, 1.75f);
                    KitModel("weapon-cannon", upgraded, new Vector3(-.42f, .12f, 0f), 1.65f);
                    KitModel("weapon-cannon", upgraded, new Vector3(.42f, .12f, 0f), 1.65f);
                    BarCube("BreechArmor", upgraded, new Vector3(0f, .18f, -.5f), new Vector3(1.1f, .6f, .28f), armor);
                    KitModel("weapon-cannon", final, new Vector3(-.56f, .16f, -.04f), 1.72f);
                    KitModel("weapon-cannon", final, new Vector3(.56f, .16f, -.04f), 1.72f);
                    KitModel("weapon-cannon", final, new Vector3(0f, .24f, .07f), 2.05f);
                    BarCube("BlastBreechArmor", final, new Vector3(0f, .4f, -.58f), new Vector3(1.7f, .85f, .32f), armor);
                    BarCube("BlastGoldCrest", final, new Vector3(0f, .76f, -.38f), new Vector3(.88f, .1f, .2f), gold);
                    muzzle.localPosition = new Vector3(0f, .68f, .6f);
                    upgradedMuzzle.localPosition = new Vector3(0f, .76f, .58f);
                    finalMuzzle.localPosition = new Vector3(0f, 1.03f, .73f);
                }
                upgraded.gameObject.SetActive(false); final.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + TowerPaths[(int)kind] + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void CreateProjectile()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere); root.name = "Turret_Projectile";
            try
            {
                UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>()); root.transform.localScale = Vector3.one * .18f;
                var material = Lit("Projectile_Gold", Color.white, null, true); root.GetComponent<Renderer>().sharedMaterial = material;
                var trail = root.AddComponent<TrailRenderer>(); trail.sharedMaterial = material; trail.time = .1f;
                trail.startWidth = .09f; trail.endWidth = .01f; trail.minVertexDistance = .1f;
                trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
                var projectile = root.AddComponent<ProjectileAgent>(); projectile.speed = 26f;
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/Turret_Projectile.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static GameObject BarCube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = name.StartsWith("Health") || name == "Background" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.receiveShadows = false; return go;
        }

        static Bounds VisibleBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.activeInHierarchy && !(r is TrailRenderer)).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no visible renderer: " + root.name);
            var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }

        [MenuItem("Tools/Tower Defense/Generate Battle Model Icons")]
        public static void GenerateIcons()
        {
            RequireEditMode(); EnsureFolders();
            foreach (string name in TowerPaths.Concat(EnemyPaths))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + name + ".prefab");
                if (!prefab) throw new InvalidOperationException("Build the combat prefab before generating its icon: " + name);
                RenderIcon(prefab, Icons + "/" + name + ".png");
            }
            AssetDatabase.SaveAssets();
        }

        static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer; foreach (Transform child in root) SetLayer(child, layer);
        }

        static void RenderIcon(GameObject prefab, string path)
        {
            const int size = 256, layer = 31;
            var preview = EditorSceneManager.NewPreviewScene();
            var texture = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false); var previous = RenderTexture.active;
            try
            {
                var model = UnityEngine.Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(model, preview);
                model.transform.position = Vector3.zero; model.transform.rotation = Quaternion.identity;
                var health = model.transform.Find("HealthBar"); if (health) health.gameObject.SetActive(false);
                SetLayer(model.transform, layer);
                foreach (var animator in model.GetComponentsInChildren<Animator>())
                {
                    if (!animator.runtimeAnimatorController) continue;
                    animator.Rebind(); int move = Animator.StringToHash("Move");
                    if (animator.HasState(0, move)) animator.Play(move, 0, .15f); animator.Update(0f);
                }
                var camera = new GameObject("Icon Camera").AddComponent<Camera>(); SceneManager.MoveGameObjectToScene(camera.gameObject, preview);
                camera.scene = preview;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.cullingMask = 1 << layer; camera.orthographic = true; camera.allowHDR = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                camera.transform.rotation = Quaternion.Euler(24f, 148f, 0f);
                var bounds = VisibleBounds(model); float extent = .01f;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                    extent = Mathf.Max(extent, Mathf.Abs(Vector3.Dot(corner, camera.transform.up)), Mathf.Abs(Vector3.Dot(corner, camera.transform.right)));
                }
                camera.orthographicSize = extent * 1.12f; float distance = Mathf.Max(8f, bounds.size.magnitude * 3f);
                camera.transform.position = bounds.center - camera.transform.forward * distance;
                camera.nearClipPlane = .05f; camera.farClipPlane = distance * 3f;
                AddIconLight(preview, "Key", new Vector3(38f, -30f, 0f), 1.4f, new Color(1f, .94f, .84f), layer);
                AddIconLight(preview, "Fill", new Vector3(25f, 130f, 0f), .75f, new Color(.72f, .84f, 1f), layer);
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                image.ReadPixels(new Rect(0f, 0f, size, size), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(preview);
                texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 256f; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256; importer.SaveAndReimport();
        }

        static void AddIconLight(Scene preview, string name, Vector3 angles, float intensity, Color color, int layer)
        {
            var light = new GameObject(name).AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, preview);
            light.type = LightType.Directional; light.intensity = intensity; light.color = color;
            light.cullingMask = 1 << layer; light.shadows = LightShadows.None; light.transform.rotation = Quaternion.Euler(angles);
        }
    }
}
