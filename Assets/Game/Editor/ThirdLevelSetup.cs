using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using TowerDefense.Gameplay;
using TowerDefense.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense.EditorTools
{
    /// <summary>Turns the existing snow fortress map into the final, independent level.</summary>
    public static class ThirdLevelSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/Level03_CoreFortress.unity";
        const string Prefabs = "Assets/Game/Prefabs/";
        const string Icons = "Assets/Game/UI/Icons/";
        static readonly string[] TowerNames =
            { "Rapid_Turret", "Frost_Turret", "Cannon_Turret", "Barracks_Tower", "Support_Tower" };
        static readonly string[] EnemyNames =
        {
            "BlueDemon_Enemy", "Fast_Runner", "Heavy_Orc", "Swarm_Blob", "Breaker_Mushroom", "Flying_Ghost",
            "Shaman_Wizard", "Boss_Warden", "Boss_Titan"
        };

        [MenuItem("Tools/Tower Defense/Set Up Third Playable Level")]
        public static void BuildAssetsAndConfigureThirdLevel()
        {
            RequireSavedEditMode();
            BuildThirdLevelAssets();
            GenerateThirdLevelIcons();
            // The original snow meshes supply their own atlas UVs; the shared atlas preserves white snow.
            WideRoadSetup.ConfigureMap(ScenePath, "Assets/Game/Maps/SnowWideRoad",
                "Assets/Game/Materials/Kenney_Default_URP.mat");
            ConfigureThirdLevel();
            Debug.Log("Third level ready on the snow fortress: ten pads, five towers, four spells, eighteen stages and two bosses.");
        }

        [MenuItem("Tools/Tower Defense/Build Third Level Assets Only")]
        public static void BuildThirdLevelAssets()
        {
            RequireSavedEditMode();
            foreach (string folder in new[] { Prefabs, Icons, "Assets/Game/Animations", "Assets/Game/Materials" })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            var authoring = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(authoring);
                Reuse("CreateEnemy", "SnowShaman", "Shaman_Wizard", 1.4f, 0f, new Color(.7f, .36f, 1f));
                Reuse("CreateEnemy", "BossYeti", "Boss_Warden", 3.4f, 0f, new Color(.25f, .8f, 1f));
                Reuse("CreateEnemy", "BossDino", "Boss_Titan", 4.4f, 0f, new Color(1f, .3f, .2f));
                MarkBoss("Boss_Warden", 2.2f, new Color(.25f, .8f, 1f));
                MarkBoss("Boss_Titan", 2.8f, new Color(1f, .3f, .2f));
                CreateSupportTower();
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(authoring, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Tools/Tower Defense/Configure Existing Third Level")]
        public static void ConfigureThirdLevel()
        {
            RequireSavedEditMode();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var map = roots.SelectMany(g => g.GetComponentsInChildren<LevelMap>(true)).FirstOrDefault();
            if (!map || !map.levelId.Contains("Level03") || map.routes == null || map.routes.Length != 2
                || map.buildSlots == null || map.buildSlots.Length != 10)
                throw new InvalidOperationException("The snow fortress requires its original two routes and ten saved build pads.");
            var session = roots.SelectMany(g => g.GetComponentsInChildren<LevelSession>(true)).FirstOrDefault();
            if (!session) session = new GameObject("Gameplay").AddComponent<LevelSession>();
            var enemies = EnemyNames.Select(LoadPrefab<EnemyAgent>).ToArray();
            var towers = TowerNames.Select(LoadPrefab<TowerAgent>).ToArray();
            map.initialGold = 400; map.baseHealth = 25;
            session.levelIndex = 3;
            session.map = map; session.enemyPrefab = enemies[0]; session.towerPrefab = towers[0];
            session.projectilePrefab = LoadPrefab<ProjectileAgent>("Turret_Projectile");
            session.towerPrefabs = towers; session.towerCosts = new[] { 60, 70, 90, 110, 100 };
            session.enemyProfiles = new[]
            {
                Profile(EnemyKind.Normal, enemies[0], 85, 2.8f, 8, 1, 1, 12),
                Profile(EnemyKind.Fast, enemies[1], 58, 4.8f, 8, 1, 1, 10),
                Profile(EnemyKind.Heavy, enemies[2], 460, 2f, 28, 4, 3, 25, .15f),
                Profile(EnemyKind.Swarm, enemies[3], 44, 3.5f, 4, 1, 1, 8),
                Profile(EnemyKind.Breaker, enemies[4], 720, 1.8f, 34, 4, 3, 32),
                Profile(EnemyKind.Flying, enemies[5], 135, 3.3f, 14, 2, 1, 12, 0f, true, 1.3f),
                Profile(EnemyKind.Shaman, enemies[6], 280, 2.1f, 20, 3, 2, 16, .08f),
                BossProfile(EnemyKind.BossWarden, enemies[7], 2600, .9f, 160, 20, 8, 45, .12f, .35f),
                BossProfile(EnemyKind.BossTitan, enemies[8], 5000, .8f, 240, 30, 12, 65, .18f, .25f)
            };
            session.towerCost = 60; session.enemyHealth = 85; session.enemySpeed = 2.8f;
            session.killReward = 8; session.leakDamage = 1;
            session.enableAugments = true; session.augmentSeed = 0;
            session.augmentChoiceWaveIndices = new[] { 0, 6, 12 };
            session.augmentChoiceTiers = new[] { AugmentTier.Silver, AugmentTier.Gold, AugmentTier.Prismatic };
            session.frostSpellUnlocked = true; session.frostSpellCost = 18; session.frostSpellRadius = 3.8f;
            session.frostSpellDuration = 3.5f; session.frostSpellCooldown = 10f;
            session.reinforcementsUnlocked = true; session.battlecryUnlocked = true;
            session.reinforcementPrefab = LoadPrefab<SoldierAgent>("Soldier_Tribal");
            session.nextLevelScene = string.Empty;
            session.waves = BuildTimeline();

            var hud = session.GetComponent<GameHud>();
            if (!hud) hud = session.gameObject.AddComponent<GameHud>();
            hud.session = session;
            hud.gameCamera = roots.SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (!hud.gameCamera) throw new InvalidOperationException("The snow map has no MainCamera.");
            hud.hudFont = AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.Contains("LiberationSans SDF") ? 0 : 1)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault(f => f) ?? TMP_Settings.defaultFontAsset;
            hud.towerIcons = TowerNames.Select(LoadIcon).ToArray(); hud.enemyIcons = EnemyNames.Select(LoadIcon).ToArray();
            MapPresentationSetup.Configure(scene);
            hud.gameCamera.rect = new Rect(0f, 0f, 1f, 1f);
            var rig = hud.gameCamera.GetComponent<MapCameraRig>();
            rig.alignToViewportCenter = true; rig.routeFramingHeight = 5f;
            rig.gameplayViewport = new Rect(.025f, .20f, .70f, .57f); rig.ApplyFraming();

            var errors = session.GetConfigurationErrors();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("; ", errors));
            EditorUtility.SetDirty(map); EditorUtility.SetDirty(session); EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            string[] playable =
            {
                "Assets/Game/Scenes/Level01_ForestOutpost.unity",
                "Assets/Game/Scenes/Level02_CanyonConfluence.unity", ScenePath
            };
            EditorBuildSettings.scenes = playable.Select(p => new EditorBuildSettingsScene(p, true))
                .Concat(EditorBuildSettings.scenes.Where(s => !playable.Contains(s.path))).ToArray();
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

        static EnemyProfile BossProfile(EnemyKind kind, EnemyAgent prefab, int health, float speed,
            int reward, int mana, int leak, int melee, float armor, float controlMultiplier)
        {
            var profile = Profile(kind, prefab, health, speed, reward, mana, leak, melee, armor);
            profile.boss = true; profile.controlDurationMultiplier = controlMultiplier; profile.enrageHealthFraction = .4f;
            return profile;
        }

        public static WavePlan[] BuildTimeline() => new[]
        {
            Wave("Snowbound patrol", 0f, false,
                Batch("A", EnemyKind.Normal, 12, 1.4f), Batch("B", EnemyKind.Normal, 8, 1.5f, 3f)),
            Wave("Frozen crossroads", 20f, false,
                Batch("B", EnemyKind.Normal, 12, 1.05f), Batch("A", EnemyKind.Fast, 8, 1f, 3f)),
            Wave("Icebound swarm", 40f, false,
                Batch("A", EnemyKind.Swarm, 14, .55f), Batch("B", EnemyKind.Swarm, 14, .55f, 2f),
                Batch("B", EnemyKind.Heavy, 2, 4f, 4f)),
            Wave("The frost shamans", 60f, true,
                Batch("A", EnemyKind.Shaman, 2, 4f), Batch("A", EnemyKind.Normal, 12, .7f, 2f),
                Batch("B", EnemyKind.Fast, 10, .65f, 3f)),
            Wave("Sky and steel", 80f, true,
                Batch("A", EnemyKind.Flying, 8, .9f), Batch("B", EnemyKind.Heavy, 4, 3f, 2f),
                Batch("B", EnemyKind.Swarm, 12, .5f, 4f)),
            Wave("BOSS: The Frost Warden", 100f, true,
                Batch("A", EnemyKind.BossWarden, 1, 1f), Batch("A", EnemyKind.Normal, 10, 1f, 4f),
                Batch("B", EnemyKind.Shaman, 2, 5f, 2f), Batch("B", EnemyKind.Swarm, 14, .6f, 5f)),
            Wave("The golden counterattack", 120f, true,
                Batch("B", EnemyKind.Breaker, 2, 4f), Batch("A", EnemyKind.Fast, 12, .65f, 2f),
                Batch("B", EnemyKind.Swarm, 12, .5f, 4f)),
            Wave("Winter wings", 140f, false,
                Batch("A", EnemyKind.Flying, 10, .7f), Batch("B", EnemyKind.Flying, 8, .8f, 2f),
                Batch("A", EnemyKind.Shaman, 2, 4f, 3f), Batch("B", EnemyKind.Normal, 10, .65f, 4f)),
            Wave("Armored blizzard", 160f, true,
                Batch("A", EnemyKind.Heavy, 4, 3f), Batch("B", EnemyKind.Heavy, 4, 3f, 1f),
                Batch("A", EnemyKind.Swarm, 14, .45f, 2f), Batch("B", EnemyKind.Fast, 12, .6f, 4f)),
            Wave("Shaman siege", 180f, false,
                Batch("A", EnemyKind.Shaman, 4, 3f), Batch("B", EnemyKind.Breaker, 2, 4f, 2f),
                Batch("A", EnemyKind.Normal, 12, .6f, 3f), Batch("B", EnemyKind.Swarm, 14, .45f, 4f)),
            Wave("Unbroken columns", 200f, false,
                Batch("A", EnemyKind.Breaker, 2, 4f), Batch("B", EnemyKind.Breaker, 2, 4f, 1f),
                Batch("A", EnemyKind.Flying, 8, .65f, 3f), Batch("B", EnemyKind.Fast, 12, .55f, 4f)),
            Wave("Under the northern lights", 220f, false,
                Batch("A", EnemyKind.Swarm, 16, .4f), Batch("B", EnemyKind.Swarm, 16, .4f, 1f),
                Batch("A", EnemyKind.Shaman, 2, 4f, 3f), Batch("B", EnemyKind.Flying, 10, .65f, 4f)),
            Wave("Prismatic resolve", 240f, true,
                Batch("A", EnemyKind.Heavy, 4, 2.6f), Batch("B", EnemyKind.Shaman, 4, 2.7f, 1f),
                Batch("A", EnemyKind.Fast, 12, .5f, 3f), Batch("B", EnemyKind.Swarm, 14, .4f, 4f)),
            Wave("The fortress surround", 260f, false,
                Batch("A", EnemyKind.Breaker, 2, 4f), Batch("B", EnemyKind.Heavy, 4, 3f, 1f),
                Batch("A", EnemyKind.Flying, 10, .6f, 3f), Batch("B", EnemyKind.Fast, 14, .5f, 4f)),
            Wave("Last shaman circle", 280f, true,
                Batch("A", EnemyKind.Shaman, 4, 2.5f), Batch("B", EnemyKind.Shaman, 4, 2.5f, 1f),
                Batch("A", EnemyKind.Swarm, 16, .4f, 3f), Batch("B", EnemyKind.Flying, 10, .6f, 4f)),
            Wave("The ground trembles", 300f, false,
                Batch("A", EnemyKind.Breaker, 2, 4f), Batch("B", EnemyKind.Breaker, 2, 4f, 1f),
                Batch("A", EnemyKind.Heavy, 4, 2.7f, 2f), Batch("B", EnemyKind.Fast, 12, .5f, 4f),
                Batch("A", EnemyKind.Swarm, 14, .4f, 5f)),
            Wave("Storm before the titan", 320f, true,
                Batch("A", EnemyKind.Flying, 10, .65f), Batch("B", EnemyKind.Flying, 10, .65f, 1f),
                Batch("A", EnemyKind.Shaman, 2, 4f, 3f), Batch("B", EnemyKind.Swarm, 14, .4f, 4f)),
            Wave("FINAL BOSS: The Winter Titan", 340f, true,
                Batch("B", EnemyKind.BossTitan, 1, 1f), Batch("A", EnemyKind.Heavy, 4, 3f, 3f),
                Batch("B", EnemyKind.Shaman, 2, 5f, 5f), Batch("A", EnemyKind.Fast, 12, .6f, 6f),
                Batch("A", EnemyKind.Swarm, 16, .45f, 7f), Batch("B", EnemyKind.Swarm, 14, .5f, 8f))
        };

        static WavePlan Wave(string label, float time, bool major, params SpawnBatch[] batches)
            => new WavePlan { label = label, startTime = time, majorWave = major, batches = batches };
        static SpawnBatch Batch(string route, EnemyKind kind, int count, float interval, float delay = 0f)
            => new SpawnBatch { routeId = route, enemyKind = kind, count = count, interval = interval, delay = delay, spawnInPairs = true };

        static void CreateSupportTower()
        {
            var root = new GameObject("Support_Tower");
            try
            {
                var tower = root.AddComponent<TowerAgent>(); tower.kind = TowerKind.Support;
                tower.range = 4.5f; tower.damage = 1; tower.fireInterval = 1f;
                tower.upgradeCost = 85; tower.secondUpgradeCost = 130;
                root.AddComponent<SupportTowerAgent>();
                var stone = Material("Support_Obelisk_Stone", new Color(.34f, .42f, .48f));
                var jade = Material("Support_Obelisk_Jade", new Color(.13f, .85f, .62f));
                var gold = Material("Support_Obelisk_Gold", new Color(1f, .76f, .2f));
                var glow = Material("Support_Obelisk_Glow", new Color(.32f, 1f, .68f), true);
                var basic = Group("JadeBeacon", root.transform);
                var upgraded = Group("TwinBeacon", root.transform);
                var final = Group("SanctuaryObelisk", root.transform);
                for (int i = 0; i < 3; i++)
                {
                    var tier = i == 0 ? basic : i == 1 ? upgraded : final;
                    Kit("tower-round-bottom-b", tier, new Vector3(0f, .18f, 0f), 1.45f, stone);
                    Kit("tower-round-middle-b", tier, new Vector3(0f, .65f, 0f), 1.05f, jade);
                    Kit("detail-crystal-large", tier, new Vector3(0f, i == 2 ? 1.45f : 1.1f, 0f), i == 2 ? 1.45f : 1.05f, glow);
                    if (i >= 1)
                    {
                        Kit("tower-round-top-b", tier, new Vector3(0f, 1.04f, 0f), 1.18f, gold);
                        Kit("detail-crystal", tier, new Vector3(-.66f, 1.02f, 0f), .8f, glow);
                        Kit("detail-crystal", tier, new Vector3(.66f, 1.02f, 0f), .8f, glow);
                    }
                    if (i == 2)
                    {
                        Kit("detail-crystal", tier, new Vector3(0f, 1.02f, -.66f), .9f, glow);
                        Kit("detail-crystal", tier, new Vector3(0f, 1.02f, .66f), .9f, glow);
                        Kit("tower-round-middle-b", tier, new Vector3(0f, 1.05f, 0f), .7f, gold);
                    }
                }
                tower.baseVisual = basic.gameObject; tower.upgradedVisual = upgraded.gameObject; tower.finalVisual = final.gameObject;
                upgraded.gameObject.SetActive(false); final.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "Support_Tower.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void MarkBoss(string prefabName, float width, Color color)
        {
            string path = Prefabs + prefabName + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var health = root.GetComponentInChildren<EnemyHealthBar>(true); health.width = width;
                var back = health.transform.Find("Background");
                if (back) back.localScale = new Vector3(width + .14f, .14f, .02f);
                if (health.fill) health.fill.localScale = new Vector3(width, .075f, .025f);
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); marker.name = "Boss_Threat_Marker";
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.SetParent(root.transform, false); marker.transform.localPosition = new Vector3(0f, .035f, 0f);
                marker.transform.localScale = new Vector3(width * .85f, .012f, width * .85f);
                marker.GetComponent<Renderer>().sharedMaterial = Material(prefabName + "_Threat", color, true);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Tools/Tower Defense/Generate Third Level Model Icons")]
        public static void GenerateThirdLevelIcons()
        {
            RequireSavedEditMode();
            var previous = SceneManager.GetActiveScene();
            var authoring = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(authoring);
                foreach (string name in new[] { "Support_Tower", "Shaman_Wizard", "Boss_Warden", "Boss_Titan" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
                    if (!prefab) throw new InvalidOperationException("Build third-level prefabs before rendering icons: " + name);
                    Reuse("RenderIcon", prefab, Icons + name + ".png");
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorSceneManager.CloseScene(authoring, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        // Reuse the existing, validated importer and icon renderer without regenerating first/second-level assets.
        static object Reuse(string name, params object[] arguments)
        {
            var method = typeof(SecondLevelSetup).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(typeof(SecondLevelSetup).FullName, name);
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
        static Material Material(string name, Color color, bool unlit = false) => (Material)Reuse("Material", name, color, unlit);
        static Transform Group(string name, Transform parent) => (Transform)Reuse("Group", name, parent);
        static GameObject Kit(string name, Transform parent, Vector3 position, float scale, Material material)
            => (GameObject)Reuse("PlaceKit", name, parent, position, scale, material);
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
            if (!sprite) throw new InvalidOperationException("Missing model icon: " + name);
            return sprite;
        }
        static void RequireSavedEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before configuring the snow fortress.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save current scene edits before configuring the snow fortress.");
        }
    }
}
