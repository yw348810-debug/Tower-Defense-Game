using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerDefense.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TowerDefense.EditorTools
{
    /// <summary>Rebuilds a map's visible terrain as a continuous two-lane corridor.</summary>
    public static class WideRoadSetup
    {
        const string ScenePath = "Assets/Game/Scenes/Level01_ForestOutpost.unity";
        const string MeshFolder = "Assets/Game/Maps/WideRoad";
        const float HalfWidth = 1.7f, RoadHeight = .205f, GrassHeight = .4f;
        const float MinX = -21f, MaxX = 21f, MinZ = -13f, MaxZ = 13f;
        const string RootName = "08_WideRoadSurface";

        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 colorUV)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) uv.Add(colorUV);
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }

        [MenuItem("Tools/Tower Defense/Widen First Level Roads")]
        public static void Configure() => ConfigureMap(ScenePath, MeshFolder);

        public static void ConfigureMap(string scenePath, string meshFolder,
            string palettePath = "Assets/Game/Materials/Kenney_Default_URP.mat")
        {
            RequireEditMode();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save current scene edits before widening roads.");
            var scene = EditorSceneManager.OpenScene(scenePath);
            var map = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<LevelMap>(true)).First();
            var terrain = map.transform.Find("01_Terrain");
            var roads = map.transform.Find("02_RoadTiles");
            if (!terrain || !roads || map.routes == null || map.routes.Length == 0)
                throw new InvalidOperationException("The map requires terrain, road tiles and route centerlines.");
            var palette = AssetDatabase.LoadAssetAtPath<Material>(palettePath);
            if (!palette) throw new InvalidOperationException("The Kenney URP palette is missing.");

            Vector2 roadUV = SurfaceUV(roads, .2f);
            Vector2 grassUV = SurfaceUV(terrain, .4f);
            var bands = RoadBands(map);
            var xs = new SortedSet<float> { MinX, MaxX };
            var zs = new SortedSet<float> { MinZ, MaxZ };
            foreach (var rect in bands)
            {
                xs.Add(Mathf.Clamp(rect.xMin, MinX, MaxX)); xs.Add(Mathf.Clamp(rect.xMax, MinX, MaxX));
                zs.Add(Mathf.Clamp(rect.yMin, MinZ, MaxZ)); zs.Add(Mathf.Clamp(rect.yMax, MinZ, MaxZ));
            }
            float[] x = xs.ToArray(), z = zs.ToArray();
            int width = x.Length - 1, height = z.Length - 1;
            var isRoad = new bool[width, height];
            var dirt = new Geometry(); var grass = new Geometry(); var banks = new Geometry();
            var stones = StoneDetails(roads, map.transform);
            for (int iz = 0; iz < height; iz++)
                for (int ix = 0; ix < width; ix++)
                {
                    Vector2 center = new Vector2((x[ix] + x[ix + 1]) * .5f, (z[iz] + z[iz + 1]) * .5f);
                    bool road = bands.Any(rect => rect.Contains(center));
                    isRoad[ix, iz] = road;
                    float y = road ? RoadHeight : GrassHeight;
                    // Clockwise when viewed from above, yielding upward-facing triangles.
                    (road ? dirt : grass).Quad(new Vector3(x[ix], y, z[iz]), new Vector3(x[ix], y, z[iz + 1]),
                        new Vector3(x[ix + 1], y, z[iz + 1]), new Vector3(x[ix + 1], y, z[iz]), road ? roadUV : grassUV);
                }
            for (int iz = 0; iz < height; iz++)
                for (int ix = 0; ix < width; ix++)
                {
                    if (ix + 1 < width && isRoad[ix, iz] != isRoad[ix + 1, iz])
                        Bank(banks, new Vector3(x[ix + 1], 0, z[iz]), new Vector3(x[ix + 1], 0, z[iz + 1]), grassUV,
                            isRoad[ix, iz] ? 1 : -1);
                    if (iz + 1 < height && isRoad[ix, iz] != isRoad[ix, iz + 1])
                        Bank(banks, new Vector3(x[ix], 0, z[iz + 1]), new Vector3(x[ix + 1], 0, z[iz + 1]), grassUV,
                            isRoad[ix, iz] ? -1 : 1);
                }
            OuterBanks(banks, x, z, isRoad, grassUV);

            var old = map.transform.Find(RootName);
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(map.transform, false);
            Directory.CreateDirectory(meshFolder); AssetDatabase.Refresh();
            Surface("Road_3.4m", root, dirt, palette, meshFolder);
            Surface("Grass_Cutouts", root, grass, palette, meshFolder);
            Surface("Road_Banks", root, banks, palette, meshFolder);
            Surface("Original_Stone_Details", root, stones, palette, meshFolder);
            foreach (Transform tile in terrain)
                if (tile.name.StartsWith("Ground_", StringComparison.Ordinal))
                    foreach (var renderer in tile.GetComponentsInChildren<Renderer>(true)) HideRenderer(renderer);
            foreach (var renderer in roads.GetComponentsInChildren<Renderer>(true)) HideRenderer(renderer);

            // Widen the visible entry arch to match the road while leaving route starts intact.
            var markers = map.transform.Find("05_Entrances_And_Base");
            if (markers)
                foreach (Transform marker in markers)
                {
                    if (!marker.name.StartsWith("Entrance_", StringComparison.Ordinal)) continue;
                    if (marker.name.Contains("Post"))
                    {
                        EnemyRoute route = map.routes.FirstOrDefault(r => r && marker.name.StartsWith("Entrance_" + r.routeId + "_", StringComparison.Ordinal));
                        if (!route || route.waypoints == null || route.waypoints.Length < 2) continue;
                        Vector3 start = route.waypoints[0].position;
                        Vector3 direction = route.waypoints[1].position - start; direction.y = 0;
                        Vector3 side = Vector3.Cross(Vector3.up, direction.normalized);
                        float sign = Vector3.Dot(marker.position - start, side) >= 0 ? 1 : -1;
                        Vector3 position = marker.position;
                        Vector3 horizontal = start + side * (HalfWidth + .1f) * sign;
                        position.x = horizontal.x; position.z = horizontal.z; marker.position = position;
                    }
                    else
                    {
                        // spawn-square is 1m wide before its old 2.1x scale.
                        Vector3 scale = marker.localScale; scale.z = 3.4f; marker.localScale = scale;
                    }
                    EditorUtility.SetDirty(marker);
                    if (PrefabUtility.IsPartOfPrefabInstance(marker))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log(scenePath + " roads widened to 3.4m with continuous corners and a shared junction.");
        }

        static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before widening roads.");
        }

        static void HideRenderer(Renderer renderer)
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }

        static List<Rect> RoadBands(LevelMap map)
        {
            var result = new List<Rect>();
            foreach (var route in map.routes)
            {
                if (!route || route.waypoints == null) continue;
                for (int i = 1; i < route.waypoints.Length; i++)
                {
                    if (!route.waypoints[i - 1] || !route.waypoints[i]) throw new InvalidOperationException("Missing route waypoint.");
                    Vector3 a = map.transform.InverseTransformPoint(route.waypoints[i - 1].position);
                    Vector3 b = map.transform.InverseTransformPoint(route.waypoints[i].position);
                    if (Mathf.Abs(a.x - b.x) > .01f && Mathf.Abs(a.z - b.z) > .01f)
                        throw new InvalidOperationException("Wide roads require the map's axis-aligned routes.");
                    result.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x) - HalfWidth, Mathf.Min(a.z, b.z) - HalfWidth,
                        Mathf.Max(a.x, b.x) + HalfWidth, Mathf.Max(a.z, b.z) + HalfWidth));
                }
            }
            if (result.Count == 0) throw new InvalidOperationException("No valid route segments.");
            return result;
        }

        static Vector2 SurfaceUV(Transform group, float worldHeight)
        {
            float largest = 0f; Vector2 result = Vector2.zero;
            foreach (var filter in group.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                Vector3[] vertices = filter.sharedMesh.vertices; Vector2[] uv = filter.sharedMesh.uv;
                int[] triangles = filter.sharedMesh.triangles;
                if (uv.Length != vertices.Length) continue;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                    Vector3 a = filter.transform.TransformPoint(vertices[ia]);
                    Vector3 b = filter.transform.TransformPoint(vertices[ib]);
                    Vector3 c = filter.transform.TransformPoint(vertices[ic]);
                    if (Mathf.Abs(a.y - worldHeight) > .015f || Mathf.Abs(b.y - worldHeight) > .015f || Mathf.Abs(c.y - worldHeight) > .015f) continue;
                    float area = Vector3.Cross(b - a, c - a).magnitude;
                    if (area <= largest) continue;
                    largest = area; result = (uv[ia] + uv[ib] + uv[ic]) / 3f;
                }
                // Every source tile uses the same palette colors; one matching mesh is enough.
                if (largest > 0f) return result;
            }
            throw new InvalidOperationException("Could not sample the original Kenney terrain palette at height " + worldHeight);
        }

        static Geometry StoneDetails(Transform roads, Transform map)
        {
            var result = new Geometry();
            foreach (var filter in roads.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                var vertices = filter.sharedMesh.vertices; var uv = filter.sharedMesh.uv; var triangles = filter.sharedMesh.triangles;
                if (uv.Length != vertices.Length) continue;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int[] indices = { triangles[i], triangles[i + 1], triangles[i + 2] };
                    Vector3[] points = indices.Select(index => map.InverseTransformPoint(filter.transform.TransformPoint(vertices[index]))).ToArray();
                    float min = points.Min(p => p.y), max = points.Max(p => p.y);
                    if (min < .195f || max > .29f || max < .212f) continue;
                    int start = result.vertices.Count;
                    for (int j = 0; j < 3; j++)
                    {
                        // Shift by 5mm so the old stone bottoms sit on the new .205m road.
                        result.vertices.Add(points[j] + Vector3.up * .005f); result.uv.Add(uv[indices[j]]);
                    }
                    result.triangles.AddRange(new[] { start, start + 1, start + 2 });
                }
            }
            return result;
        }

        static void Bank(Geometry geometry, Vector3 a, Vector3 b, Vector2 uv, int side)
        {
            Vector3 aLow = a + Vector3.up * RoadHeight, bLow = b + Vector3.up * RoadHeight;
            Vector3 aHigh = a + Vector3.up * GrassHeight, bHigh = b + Vector3.up * GrassHeight;
            if (side > 0) geometry.Quad(aLow, bLow, bHigh, aHigh, uv);
            else geometry.Quad(bLow, aLow, aHigh, bHigh, uv);
        }

        static void OuterBanks(Geometry geometry, float[] x, float[] z, bool[,] road, Vector2 uv)
        {
            for (int ix = 0; ix < x.Length - 1; ix++)
            {
                float front = road[ix, 0] ? RoadHeight : GrassHeight;
                float back = road[ix, z.Length - 2] ? RoadHeight : GrassHeight;
                geometry.Quad(new Vector3(x[ix + 1], 0, MinZ), new Vector3(x[ix], 0, MinZ),
                    new Vector3(x[ix], front, MinZ), new Vector3(x[ix + 1], front, MinZ), uv);
                geometry.Quad(new Vector3(x[ix], 0, MaxZ), new Vector3(x[ix + 1], 0, MaxZ),
                    new Vector3(x[ix + 1], back, MaxZ), new Vector3(x[ix], back, MaxZ), uv);
            }
            for (int iz = 0; iz < z.Length - 1; iz++)
            {
                float left = road[0, iz] ? RoadHeight : GrassHeight;
                float right = road[x.Length - 2, iz] ? RoadHeight : GrassHeight;
                geometry.Quad(new Vector3(MinX, 0, z[iz]), new Vector3(MinX, 0, z[iz + 1]),
                    new Vector3(MinX, left, z[iz + 1]), new Vector3(MinX, left, z[iz]), uv);
                geometry.Quad(new Vector3(MaxX, 0, z[iz + 1]), new Vector3(MaxX, 0, z[iz]),
                    new Vector3(MaxX, right, z[iz]), new Vector3(MaxX, right, z[iz + 1]), uv);
            }
        }

        static void Surface(string name, Transform parent, Geometry geometry, Material material, string meshFolder)
        {
            string path = meshFolder + "/" + name + ".asset";
            Mesh created = geometry.Mesh(name), mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh)
            {
                EditorUtility.CopySerialized(created, mesh); UnityEngine.Object.DestroyImmediate(created);
                EditorUtility.SetDirty(mesh);
            }
            else { mesh = created; AssetDatabase.CreateAsset(mesh, path); }
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        }
    }
}
