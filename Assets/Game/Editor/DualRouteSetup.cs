using System;
using System.Linq;
using TowerDefense.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    public static class DualRouteSetup
    {
        const string ScenePath = "Assets/Game/Scenes/Level01_ForestOutpost.unity";
        const string Models = "Assets/ThirdParty/Kenney/TowerDefenseKit/Models/";
        static Material palette, entrance;

        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var map = scene.GetRootGameObjects().Select(g => g.GetComponent<LevelMap>()).First(m => m);
            palette = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Kenney_Default_URP.mat");
            entrance = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Entrance_B.mat");
            Transform ground = map.transform.Find("01_Terrain"), roads = map.transform.Find("02_RoadTiles");
            for (int x = 0; x < 4; x++)
            {
                string suffix = x.ToString("00") + "_03";
                Remove(ground, "Ground_" + suffix);
                Remove(roads, "Road_" + suffix);
                Model("tile-straight", "Road_" + suffix, roads, new Vector3((x - 10) * 2, 0, -6), 2, 90, palette);
            }
            Remove(roads, "Road_04_03");
            Model("tile-split", "Road_04_03", roads, new Vector3(-12, 0, -6), 2, 0, palette);
            var routes = map.transform.Find("03_EnemyRoutes");
            Remove(routes, "Route_B");
            var routeRoot = new GameObject("Route_B").transform;
            routeRoot.SetParent(routes, false);
            var route = routeRoot.gameObject.AddComponent<EnemyRoute>();
            route.routeId = "B";
            var points = new[] { new Vector3(-20,.24f,-6), new Vector3(-12,.24f,-6),
                new Vector3(6,.24f,-6), new Vector3(6,.24f,6), new Vector3(20,.24f,6) };
            route.waypoints = points.Select((p, i) =>
            {
                var t = new GameObject("Waypoint_" + i.ToString("00")).transform;
                t.SetParent(routeRoot, false); t.position = p;
                return t;
            }).ToArray();
            map.routes = new[] { routes.Find("Route_A").GetComponent<EnemyRoute>(), route };
            var markers = map.transform.Find("05_Entrances_And_Base");
            if (!markers) markers = map.transform;
            Remove(markers, "Entrance_B"); Remove(markers, "Entrance_B_Post0"); Remove(markers, "Entrance_B_Post1");
            Model("spawn-square", "Entrance_B", markers, points[0] + Vector3.down * .01f, 2.1f, 0, entrance);
            for (int i = 0; i < 2; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Entrance_B_Post" + i;
                post.transform.SetParent(markers, false);
                post.transform.position = points[0] + new Vector3(0,.85f,i == 0 ? -.85f : .85f);
                post.transform.localScale = new Vector3(.3f,1.7f,.3f);
                post.GetComponent<Renderer>().sharedMaterial = entrance;
                UnityEngine.Object.DestroyImmediate(post.GetComponent<Collider>());
            }
            foreach (Transform item in map.transform.Find("06_Scenery").Cast<Transform>().ToArray())
                if (item.position.x >= -22 && item.position.x <= -10 && item.position.z >= -8 && item.position.z <= -4)
                    UnityEngine.Object.DestroyImmediate(item.gameObject);
            var camera = scene.GetRootGameObjects().Select(g => g.GetComponent<Camera>()).First(c => c);
            var rig = camera.GetComponent<MapCameraRig>();
            rig.gameplayViewport = new Rect(.025f, .20f, .95f, .64f);
            rig.focusOffset = new Vector3(0,.5f,-1.5f);
            rig.ApplyFraming();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("First level now has two entrances and a shared defensive choke point.");
        }
        static void Remove(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t) UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        static void Model(string assetName, string name, Transform parent, Vector3 position, float scale, float yaw, Material material)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Models + assetName + ".fbx");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.name = name;
            go.transform.position = position;
            go.transform.localRotation = Quaternion.Euler(0,yaw,0);
            go.transform.localScale = Vector3.one * scale;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material).ToArray();
        }
    }
}
