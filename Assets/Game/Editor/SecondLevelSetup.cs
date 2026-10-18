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
    /// <summary>Configures only the existing canyon map; first and third map assets are preserved.</summary>
    public static class SecondLevelSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/Level02_CanyonConfluence.unity";
        const string Monsters = "Assets/ThirdParty/Quaternius/UltimateMonsters/";
        const string Kit = "Assets/ThirdParty/Kenney/TowerDefenseKit/Models/";
        const string Prefabs = "Assets/Game/Prefabs/";
        const string Icons = "Assets/Game/UI/Icons/";
        static readonly string[] TowerNames = { "Rapid_Turret", "Frost_Turret", "Cannon_Turret", "Barracks_Tower" };
        static readonly string[] EnemyNames = { "BlueDemon_Enemy", "Fast_Runner", "Heavy_Orc", "Swarm_Blob", "Breaker_Mushroom", "Flying_Ghost" };

        [MenuItem("Tools/Tower Defense/Set Up Second Playable Level")]
        public static void BuildAssetsAndConfigureSecondLevel()
        {
            RequireSavedEditMode();
            BuildSecondLevelAssets();
            GenerateSecondLevelIcons();
            WideRoadSetup.ConfigureMap(ScenePath, "Assets/Game/Maps/CanyonWideRoad",
                "Assets/Game/Materials/Kenney_Canyon_URP.mat");
            ConfigureSecondLevel();
            Debug.Log("Second level ready: eight pads, barracks and hero, six enemies, twelve continuous stages, three augment choices and frost spell.");
        }

        [MenuItem("Tools/Tower Defense/Build Second Level Assets Only")]
        public static void BuildSecondLevelAssets()
        {
            RequireSavedEditMode();
            foreach (string path in new[] { Prefabs, Icons, "Assets/Game/Animations", "Assets/Game/Materials" })
                Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            var authoring = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(authoring);
                CreateFriendly("FriendlyTribal", "Soldier_Tribal", 1.25f, false);
                CreateFriendly("FriendlyNinjaHero", "Hero_Ninja", 1.8f, true);
                CreateEnemy("GreenBlob", "Swarm_Blob", .85f, 0f, new Color(.65f, .93f, .22f));
                CreateEnemy("MushroomKing", "Breaker_Mushroom", 2.6f, 0f, new Color(1f, .25f, .25f));
                CreateEnemy("FlyingGhost", "Flying_Ghost", 1.2f, 1.3f, new Color(.72f, .5f, 1f));
                CreateBarracks();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(authoring, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Tools/Tower Defense/Configure Existing Second Level")]
        public static void ConfigureSecondLevel()
        {
            RequireSavedEditMode();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var map = roots.SelectMany(g => g.GetComponentsInChildren<LevelMap>(true)).FirstOrDefault();
            if (!map || map.routes == null || map.routes.Length != 2 || map.buildSlots == null || map.buildSlots.Length != 8)
                throw new InvalidOperationException("The canyon map requires its two saved routes and eight build pads.");
            var session = roots.SelectMany(g => g.GetComponentsInChildren<LevelSession>(true)).FirstOrDefault();
            if (!session) session = new GameObject("Gameplay").AddComponent<LevelSession>();
            var enemies = EnemyNames.Select(LoadPrefab<EnemyAgent>).ToArray();
            var towers = TowerNames.Select(LoadPrefab<TowerAgent>).ToArray();
            map.initialGold = 240; map.baseHealth = 20;
            session.levelIndex = 2;
            session.map = map; session.enemyPrefab = enemies[0]; session.towerPrefab = towers[0];
            session.projectilePrefab = LoadPrefab<ProjectileAgent>("Turret_Projectile");
            session.towerPrefabs = towers; session.towerCosts = new[] { 60, 70, 90, 110 };
            session.enemyProfiles = new[]
            {
                Profile(EnemyKind.Normal, enemies[0], 65, 2.7f, 7, 1, 1, 10),
                Profile(EnemyKind.Fast, enemies[1], 44, 4.7f, 7, 1, 1, 8),
                Profile(EnemyKind.Heavy, enemies[2], 400, 2f, 26, 4, 3, 22, .15f),
                Profile(EnemyKind.Swarm, enemies[3], 35, 3.3f, 4, 1, 1, 7),
                Profile(EnemyKind.Breaker, enemies[4], 600, 1.8f, 30, 4, 3, 28),
                Profile(EnemyKind.Flying, enemies[5], 100, 3.2f, 12, 2, 1, 10, 0f, true, 1.3f)
            };
            session.towerCost = 60; session.enemyHealth = 65; session.enemySpeed = 2.7f;
            session.killReward = 7; session.leakDamage = 1;
            session.enableAugments = true; session.augmentSeed = 0;
            session.augmentChoiceWaveIndices = new[] { 0, 4, 8 };
            session.augmentChoiceTiers = new[] { AugmentTier.Silver, AugmentTier.Gold, AugmentTier.Prismatic };
            session.frostSpellUnlocked = true; session.frostSpellCost = 18; session.frostSpellRadius = 3.8f;
            session.frostSpellDuration = 3.5f; session.frostSpellCooldown = 10f;
            session.nextLevelScene = string.Empty;
            session.waves = BuildTimeline();
            var hud = session.GetComponent<GameHud>();
            if (!hud) hud = session.gameObject.AddComponent<GameHud>();
            hud.session = session;
            hud.gameCamera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (!hud.gameCamera) throw new InvalidOperationException("The canyon map has no MainCamera.");
            hud.hudFont = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.Contains("LiberationSans SDF") ? 0 : 1)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault(f => f) ?? TMP_Settings.defaultFontAsset;
            hud.towerIcons = TowerNames.Select(LoadIcon).ToArray();
            hud.enemyIcons = EnemyNames.Select(LoadIcon).ToArray();
            MapPresentationSetup.Configure(scene);
            hud.gameCamera.rect = new Rect(0f, 0f, 1f, 1f);
            var rig = hud.gameCamera.GetComponent<MapCameraRig>();
            rig.gameplayViewport = new Rect(.025f, .1f, .95f, .77f); rig.ApplyFraming();
            var errors = session.GetConfigurationErrors();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            EditorUtility.SetDirty(map); EditorUtility.SetDirty(session); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            // Make this level accessible without adding the unfinished third map to the playable list.
            string first = "Assets/Game/Scenes/Level01_ForestOutpost.unity";
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(first, true), new EditorBuildSettingsScene(ScenePath, true)
            }.Concat(EditorBuildSettings.scenes.Where(s => s.path != first && s.path != ScenePath
                && !s.path.EndsWith("Level03_CoreFortress.unity", StringComparison.Ordinal))).ToArray();
            AssetDatabase.SaveAssets(); Selection.activeGameObject = session.gameObject;
        }

        static EnemyProfile Profile(EnemyKind kind, EnemyAgent prefab, int health, float speed,
            int reward, int mana, int leak, int melee, float armor = 0f, bool flying = false, float height = 0f)
            => new EnemyProfile
            {
                kind = kind, prefab = prefab, health = health, speed = speed, reward = reward,
                manaReward = mana, leakDamage = leak, damageReduction = armor,
                meleeDamage = melee, meleeInterval = 1f, unblockable = flying, flightHeight = height
            };

        public static WavePlan[] BuildTimeline() => new[]
        {
            Wave("Canyon patrol", 0f, false, Batch("A", EnemyKind.Normal, 14, 1.65f)),
            Wave("Two fronts", 24f, false,
                Batch("A", EnemyKind.Normal, 12, 1.15f), Batch("B", EnemyKind.Normal, 10, 1.25f, 2f)),
            Wave("The swarm", 48f, false,
                Batch("A", EnemyKind.Swarm, 12, .6f), Batch("B", EnemyKind.Normal, 8, .85f, 1f),
                Batch("B", EnemyKind.Fast, 8, .8f, 5f)),
            Wave("Armored columns", 72f, true,
                Batch("A", EnemyKind.Heavy, 2, 4f), Batch("A", EnemyKind.Normal, 8, .8f, 3f),
                Batch("B", EnemyKind.Fast, 10, .7f, 1f)),
            Wave("Eyes in the sky", 96f, true,
                Batch("A", EnemyKind.Flying, 8, .85f), Batch("B", EnemyKind.Swarm, 12, .55f, 2f),
                Batch("B", EnemyKind.Heavy, 2, 4f, 4f), Batch("A", EnemyKind.Normal, 6, .75f, 5f)),
            Wave("The siege breaker", 120f, true,
                Batch("A", EnemyKind.Breaker, 2, 4f), Batch("A", EnemyKind.Swarm, 12, .5f, 2f),
                Batch("B", EnemyKind.Swarm, 12, .5f, 3f), Batch("B", EnemyKind.Fast, 8, .65f, 6f)),
            Wave("Heavy traffic", 144f, false,
                Batch("A", EnemyKind.Heavy, 4, 3f), Batch("B", EnemyKind.Heavy, 4, 3f, 1f),
                Batch("A", EnemyKind.Normal, 8, .65f, 3f), Batch("B", EnemyKind.Fast, 10, .6f, 5f)),
            Wave("Over the barricades", 168f, false,
                Batch("A", EnemyKind.Flying, 8, .75f), Batch("B", EnemyKind.Flying, 8, .75f, 1f),
                Batch("A", EnemyKind.Swarm, 12, .45f, 3f), Batch("B", EnemyKind.Normal, 8, .65f, 4f)),
            Wave("Elite siege", 192f, true,
                Batch("A", EnemyKind.Breaker, 2, 4f), Batch("B", EnemyKind.Breaker, 2, 4f, 1f),
                Batch("A", EnemyKind.Heavy, 4, 3f, 3f), Batch("B", EnemyKind.Fast, 8, .55f, 5f),
                Batch("A", EnemyKind.Swarm, 12, .45f, 7f)),
            Wave("Relentless advance", 216f, false,
                Batch("A", EnemyKind.Flying, 8, .7f), Batch("B", EnemyKind.Fast, 10, .55f, 2f),
                Batch("B", EnemyKind.Heavy, 4, 3f, 3f), Batch("A", EnemyKind.Swarm, 12, .4f, 6f)),
            Wave("Last reserves", 240f, true,
                Batch("A", EnemyKind.Breaker, 2, 4f), Batch("A", EnemyKind.Heavy, 4, 3f, 2f),
                Batch("B", EnemyKind.Swarm, 12, .4f, 3f), Batch("B", EnemyKind.Flying, 8, .7f, 5f)),
            Wave("Final canyon siege", 264f, true,
                Batch("A", EnemyKind.Breaker, 2, 4f), Batch("B", EnemyKind.Breaker, 2, 4f, 1f),
                Batch("A", EnemyKind.Heavy, 4, 3f, 2f), Batch("B", EnemyKind.Heavy, 4, 3f, 3f),
                Batch("A", EnemyKind.Flying, 8, .7f, 5f), Batch("B", EnemyKind.Flying, 8, .7f, 6f),
                Batch("A", EnemyKind.Swarm, 12, .4f, 7f), Batch("B", EnemyKind.Swarm, 12, .4f, 8f))
        };

        static WavePlan Wave(string name, float time, bool major, params SpawnBatch[] batches)
            => new WavePlan { label = name, startTime = time, majorWave = major, batches = batches };
        static SpawnBatch Batch(string route, EnemyKind kind, int count, float interval, float delay = 0f)
            => new SpawnBatch { routeId = route, enemyKind = kind, count = count, interval = interval, delay = delay, spawnInPairs = true };

        static void CreateFriendly(string modelName, string prefabName, float height, bool hero)
        {
            var controller = ImportMonster(modelName, false);
            var root = new GameObject(prefabName);
            try
            {
                var actor = root.AddComponent<SoldierAgent>();
                var visual = CreateMonsterVisual(modelName, root.transform, height, 0f, controller);
                actor.animator = visual.GetComponent<Animator>();
                var teal = Material("Ally_Teal", new Color(.12f, .92f, .82f));
                var gold = Material("Ally_Hero_Gold", new Color(1f, .76f, .16f));
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "Friendly_Team_Ring";
                UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(root.transform, false); ring.transform.localPosition = new Vector3(0f, .015f, 0f);
                ring.transform.localScale = new Vector3(hero ? .82f : .58f, .012f, hero ? .82f : .58f);
                ring.GetComponent<Renderer>().sharedMaterial = hero ? gold : teal;
                ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                var emblem = Cube(hero ? "Hero_Gold_Crest" : "Soldier_Teal_Crest", root.transform,
                    new Vector3(0f, height + .05f, 0f), hero ? new Vector3(.24f, .24f, .06f) : new Vector3(.13f, .13f, .045f), hero ? gold : teal);
                emblem.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                // The hero's separate silhouette and gold mantle make it identifiable among the three recruits.
                if (hero) Cube("Hero_Command_Mantle", visual.transform,
                    new Vector3(0f, .8f / visual.transform.localScale.y, -.19f / visual.transform.localScale.z),
                    new Vector3(.38f, .6f, .055f) / visual.transform.localScale.y, teal);
                var bar = Group("HealthBar", root.transform); bar.localPosition = Vector3.up * (height + .3f);
                actor.healthBar = bar; actor.healthBarWidth = hero ? 1f : .72f;
                Cube("Background", bar, new Vector3(0f, 0f, .01f), new Vector3(actor.healthBarWidth + .08f, .12f, .02f),
                    Material("Ally_Health_Background", new Color(.04f, .12f, .15f), true));
                actor.healthFill = Cube("Health", bar, Vector3.zero, new Vector3(actor.healthBarWidth, .07f, .025f),
                    Material(hero ? "Ally_Health_Hero" : "Ally_Health_Recruit", hero ? new Color(1f, .83f, .28f) : new Color(.14f, 1f, .84f), true)).transform;
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + prefabName + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void CreateEnemy(string modelName, string prefabName, float height, float flightHeight, Color healthColor)
        {
            var controller = ImportMonster(modelName, true);
            var root = new GameObject(prefabName);
            try
            {
                var actor = root.AddComponent<EnemyAgent>();
                var visual = CreateMonsterVisual(modelName, root.transform, height, flightHeight, controller);
                actor.animator = visual.GetComponent<Animator>(); actor.aimOffset = Vector3.up * (height * .56f + flightHeight);
                var health = Group("HealthBar", root.transform); health.localPosition = Vector3.up * (height + .28f + flightHeight);
                var bar = health.gameObject.AddComponent<EnemyHealthBar>(); bar.enemy = actor;
                bar.width = modelName == "MushroomKing" ? 1.45f : .85f;
                Cube("Background", health, new Vector3(0f, 0f, .01f), new Vector3(bar.width + .1f, .12f, .02f),
                    Material("Health_Background", new Color(.06f, .1f, .13f), true));
                bar.fill = Cube("Health", health, Vector3.zero, new Vector3(bar.width, .075f, .025f),
                    Material("Health_" + modelName, healthColor, true)).transform;
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + prefabName + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static AnimatorController ImportMonster(string modelName, bool moveDefault)
        {
            string path = Monsters + "Models/" + modelName + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Import the selected FBX first: " + modelName);
            importer.animationType = ModelImporterAnimationType.Generic; importer.importAnimation = true;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0
                    || clip.name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0
                    || clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0
                    || clip.name.IndexOf("Fly", StringComparison.OrdinalIgnoreCase) >= 0;
                clip.lockRootPositionXZ = true; clip.lockRootHeightY = true; clip.lockRootRotation = true;
            }
            importer.clipAnimations = clips;
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Monsters + "Textures/Atlas_Monsters.png");
            if (!atlas) throw new InvalidOperationException("The existing Quaternius monster atlas is missing.");
            var material = MonsterMaterial(atlas);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Atlas"), material);
            importer.SaveAndReimport();
            var animationClips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            AnimationClip Clip(params string[] names) => names.Select(n => animationClips.FirstOrDefault(c => c.name.EndsWith(n, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(c => c);
            var moveClip = Clip("Walk", "Run", "Fly", "Flying", "Idle") ?? animationClips.FirstOrDefault();
            if (!moveClip) throw new InvalidOperationException("No animation clips found for " + modelName);
            var idleClip = Clip("Idle", "Idle1", "Stand") ?? moveClip;
            var attackClip = Clip("Attack", "Attack1", "Attack2", "Attack_1", "Punch")
                ?? animationClips.FirstOrDefault(c => c.name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0) ?? idleClip;
            string controllerPath = "Assets/Game/Animations/" + modelName + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            var move = State(machine, "Move", moveClip); move.speed = 1.15f;
            var idle = State(machine, "Idle", idleClip); State(machine, "Attack", attackClip);
            machine.defaultState = moveDefault ? move : idle;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorState State(AnimatorStateMachine machine, string name, AnimationClip clip)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
            if (!state) state = machine.AddState(name);
            state.motion = clip; return state;
        }

        static GameObject CreateMonsterVisual(string modelName, Transform parent, float height, float flightHeight, AnimatorController controller)
        {
            string path = Monsters + "Models/" + modelName + ".fbx";
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            visual.name = "Visual";
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Quaternius_Monster_URP.mat");
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            var bounds = VisibleBounds(visual); float scale = height / Mathf.Max(.01f, bounds.size.y);
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale + flightHeight, -bounds.center.z * scale);
            var animator = visual.GetComponent<Animator>(); if (!animator) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return visual;
        }

        static Material MonsterMaterial(Texture2D atlas)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Quaternius_Monster_URP.mat");
            if (material) return material;
            material = Material("Quaternius_Monster_URP", Color.white);
            material.SetTexture("_BaseMap", atlas); EditorUtility.SetDirty(material); return material;
        }

        static void CreateBarracks()
        {
            var root = new GameObject("Barracks_Tower");
            try
            {
                var tower = root.AddComponent<TowerAgent>(); tower.kind = TowerKind.Barracks;
                tower.range = 7f; tower.damage = 8; tower.fireInterval = 1.1f;
                tower.upgradeCost = 90; tower.secondUpgradeCost = 140;
                var barracks = root.AddComponent<BarracksAgent>();
                barracks.soldierPrefab = LoadPrefab<SoldierAgent>("Soldier_Tribal");
                barracks.heroPrefab = LoadPrefab<SoldierAgent>("Hero_Ninja");
                barracks.rallySearchDistance = 7f; barracks.recruitRadius = 2.5f;
                var teal = Material("Barracks_Canvas", new Color(.12f, .66f, .65f));
                var gold = Material("Barracks_Hero_Gold", new Color(1f, .73f, .16f));
                var wood = Material("Barracks_Wood", new Color(.32f, .19f, .11f));
                var stone = Material("Barracks_Stone", new Color(.48f, .5f, .49f));
                PlaceKit("tower-square-bottom-b", root.transform, new Vector3(0f, .24f, 0f), 1.55f);
                var basic = Group("BasicCamp", root.transform); var upgraded = Group("ReinforcedCamp", root.transform);
                var final = Group("HeroGarrison", root.transform);
                Tent(basic, new Vector3(-.5f, .45f, 0f), teal, wood);
                Tent(basic, new Vector3(.5f, .45f, 0f), teal, wood);
                Tent(upgraded, new Vector3(-.55f, .45f, -.35f), teal, wood);
                Tent(upgraded, new Vector3(.55f, .45f, -.35f), teal, wood);
                Tent(upgraded, new Vector3(0f, .45f, .5f), teal, wood);
                Tent(final, new Vector3(-.68f, .45f, -.3f), teal, wood);
                Tent(final, new Vector3(.68f, .45f, -.3f), teal, wood);
                PlaceKit("tower-square-middle-b", final, new Vector3(0f, .5f, .22f), 1.25f, stone);
                PlaceKit("tower-square-roof-b", final, new Vector3(0f, 1.5f, .22f), 1.3f, teal);
                for (int i = 0; i < 3; i++)
                {
                    var tier = i == 0 ? basic : i == 1 ? upgraded : final;
                    float height = i == 2 ? 2.3f : i == 1 ? 1.9f : 1.55f;
                    Cube("FlagPole", tier, new Vector3(-.86f, .45f + height * .5f, .62f), new Vector3(.055f, height, .055f), wood);
                    Cube(i == 2 ? "Hero_Banner" : "Recruit_Banner", tier, new Vector3(-.65f, .38f + height, .62f),
                        new Vector3(.44f, i == 2 ? .6f : .35f, .04f), i == 2 ? gold : teal);
                }
                tower.baseVisual = basic.gameObject; tower.upgradedVisual = upgraded.gameObject; tower.finalVisual = final.gameObject;
                upgraded.gameObject.SetActive(false); final.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "Barracks_Tower.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void Tent(Transform parent, Vector3 position, Material canvas, Material wood)
        {
            var tent = Group("Recruit_Tent", parent); tent.localPosition = position;
            PlaceKit("wood-structure", tent, Vector3.zero, .68f, wood);
            PlaceKit("tower-square-roof-b", tent, new Vector3(0f, .56f, 0f), .72f, canvas);
            Cube("Dark_Entry", tent, new Vector3(0f, .2f, .31f), new Vector3(.22f, .34f, .035f), wood);
        }

        static GameObject PlaceKit(string name, Transform parent, Vector3 position, float scale, Material material = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + name + ".fbx");
            if (!asset) throw new InvalidOperationException("Missing Kenney model: " + name);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = position; go.transform.localScale = Vector3.one * scale;
            if (material) foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
            return go;
        }

        static Transform Group(string name, Transform parent)
        { var group = new GameObject(name).transform; group.SetParent(parent, false); return group; }

        static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
            var renderer = go.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = name.StartsWith("Health", StringComparison.Ordinal) || name == "Background"
                ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return go;
        }

        static Material Material(string name, Color color, bool unlit = false)
        {
            string path = "Assets/Game/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Required URP shader is missing.");
            material = new Material(shader) { name = name, color = color, enableInstancing = true };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .2f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        static T LoadPrefab<T>(string name) where T : Component
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
            var component = asset ? asset.GetComponent<T>() : null;
            if (!component) throw new InvalidOperationException("Missing gameplay prefab: " + name);
            return component;
        }

        static Sprite LoadIcon(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Icons + name + ".png");
            if (!sprite) throw new InvalidOperationException("Generate second-level model icons first: " + name);
            return sprite;
        }

        static Bounds VisibleBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => r.gameObject.activeInHierarchy && !(r is TrailRenderer)).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Model has no renderer: " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [MenuItem("Tools/Tower Defense/Generate Second Level Model Icons")]
        public static void GenerateSecondLevelIcons()
        {
            RequireSavedEditMode();
            var previous = SceneManager.GetActiveScene();
            var authoring = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(authoring);
                foreach (string name in new[] { "Barracks_Tower", "Swarm_Blob", "Breaker_Mushroom", "Flying_Ghost" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
                    if (!prefab) throw new InvalidOperationException("Build second-level prefabs first: " + name);
                    RenderIcon(prefab, Icons + name + ".png");
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(authoring, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
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
                camera.scene = preview; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
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
                IconLight(preview, "Key", new Vector3(38f, -30f, 0f), 1.4f, new Color(1f, .94f, .84f), layer);
                IconLight(preview, "Fill", new Vector3(25f, 130f, 0f), .75f, new Color(.72f, .84f, 1f), layer);
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                image.ReadPixels(new Rect(0f, 0f, size, size), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image);
                EditorSceneManager.ClosePreviewScene(preview); texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = size; importer.SaveAndReimport();
        }

        static void SetLayer(Transform root, int layer)
        { root.gameObject.layer = layer; foreach (Transform child in root) SetLayer(child, layer); }

        static void IconLight(Scene scene, string name, Vector3 angles, float intensity, Color color, int layer)
        {
            var light = new GameObject(name).AddComponent<Light>(); SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            light.type = LightType.Directional; light.intensity = intensity; light.color = color;
            light.cullingMask = 1 << layer; light.shadows = LightShadows.None; light.transform.rotation = Quaternion.Euler(angles);
        }

        static void RequireSavedEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before configuring the canyon level.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save current scene edits before configuring the canyon level.");
        }
    }
}
