using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerDefense.Gameplay;
using TowerDefense.Maps;
using TowerDefense.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TowerDefense.EditorTools
{
    /// <summary>Authors the front end while leaving each playable battlefield asset untouched.</summary>
    public static class MainMenuSetup
    {
        public const string MainMenuScenePath = "Assets/Game/Scenes/MainMenu.unity";
        private const string Models = "Assets/ThirdParty/Kenney/TowerDefenseKit/Models/";
        private const string MenuAssets = "Assets/Game/UI/Menu";
        private const string MenuMaterials = "Assets/Game/Materials/Menu";
        private static readonly string[] BattleScenes =
        {
            "Assets/Game/Scenes/Level01_ForestOutpost.unity",
            "Assets/Game/Scenes/Level02_CanyonConfluence.unity",
            "Assets/Game/Scenes/Level03_CoreFortress.unity"
        };

        [MenuItem("Tools/Tower Defense/Build Home Menu")]
        public static void Build()
        {
            RequireSavedEditMode();
            ChineseFontSetup.Build();
            var original = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Sprite[] previews = GenerateLevelPreviews();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                BuildBackdrop(scene);
                var menu = new GameObject("Frontend Menu").AddComponent<FrontendMenu>();
                menu.levelPreviews = previews;
                menu.fontOverride = Localization.Font;
                EditorSceneManager.SaveScene(scene, MainMenuScenePath);
                ConfigureBuildOrder();
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuScenePath);
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = menu.gameObject;
                Debug.Log("Home menu ready. MainMenu is the initial scene; all three battlefields are selectable.");
            }
            catch
            {
                EditorSceneManager.RestoreSceneManagerSetup(original);
                throw;
            }
        }

        public static Sprite[] GenerateLevelPreviews()
        {
            Directory.CreateDirectory(MenuAssets);
            AssetDatabase.Refresh();
            var previews = new Sprite[BattleScenes.Length];
            for (int i = 0; i < BattleScenes.Length; i++)
            {
                var scene = EditorSceneManager.OpenScene(BattleScenes[i], OpenSceneMode.Single);
                var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c => c.CompareTag("MainCamera"));
                if (!camera) throw new InvalidOperationException("No main camera in " + BattleScenes[i]);
                string path = MenuAssets + "/Level" + (i + 1) + "_Preview.png";
                CapturePreview(camera, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
                previews[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return previews;
        }

        private static void CapturePreview(Camera camera, string path)
        {
            var rig = camera.GetComponent<MapCameraRig>();
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var oldPosition = camera.transform.position;
            var oldRotation = camera.transform.rotation;
            var oldAspect = camera.aspect;
            var oldRect = camera.rect;
            var viewport = rig ? rig.gameplayViewport : default;
            bool align = rig && rig.alignToViewportCenter;
            float height = rig ? rig.routeFramingHeight : 3.5f;
            var texture = new RenderTexture(512, 300, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(512, 300, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = texture;
                camera.aspect = 512f / 300f;
                if (rig)
                {
                    rig.gameplayViewport = new Rect(.04f, .08f, .92f, .84f);
                    rig.alignToViewportCenter = false;
                    rig.routeFramingHeight = 3.5f;
                    rig.ApplyFraming();
                }
                camera.rect = new Rect(0, 0, 1, 1);
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 512, 300), 0, 0);
                image.Apply(false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                camera.rect = oldRect;
                if (rig)
                {
                    rig.gameplayViewport = viewport;
                    rig.alignToViewportCenter = align;
                    rig.routeFramingHeight = height;
                }
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation);
                RenderTexture.active = oldActive;
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(image);
                // Preview framing is deliberately never written to a battlefield scene.
            }
        }

        private static void BuildBackdrop(Scene scene)
        {
            Directory.CreateDirectory(MenuMaterials);
            AssetDatabase.Refresh();
            var stone = Material("Island Stone", new Color(.075f, .17f, .19f));
            var underside = Material("Island Underside", new Color(.035f, .085f, .105f));
            var roadEdge = Material("Road Edges", new Color(.29f, .35f, .29f));
            var road = Material("Golden Road", new Color(.80f, .64f, .36f));
            var gold = Material("Flag Gold", new Color(1f, .72f, .31f));
            var teal = Material("Flag Teal", new Color(.12f, .79f, .65f));
            var glow = Material("Lantern Glow", new Color(.32f, 1f, .81f), true);
            var pole = Material("Lantern Metal", new Color(.13f, .24f, .26f));
            var palette = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Kenney_Default_URP.mat");
            if (!palette) throw new InvalidOperationException("Kenney palette material is unavailable.");

            var island = new GameObject("Home Island / Decorative Only").transform;
            island.position = new Vector3(8, 0, 1);
            var foundation = new GameObject("Floating Island Foundation", typeof(MeshFilter), typeof(MeshRenderer));
            foundation.transform.SetParent(island, false);
            foundation.GetComponent<MeshFilter>().sharedMesh = BuildIslandMesh();
            foundation.GetComponent<MeshRenderer>().sharedMaterials = new[] { stone, underside };
            var terrain = new GameObject("Forest Terrain").transform;
            terrain.SetParent(island, false);
            for (int z = -3; z <= 3; z++)
                for (int x = -5; x <= 4; x++)
                {
                    if (Mathf.Abs(z) == 3 && (x == -5 || x == 4)) continue;
                    PlaceModel("tile", terrain, new Vector3(x * 2 + 1, 0, z * 2), 2f, 0, palette);
                }

            var path = new GameObject("Winding Route").transform;
            path.SetParent(island, false);
            Vector3[] points =
            {
                new Vector3(-10, .43f, -3), new Vector3(-5, .43f, -3), new Vector3(-5, .43f, 1),
                new Vector3(4, .43f, 1), new Vector3(4, .43f, -3), new Vector3(10, .43f, -3)
            };
            for (int i = 1; i < points.Length; i++)
            {
                RoadSegment(path, points[i - 1], points[i], 2.85f, roadEdge);
                RoadSegment(path, points[i - 1] + Vector3.up * .035f, points[i] + Vector3.up * .035f, 2.45f, road);
            }

            PlaceTower("Rapid_Turret", island, new Vector3(-1.7f, .42f, -2.3f), 1.6f, -65);
            PlaceTower("Frost_Turret", island, new Vector3(1.5f, .42f, 4.3f), 1.7f, 180);
            PlaceTower("Cannon_Turret", island, new Vector3(7.5f, .42f, 1.7f), 1.65f, -115);

            var grove = new GameObject("Pine Grove").transform;
            grove.SetParent(island, false);
            Vector3[] trees =
            {
                new Vector3(-8.1f, .3f, 4.6f), new Vector3(-7.1f,.3f,6), new Vector3(-5,.3f,5.5f),
                new Vector3(-3,.3f,5.9f), new Vector3(-1.2f,.3f,5.6f), new Vector3(4.2f,.3f,5.8f),
                new Vector3(6.5f,.3f,5.2f), new Vector3(8.2f,.3f,4.7f),new Vector3(-8.3f,.3f,.3f),
                new Vector3(-9,.3f,-5),new Vector3(-6.7f,.3f,-5.6f),new Vector3(-3.5f,.3f,-5.8f),
                new Vector3(1.7f,.3f,-5.8f), new Vector3(6.9f,.3f,-5.7f)
            };
            for (int i = 0; i < trees.Length; i++)
                PlaceModel(i % 3 == 0 ? "detail-tree-large" : "detail-tree", grove, trees[i], i < 9 ? 2.25f + i % 3 * .22f : 1.8f, i * 137, palette);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2f / 9f;
                PlaceModel("detail-rocks-large", grove, new Vector3(Mathf.Cos(angle) * 8.9f, .25f, Mathf.Sin(angle) * 6.1f),
                    .9f + i % 3 * .18f, i * 73, palette);
            }
            Flag(island, new Vector3(-7.4f, .35f, -1f), teal, pole, -35);
            Flag(island, new Vector3(7.5f, .35f, -3.7f), gold, pole, 20);

            var core = new GameObject("Glowing Core").transform;
            core.SetParent(island, false);
            core.localPosition = new Vector3(8.7f, .35f, -2.7f);
            PlaceModel("tower-square-bottom-a", core, Vector3.zero, 1.5f, 0, palette);
            PlaceModel("tower-square-top-a", core, new Vector3(0, .72f, 0), 1.5f, 0, palette);
            PlaceModel("detail-crystal-large", core, new Vector3(0, 1.45f, 0), 1.65f, 25, glow);
            Lantern(island, new Vector3(-5.2f, .35f, -5), pole, glow);
            Lantern(island, new Vector3(3.6f, .35f, 2.9f), pole, glow);
            Lantern(island, new Vector3(9, .35f, -.7f), pole, glow);

            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 24, -38);
            camera.transform.LookAt(new Vector3(0, 0, 0));
            camera.fieldOfView = 36;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 120;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .065f, .095f);
            camera.allowHDR = true;
            camera.gameObject.AddComponent<HomeBackdropMotion>();
            var sun = new GameObject("Warm Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = new Color(1f, .88f, .66f);
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = .025f;
            sun.transform.rotation = Quaternion.Euler(46, -36, 0);
            var fillLight = new GameObject("Cool Rim Light").AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = .55f;
            fillLight.color = new Color(.30f, .73f, .91f);
            fillLight.shadows = LightShadows.None;
            fillLight.transform.rotation = Quaternion.Euler(25, 145, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.26f, .38f, .43f);
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = camera.backgroundColor;
            RenderSettings.fogStartDistance = 52;
            RenderSettings.fogEndDistance = 90;
            CreateMotes(island, glow);
        }

        private static void CreateMotes(Transform parent, Material glow)
        {
            var go = new GameObject("Fireflies");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 1.7f, 0);
            var particles = go.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 5;
            main.startSpeed = .15f;
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .085f);
            main.startColor = new Color(.43f, 1f, .76f, .6f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            var emission = particles.emission;
            emission.rateOverTime = 5;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(18, 3, 11);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = glow;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void PlaceTower(string prefab, Transform parent, Vector3 position, float scale, float rotation)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/" + prefab + ".prefab");
            if (!asset) throw new InvalidOperationException("Missing tower prefab: " + prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Showcase / " + prefab;
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one * scale;
            go.transform.localRotation = Quaternion.Euler(0, rotation, 0);
            var tower = go.GetComponent<TowerAgent>();
            if (tower)
            {
                if (tower.baseVisual) tower.baseVisual.SetActive(false);
                if (tower.upgradedVisual) tower.upgradedVisual.SetActive(false);
                if (tower.finalVisual) tower.finalVisual.SetActive(true);
            }
            foreach (var behavior in go.GetComponentsInChildren<MonoBehaviour>(true))
                UnityEngine.Object.DestroyImmediate(behavior);
            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
        }

        private static void Flag(Transform parent, Vector3 position, Material cloth, Material metal, float rotation)
        {
            var root = new GameObject("Frontier Banner").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0, rotation, 0);
            Primitive("Pole", PrimitiveType.Cylinder, root, new Vector3(0, 1.8f, 0), new Vector3(.08f, 1.8f, .08f), metal);
            var banner = new GameObject("Banner Cloth", typeof(MeshFilter), typeof(MeshRenderer));
            banner.transform.SetParent(root, false);
            var mesh = new Mesh { name = "Frontier Banner Mesh" };
            mesh.vertices = new[] { new Vector3(0, 3.45f, 0), new Vector3(1.35f, 3.38f, .08f), new Vector3(1.16f, 2.75f, .03f), new Vector3(0, 2.65f, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 2, 1, 0, 3, 2, 0 };
            mesh.RecalculateNormals();
            string path = MenuAssets + "/BannerMesh.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved) { UnityEngine.Object.DestroyImmediate(mesh); mesh = saved; }
            else AssetDatabase.CreateAsset(mesh, path);
            banner.GetComponent<MeshFilter>().sharedMesh = mesh;
            banner.GetComponent<MeshRenderer>().sharedMaterial = cloth;
        }

        private static void Lantern(Transform parent, Vector3 position, Material metal, Material glow)
        {
            var root = new GameObject("Teal Lantern").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            Primitive("Post", PrimitiveType.Cylinder, root, new Vector3(0, .9f, 0), new Vector3(.1f, .9f, .1f), metal);
            Primitive("Lamp", PrimitiveType.Sphere, root, new Vector3(0, 1.85f, 0), Vector3.one * .35f, glow);
            var light = new GameObject("Teal Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = new Vector3(0, 1.85f, 0);
            light.type = LightType.Point;
            light.color = new Color(.2f, 1f, .74f);
            light.intensity = 2;
            light.range = 5;
            light.shadows = LightShadows.None;
        }

        private static void RoadSegment(Transform parent, Vector3 start, Vector3 end, float width, Material material)
        {
            var delta = end - start;
            bool horizontal = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
            Primitive("Route", PrimitiveType.Cube, parent, (start + end) * .5f,
                new Vector3(horizontal ? Mathf.Abs(delta.x) + width : width, .04f,
                    horizontal ? width : Mathf.Abs(delta.z) + width), material);
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var collider = go.GetComponent<Collider>();
            if (collider) UnityEngine.Object.DestroyImmediate(collider);
            return go;
        }

        private static void PlaceModel(string name, Transform parent, Vector3 position, float scale, float rotation, Material material)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");
            if (!asset) throw new InvalidOperationException("Missing Kenney scenery: " + name);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, rotation, 0);
            go.transform.localScale = Vector3.one * scale;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
        }

        private static Mesh BuildIslandMesh()
        {
            string path = MenuAssets + "/IslandMesh.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) return existing;
            Vector3[] outline =
            {
                new Vector3(-8, 0, -7), new Vector3(8, 0, -7), new Vector3(10, 0, -5), new Vector3(10, 0, 5),
                new Vector3(8, 0, 7), new Vector3(-8, 0, 7), new Vector3(-10, 0, 5), new Vector3(-10, 0, -5)
            };
            var vertices = new List<Vector3>();
            var sides = new List<int>();
            var bottom = new List<int>();
            Action<Vector3, Vector3, Vector3, List<int>> triangle = (a, b, c, list) =>
            {
                int n = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                list.Add(n); list.Add(n + 1); list.Add(n + 2);
            };
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i];
                var b = outline[(i + 1) % outline.Length];
                var lowerA = a * .86f + Vector3.down * (2.2f + i % 3 * .55f);
                var lowerB = b * .86f + Vector3.down * (2.2f + (i + 1) % 3 * .55f);
                triangle(a, b, lowerB, sides);
                triangle(a, lowerB, lowerA, sides);
                triangle(lowerA, lowerB, new Vector3(.8f, -5.4f, -.3f), bottom);
            }
            var mesh = new Mesh { name = "Faceted Floating Island" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(sides, 0);
            mesh.SetTriangles(bottom, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Material Material(string name, Color color, bool emissive = false)
        {
            string path = MenuMaterials + "/" + name.Replace(" ", "_") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("URP Lit shader is missing.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Smoothness", .12f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.65f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureBuildOrder()
        {
            var required = new[] { MainMenuScenePath }.Concat(BattleScenes).ToArray();
            var remaining = EditorBuildSettings.scenes.Where(s => !required.Contains(s.path));
            EditorBuildSettings.scenes = required.Select(p => new EditorBuildSettingsScene(p, true)).Concat(remaining).ToArray();
        }

        private static void RequireSavedEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Leave Play Mode and finish compilation before authoring the menu.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save the open scene before building the home menu: " + SceneManager.GetSceneAt(i).name);
        }
    }
}
