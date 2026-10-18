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
    /// <summary>Decorates the authored maps without touching route, slot or combat data.</summary>
    public static class MapPolishSetup
    {
        const string Kit = "Assets/ThirdParty/Kenney/TowerDefenseKit/Models/";
        const string AssetRoot = "Assets/Game/Maps/ArtPolish";
        const string ArtRoot = "09_ArtPolish";
        static readonly string[] ScenePaths =
        {
            "Assets/Game/Scenes/Level01_ForestOutpost.unity",
            "Assets/Game/Scenes/Level02_CanyonConfluence.unity",
            "Assets/Game/Scenes/Level03_CoreFortress.unity"
        };

        sealed class Dressing
        {
            public LevelMap map;
            public Transform root;
            public string folder;
            public int biome;
            public System.Random random;
            public Material road, edge, stone, wood, metal, gold, water, glow, palette, snow;
            public Material[] ground;
            public readonly List<Rect> roadBands = new List<Rect>();
            public readonly List<Vector2> slots = new List<Vector2>();
            public readonly List<Vector3> landmarks = new List<Vector3>();
            public readonly List<Vector4> levelWaterGround = new List<Vector4>();

            public bool Road(Vector2 p) => roadBands.Any(b => b.Contains(p));
            public float RoadDistance(Vector2 p)
            {
                float nearest = float.PositiveInfinity;
                foreach (var b in roadBands)
                {
                    float dx = Mathf.Max(b.xMin - p.x, 0f, p.x - b.xMax);
                    float dz = Mathf.Max(b.yMin - p.y, 0f, p.y - b.yMax);
                    nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dz * dz));
                }
                return nearest;
            }
            public bool Clear(Vector2 p, float radius)
            {
                if (RoadDistance(p) < radius + .5f) return false;
                if (slots.Any(s => Vector2.Distance(s, p) < radius + 1.5f)) return false;
                Vector3 core = map.transform.InverseTransformPoint(map.baseAnchor.position);
                return Vector2.Distance(p, new Vector2(core.x, core.z)) > radius + 2.5f;
            }
            public float Height(float x, float z)
            {
                var p = new Vector2(x, z);
                float clearance = RoadDistance(p);
                foreach (var slot in slots) clearance = Mathf.Min(clearance, Vector2.Distance(slot, p) - 1.6f);
                // Pads and both lanes stay flat; small hills live in decorative pockets.
                float rise = Mathf.Clamp01((clearance - .7f) / 3.5f);
                float wave = Mathf.PerlinNoise((x + 71) * .15f, (z + 93) * .15f);
                float elevation = rise * rise * (biome == 1 ? .42f : .28f) * wave;
                foreach (var lake in levelWaterGround)
                {
                    float radius = new Vector2((x - lake.x) / lake.z, (z - lake.y) / lake.w).magnitude;
                    // Extend the level area beyond the shoreline so a coarse triangle cannot
                    // interpolate a distant hill through the water. Only the outer rim blends.
                    elevation *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radius - 1f) * 3f));
                }
                return .415f + elevation;
            }
            public float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2);
            }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            { Triangle(a, b, c); Triangle(a, c, d); }
            public Mesh Create(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }

        [MenuItem("Tools/Tower Defense/Polish All Three Maps")]
        public static void Build()
        {
            RequireSavedEditMode();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var buildSettings = EditorBuildSettings.scenes;
            Directory.CreateDirectory("Captures/Maps/BeforePolish");
            // Retain a recoverable scene copy before the first art pass, never overwrite it.
            foreach (string path in ScenePaths)
            {
                string backup = "Captures/Maps/BeforePolish/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            try
            {
                foreach (string path in ScenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Configure(scene);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save " + path);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("Map art complete: forest campsite and pond, canyon strata and ruins, snow citadel and ice. Routes, build slots and combat settings preserved.");
            }
            finally
            {
                // This tool has no authority to reorder startup or selectable scenes.
                EditorBuildSettings.scenes = buildSettings;
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        public static void Configure(Scene scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before authoring map art.");
            var map = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelMap>(true)).FirstOrDefault();
            if (!map || !map.baseAnchor || map.GetValidationErrors().Count != 0)
                throw new InvalidOperationException("A valid authored LevelMap is required.");
            int biome = map.levelId.Contains("Level03") ? 2 : map.levelId.Contains("Level02") ? 1 : 0;
            string folder = AssetRoot + "/" + map.levelId;
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var old = map.transform.Find(ArtRoot);
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var d = new Dressing
            {
                map = map, root = Group(ArtRoot, map.transform), folder = folder,
                biome = biome, random = new System.Random(9421 + biome)
            };
            foreach (var route in map.routes)
                for (int i = 1; i < route.waypoints.Length; i++)
                {
                    Vector3 a = map.transform.InverseTransformPoint(route.waypoints[i - 1].position);
                    Vector3 b = map.transform.InverseTransformPoint(route.waypoints[i].position);
                    d.roadBands.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 1.7f, Mathf.Min(a.z, b.z) - 1.7f,
                        Mathf.Max(a.x, b.x) + 1.7f, Mathf.Max(a.z, b.z) + 1.7f));
                }
            d.slots.AddRange(map.buildSlots.Select(s => map.transform.InverseTransformPoint(s.transform.position))
                .Select(p => new Vector2(p.x, p.z)));
            if (biome == 0) d.levelWaterGround.Add(new Vector4(-2f, 8.7f, 5.2f, 3.8f));
            else if (biome == 1) d.levelWaterGround.Add(new Vector4(16f, -10.9f, 3.9f, 2.9f));
            else d.levelWaterGround.Add(new Vector4(-15f, 0f, 5.0f, 3.7f));
            PrepareMaterials(d);
            ReplaceSurfaces(d);
            // Replace random individual props with grouped plantings and purposeful landmarks.
            var previousScenery = map.transform.Find("06_Scenery");
            if (previousScenery) previousScenery.gameObject.SetActive(false);
            var outskirts = map.transform.Find("07_Outskirts");
            if (outskirts)
                foreach (Transform child in outskirts)
                {
                    if (child.name == "Surrounding_Ground")
                        foreach (var r in child.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = d.ground[0];
                    else child.gameObject.SetActive(false);
                }
            DressPads(d);
            DressRoads(d);
            if (biome == 0) Forest(d);
            else if (biome == 1) Canyon(d);
            else SnowFortress(d);
            PlantClusters(d);
            Outskirts(d);
            Lighting(scene, d);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void PrepareMaterials(Dressing d)
        {
            Color[] colors = d.biome == 2
                ? new[] { new Color(.71f,.84f,.88f), new Color(.80f,.91f,.94f), new Color(.74f,.86f,.90f), new Color(.85f,.94f,.96f) }
                : d.biome == 1
                ? new[] { new Color(.51f,.39f,.24f), new Color(.55f,.43f,.28f), new Color(.57f,.46f,.31f), new Color(.52f,.42f,.29f) }
                : new[] { new Color(.27f,.47f,.31f), new Color(.29f,.50f,.33f), new Color(.32f,.52f,.34f), new Color(.30f,.49f,.31f) };
            Color midpoint = colors.Aggregate(Color.clear, (total, color) => total + color) / colors.Length;
            d.ground = colors.Select((c, i) => Mat(d, "Ground_" + i, Color.Lerp(midpoint, c, .7f))).ToArray();
            d.road = Mat(d, "Road", d.biome == 2 ? new Color(.41f,.60f,.69f) : d.biome == 1 ? new Color(.76f,.60f,.39f) : new Color(.66f,.52f,.34f));
            d.edge = Mat(d, "Road_Edge", d.biome == 2 ? new Color(.66f,.81f,.87f) : new Color(.49f,.48f,.38f));
            d.stone = Mat(d, "Rock", d.biome == 1 ? new Color(.52f,.37f,.27f) : new Color(.31f,.42f,.45f));
            d.wood = Mat(d, "Wood", new Color(.27f,.24f,.18f));
            d.metal = Mat(d, "Metal", new Color(.17f,.27f,.31f));
            d.gold = Mat(d, "Banner", d.biome == 2 ? new Color(.25f,.64f,.76f) : new Color(.80f,.57f,.23f));
            d.water = Mat(d, "Water", d.biome == 2 ? new Color(.26f,.60f,.72f) : new Color(.19f,.49f,.50f), .65f);
            d.glow = Mat(d, "Lantern", d.biome == 2 ? new Color(.37f,.84f,.97f) : new Color(1f,.74f,.33f), 0, true);
            d.snow = Mat(d, "Snow", new Color(.86f,.94f,.97f));
            string originalPath = "Assets/Game/Materials/" + (d.biome == 1 ? "Kenney_Canyon_URP.mat" : "Kenney_Default_URP.mat");
            var source = AssetDatabase.LoadAssetAtPath<Material>(originalPath);
            string palettePath = d.folder + "/Environment_Palette.mat";
            d.palette = AssetDatabase.LoadAssetAtPath<Material>(palettePath);
            if (!d.palette) { d.palette = new Material(source); AssetDatabase.CreateAsset(d.palette, palettePath); }
            d.palette.color = d.biome == 2 ? new Color(.89f,.97f,1f) : d.biome == 1 ? new Color(.91f,.83f,.67f) : new Color(.70f,.87f,.76f);
            d.palette.enableInstancing = true; EditorUtility.SetDirty(d.palette);
        }

        static Material Mat(Dressing d, string name, Color color, float smoothness = 0f, bool emission = false)
        {
            string path = d.folder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (!shader) throw new InvalidOperationException("URP Lit shader unavailable.");
                m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path);
            }
            m.color = color; m.SetFloat("_Smoothness", smoothness); m.enableInstancing = true;
            if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.25f); }
            EditorUtility.SetDirty(m); return m;
        }

        static void ReplaceSurfaces(Dressing d)
        {
            var wide = d.map.transform.Find("08_WideRoadSurface");
            if (!wide) throw new InvalidOperationException("Polish requires the existing two-lane road surface.");
            foreach (var r in wide.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("Grass", StringComparison.Ordinal)) r.enabled = false;
                else if (r.name.Contains("Road_Banks")) r.sharedMaterial = d.ground[1];
                else if (r.name.Contains("Stone")) r.sharedMaterial = d.edge;
                else r.sharedMaterial = d.road;
                EditorUtility.SetDirty(r);
            }
            var xs = new SortedSet<float> { -21, 21 }; var zs = new SortedSet<float> { -13, 13 };
            for (float x = -21; x <= 21; x += 1.5f) xs.Add(x);
            for (float z = -13; z <= 13; z += 1.5f) zs.Add(z);
            foreach (var band in d.roadBands)
            {
                xs.Add(Mathf.Clamp(band.xMin,-21,21)); xs.Add(Mathf.Clamp(band.xMax,-21,21));
                zs.Add(Mathf.Clamp(band.yMin,-13,13)); zs.Add(Mathf.Clamp(band.yMax,-13,13));
            }
            float[] xv = xs.ToArray(), zv = zs.ToArray();
            var geometry = Enumerable.Range(0,4).Select(i => new Geometry()).ToArray();
            for (int x = 0; x < xv.Length - 1; x++) for (int z = 0; z < zv.Length - 1; z++)
            {
                var p = new Vector2((xv[x] + xv[x+1]) * .5f, (zv[z] + zv[z+1]) * .5f);
                if (d.Road(p)) continue;
                float noise = Mathf.PerlinNoise((p.x + 16) * .21f, (p.y + 23) * .21f);
                int tone = Mathf.Clamp(Mathf.FloorToInt(noise * 5f - .8f), 0, 3);
                var g = geometry[tone];
                Vector3 a = new Vector3(xv[x], d.Height(xv[x],zv[z]), zv[z]);
                Vector3 b = new Vector3(xv[x], d.Height(xv[x],zv[z+1]), zv[z+1]);
                Vector3 c = new Vector3(xv[x+1], d.Height(xv[x+1],zv[z+1]), zv[z+1]);
                Vector3 e = new Vector3(xv[x+1], d.Height(xv[x+1],zv[z]), zv[z]);
                // A low-poly center gives the floor broad facets, rather than a tile texture.
                Vector3 center = new Vector3(p.x,d.Height(p.x,p.y),p.y);
                g.Triangle(a,b,center); g.Triangle(b,c,center); g.Triangle(c,e,center); g.Triangle(e,a,center);
            }
            var surface = Group("01_Faceted_Terrain", d.root);
            for (int i = 0; i < 4; i++) MeshObject(d, "Terrain_Tone_" + i, surface, geometry[i].Create("Terrain_"+i), d.ground[i]);
        }

        static void DressPads(Dressing d)
        {
            var group = Group("02_Build_Slot_Stonework", d.root);
            foreach (var slot in d.slots)
            {
                var p = new Vector3(slot.x,.44f,slot.y);
                Cylinder("Stone_Foundation", group,p,new Vector3(2.3f,.035f,2.3f), d.stone);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * .25f;
                    var b = Box("Foundation_Segment",group,p + new Vector3(Mathf.Sin(a)*1.16f,.03f,Mathf.Cos(a)*1.16f),
                        new Vector3(.52f,.10f,.24f),d.edge);
                    b.transform.localRotation = Quaternion.Euler(0,i*45,0);
                }
            }
        }

        static void DressRoads(Dressing d)
        {
            var group = Group("03_Road_Edges_And_Stone_Inlays", d.root);
            var used = new HashSet<Vector2Int>();
            foreach (var route in d.map.routes)
                for (int i = 1; i < route.waypoints.Length; i++)
                {
                    Vector3 a = d.map.transform.InverseTransformPoint(route.waypoints[i-1].position);
                    Vector3 b = d.map.transform.InverseTransformPoint(route.waypoints[i].position);
                    Vector3 direction = (b-a).normalized; direction.y = 0;
                    Vector3 side = Vector3.Cross(Vector3.up,direction);
                    float length = Vector3.Distance(a,b);
                    for (float t = .6f; t < length - .3f; t += 1.45f)
                    {
                        Vector3 center = a + direction * t;
                        foreach (int sign in new[]{-1,1})
                        {
                            Vector3 p = center + side * (1.83f + d.Next(0,.13f)) * sign;
                            var key = new Vector2Int(Mathf.RoundToInt(p.x * 2f),Mathf.RoundToInt(p.z * 2f));
                            if (!used.Add(key) || d.Road(new Vector2(p.x,p.z))) continue;
                            p.y = .38f;
                            var edge = Box("Broken_Curb", group,p,new Vector3(d.Next(.32f,.58f),.16f,d.Next(.18f,.31f)),d.edge);
                            edge.transform.localRotation = Quaternion.Euler(0,d.Next(-22,22) + Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg,0);
                        }
                        // Small recessed stone groups make lane scale and direction easier to read.
                        if (d.random.NextDouble() > .63)
                            for (int k = 0; k < 2; k++)
                            {
                                Vector3 p = center + side*d.Next(-.92f,.92f) + direction*d.Next(-.25f,.25f); p.y=.222f;
                                var stone = Box("Road_Pebble",group,p,new Vector3(d.Next(.11f,.24f),.025f,d.Next(.08f,.17f)),d.edge);
                                stone.transform.localRotation=Quaternion.Euler(0,d.Next(0,180),0);
                            }
                    }
                }
            // Entrance posts remain their existing gameplay markers, dressed with a banner and glow.
            var entrances = d.map.transform.Find("05_Entrances_And_Base");
            if (entrances)
                foreach (Transform marker in entrances)
                    if (marker.name.StartsWith("Entrance_", StringComparison.Ordinal) && marker.name.Contains("Post"))
                    {
                        Vector3 p=d.map.transform.InverseTransformPoint(marker.position);
                        Box("Entrance_Post_Cap",group,p+Vector3.up*.95f,new Vector3(.42f,.16f,.42f),d.metal);
                        Box("Entrance_Banner",group,p+new Vector3(.30f,.37f,0),new Vector3(.50f,.65f,.045f),d.gold);
                        Cylinder("Entrance_Lantern",group,p+Vector3.up*1.13f,new Vector3(.17f,.12f,.17f),d.glow);
                    }
        }

        static void Forest(Dressing d)
        {
            var pond=Group("04_Forest_Pond",d.root); Vector3 center=new Vector3(-2,.46f,8.7f);
            d.landmarks.Add(new Vector3(-2,0,8.7f));
            Disc(d,"Pond_Basin",pond,center,new Vector2(3.6f,2.2f),d.stone);
            Disc(d,"Still_Water",pond,center+Vector3.up*.022f,new Vector2(3.25f,1.93f),d.water);
            for(int i=0;i<14;i++)
            {
                float a=i*Mathf.PI*2f/14;
                Place(d,"detail-rocks",pond,new Vector3(center.x+Mathf.Sin(a)*3.5f,.42f,center.z+Mathf.Cos(a)*2.17f),d.Next(.55f,.9f),d.Next(0,360),d.stone);
            }
            var dock=Group("Fishing_Dock",pond);
            for(int i=0;i<6;i++)Box("Dock_Plank",dock,new Vector3(-2.8f+i*.32f,.57f,6.7f),new Vector3(.29f,.1f,1.12f),d.wood);
            foreach(float x in new[]{-2.85f,-1.15f})Box("Dock_Post",dock,new Vector3(x,.64f,6.7f),new Vector3(.1f,.5f,.1f),d.wood);
            var camp=Group("05_Forester_Camp",d.root);d.landmarks.Add(new Vector3(13,0,-9));
            Tent(d,camp,new Vector3(13,d.Height(13,-9),-9),1.6f);
            Tent(d,camp,new Vector3(16,d.Height(16,-10),-10),1.15f);
            Cylinder("Fire_Stone_Ring",camp,new Vector3(12,.56f,-6.8f),new Vector3(.5f,.055f,.5f),d.stone);
            Box("Campfire_Log",camp,new Vector3(12,.67f,-6.8f),new Vector3(.75f,.15f,.18f),d.wood);
            var log=Box("Crossed_Log",camp,new Vector3(12,.7f,-6.8f),new Vector3(.75f,.15f,.18f),d.wood);log.transform.localRotation=Quaternion.Euler(0,90,0);
            Place(d,"detail-crystal",camp,new Vector3(12,.76f,-6.8f),.62f,0,d.glow);
            for(int i=0;i<3;i++)Box("Supply_Crate",camp,new Vector3(15.2f+i*.55f,.73f,-8),new Vector3(.45f,.5f,.5f),d.wood);
            Fence(d,camp,new Vector3(11,.55f,-11.7f),new Vector3(17,.55f,-11.7f));
            var lookout=Group("06_Woodland_Lookout",d.root);d.landmarks.Add(new Vector3(-18,0,11));
            Place(d,"wood-structure-high",lookout,new Vector3(-18,.43f,11),1.9f,0,d.wood);
            Place(d,"tower-square-roof-b",lookout,new Vector3(-18,2.3f,11),2f,0,d.gold);
            Atmosphere(d,new Color(1f,.87f,.49f,.38f),false);
        }

        static void Canyon(Dressing d)
        {
            var mesas=Group("04_Layered_Canyon_Rock",d.root);
            Mesa(d,mesas,new Vector3(.2f,0,8),2.6f,2.0f,2.7f);
            Mesa(d,mesas,new Vector3(-15,0,0),2.0f,1.35f,1.9f);
            Mesa(d,mesas,new Vector3(15,0,9.6f),3.1f,2.0f,3.2f);
            var ruin=Group("05_Ancient_Ruin",d.root); d.landmarks.Add(new Vector3(9.8f,0,8.5f));
            Vector3 c=new Vector3(9.8f,.46f,8.5f);
            for(int i=0;i<3;i++)
                Box("Ruined_Step",ruin,c+new Vector3(0,.08f*i,-.65f*i),new Vector3(3.7f-i*.4f,.15f,1.4f),d.edge);
            foreach(float x in new[]{-1.25f,1.25f})
            {
                Cylinder("Column_Base",ruin,c+new Vector3(x,.2f,.4f),new Vector3(.65f,.14f,.65f),d.stone);
                Box("Old_Column",ruin,c+new Vector3(x,1.4f,.4f),new Vector3(.52f,2.5f,.55f),d.edge);
                Box("Column_Capital",ruin,c+new Vector3(x,2.68f,.4f),new Vector3(.75f,.2f,.74f),d.stone);
            }
            Box("Ancient_Lintel",ruin,c+new Vector3(0,2.86f,.4f),new Vector3(3.25f,.3f,.68f),d.edge);
            Place(d,"detail-crystal",ruin,c+new Vector3(0,.35f,.3f),1.2f,0,d.glow);
            Box("Fallen_Column",ruin,c+new Vector3(-2,.2f,-1),new Vector3(.6f,.65f,1.8f),d.stone).transform.localRotation=Quaternion.Euler(0,26,0);
            var oasis=Group("06_Stone_Basin",d.root);d.landmarks.Add(new Vector3(16,0,-11));
            Disc(d,"Basin_Rim",oasis,new Vector3(16,.47f,-10.9f),new Vector2(2.4f,1.4f),d.stone);
            Disc(d,"Mineral_Water",oasis,new Vector3(16,.50f,-10.9f),new Vector2(2.1f,1.12f),d.water);
            for(int i=0;i<6;i++)Place(d,"detail-crystal",oasis,new Vector3(14.3f+i*.7f,.43f,-12.25f),d.Next(.75f,1.25f),d.Next(0,360),d.gold);
        }

        static void SnowFortress(Dressing d)
        {
            var ice=Group("04_Frozen_Lake",d.root);d.landmarks.Add(new Vector3(-15,0,0));
            Disc(d,"Frozen_Shore",ice,new Vector3(-15,.48f,0),new Vector2(3.45f,2.1f),d.snow);
            Disc(d,"Blue_Ice",ice,new Vector3(-15,.50f,0),new Vector2(3.05f,1.8f),d.water);
            for(int i=0;i<7;i++)
            {
                var crack=Box("Ice_Fissure",ice,new Vector3(-16.8f+i*.55f,.525f,d.Next(-1.3f,1.3f)),new Vector3(.028f,.012f,d.Next(.5f,1.05f)),d.snow);
                crack.transform.localRotation=Quaternion.Euler(0,d.Next(-35,35),0);
            }
            Place(d,"snow-detail-crystal-large",ice,new Vector3(-18,.46f,-1.2f),2.1f,18,d.palette);
            var fort=Group("05_Central_Citadel",d.root);d.landmarks.Add(new Vector3(2,0,0));
            Box("Old_Courtyard",fort,new Vector3(1,.46f,0),new Vector3(14.5f,.10f,2.85f),d.stone);
            for(int i=0;i<8;i++)Box("Courtyard_Paving",fort,new Vector3(-5+i*1.8f,.52f,0),new Vector3(1.66f,.035f,2.65f),d.edge);
            foreach(float x in new[]{-6.7f,-.3f,6.5f})
            {
                Box("Wall_Footing",fort,new Vector3(x,.60f,0),new Vector3(2.4f,.26f,1.25f),d.metal);
                Box("Fortress_Wall",fort,new Vector3(x,.90f,0),new Vector3(2.1f,.50f,.72f),d.stone);
                Box("Wall_Snow",fort,new Vector3(x,1.18f,0),new Vector3(2.18f,.09f,.81f),d.snow);
                for(int i=-1;i<=1;i++)Box("Battlement",fort,new Vector3(x+i*.73f,1.24f,0),new Vector3(.34f,.12f,.88f),d.snow);
            }
            foreach(float x in new[]{-4,3.2f})
            {
                Cylinder("Citadel_Brazier",fort,new Vector3(x,.89f,0),new Vector3(.43f,.2f,.43f),d.metal);
                Place(d,"detail-crystal",fort,new Vector3(x,1.1f,0),.75f,0,d.glow);
            }
            var chapel=Group("06_Snow_Watchhouse",d.root);d.landmarks.Add(new Vector3(3.5f,0,11.8f));
            Place(d,"tower-square-bottom-b",chapel,new Vector3(3.5f,.45f,11.8f),2.5f,0,d.stone);
            Place(d,"tower-square-middle-b",chapel,new Vector3(3.5f,1.75f,11.8f),2.2f,0,d.stone);
            Place(d,"tower-square-roof-b",chapel,new Vector3(3.5f,2.8f,11.8f),2.6f,0,d.snow);
            Banner(d,chapel,new Vector3(1.1f,.44f,11.8f),2.9f);
            Banner(d,chapel,new Vector3(6,.44f,11.8f),2.9f);
            Atmosphere(d,new Color(.85f,.96f,1f,.65f),true);
        }

        static void PlantClusters(Dressing d)
        {
            var group=Group("07_Groves_And_Ground_Details",d.root);
            // One cluster occupies a pocket; trees vary in height and orbit its rocks.
            for(float z=-11.5f;z<=12;z+=3.8f)for(float x=-19;x<=20;x+=3.8f)
            {
                var center=new Vector2(x+d.Next(-.8f,.8f),z+d.Next(-.7f,.7f));
                if(!d.Clear(center,1.1f) || d.landmarks.Any(p=>Vector2.Distance(center,new Vector2(p.x,p.z))<4.3f))continue;
                if(d.random.NextDouble()>.7f)continue;
                int count=d.biome==1?2:3;
                for(int i=0;i<count;i++)
                {
                    var p=center+new Vector2(d.Next(-.75f,.75f),d.Next(-.65f,.65f));
                    if(!d.Clear(p,.7f))continue;
                    string model=d.biome==1?"detail-rocks-large":i==0?"detail-rocks":i==1?"detail-tree-large":"detail-tree";
                    if(d.biome==2)model="snow-"+model;
                    float scale=d.biome==1?d.Next(1.4f,2.5f):d.Next(1.25f,2.5f);
                    Place(d,model,group,new Vector3(p.x,d.Height(p.x,p.y),p.y),scale,d.Next(0,360),d.biome==1?d.stone:d.palette);
                }
            }
            // Small tufts, flowers and ice fragments fill bare pockets without hiding traffic.
            var detail=new Geometry();
            for(int i=0;i<190;i++)
            {
                var p=new Vector2(d.Next(-20.5f,20.5f),d.Next(-12.5f,12.5f));
                if(!d.Clear(p,.3f) || d.landmarks.Any(l=>Vector2.Distance(p,new Vector2(l.x,l.z))<2.8f))continue;
                float y=d.Height(p.x,p.y),h=d.Next(.11f,.25f),w=d.Next(.07f,.13f);
                detail.Triangle(new Vector3(p.x-w,y,p.y),new Vector3(p.x,y+h,p.y+.04f),new Vector3(p.x+w,y,p.y));
                detail.Triangle(new Vector3(p.x,y,p.y-w),new Vector3(p.x+.04f,y+h,p.y),new Vector3(p.x,y,p.y+w));
            }
            Material tint=Mat(d,"Ground_Accents",d.biome==2?new Color(.50f,.72f,.79f):d.biome==1?new Color(.77f,.62f,.29f):new Color(.50f,.62f,.30f));
            tint.SetFloat("_Cull",0f);
            MeshObject(d,"Wild_Ground_Details",group,detail.Create("Wild_Ground_Details"),tint);
        }

        static void Outskirts(Dressing d)
        {
            var group=Group("08_Backdrop_Ridges",d.root);
            Vector2[] clusters = d.biome == 1
                ? new[] {new Vector2(-20,18.7f),new Vector2(-1.5f,20),new Vector2(20,18.3f)}
                : new[] {new Vector2(-22,18.8f),new Vector2(-8,20.3f),new Vector2(8,17.6f),new Vector2(24,20.2f)};
            for(int cluster=0;cluster<clusters.Length;cluster++)
            {
                Vector2 center=clusters[cluster];
                if(d.biome==1)
                {
                    for(int j=0;j<2;j++)
                        Mesa(d,group,new Vector3(center.x+(j==0?-3.1f:2.8f)+d.Next(-.8f,.8f),0,center.y+d.Next(-1.4f,1.4f)),
                            d.Next(2.7f,3.8f),d.Next(2f,2.9f),d.Next(2.1f,4.0f));
                }
                else
                    for(int j=0;j<4+cluster%3;j++)
                    {
                        string model=d.biome==2?"snow-detail-tree-large":"detail-tree-large";
                        float angle=j*2.4f+d.Next(-.3f,.3f),radius=d.Next(1.2f,3.7f);
                        Place(d,model,group,new Vector3(center.x+Mathf.Sin(angle)*radius,.2f,center.y+Mathf.Cos(angle)*radius*.72f),
                            d.Next(2.5f,4.3f),d.Next(0,360),d.palette);
                    }
            }
            for(int side=-1;side<=1;side+=2)for(int z=-10;z<=12;z+=6)
            {
                string model=d.biome==2?"snow-detail-rocks-large":"detail-rocks-large";
                Place(d,model,group,new Vector3(side*d.Next(24,27),.05f,z),d.Next(2.3f,3.9f),d.Next(0,360),d.biome==1?d.stone:d.palette);
            }
            // Foreground remains low so roads and the build bar stay readable.
            for(int i=0;i<8;i++)
            {
                string model=d.biome==2?"snow-detail-rocks":"detail-rocks";
                Place(d,model,group,new Vector3(-21+i*6,.0f,-16.5f),d.Next(1.2f,1.9f),d.Next(0,360),d.biome==1?d.stone:d.palette);
            }
        }

        static void Lighting(Scene scene,Dressing d)
        {
            var sun=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).FirstOrDefault(l=>l.type==LightType.Directional);
            if(sun)
            {
                sun.color=d.biome==2?new Color(.81f,.91f,1f):d.biome==1?new Color(1f,.82f,.62f):new Color(1f,.94f,.78f);
                sun.intensity=d.biome==2?1.15f:1.25f;sun.shadows=LightShadows.Soft;
                sun.shadowBias=.035f;sun.shadowNormalBias=.18f;sun.transform.rotation=Quaternion.Euler(48,-32,0);
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=d.biome==2?new Color(.48f,.66f,.78f):new Color(.51f,.60f,.62f);
            RenderSettings.ambientEquatorColor=d.biome==2?new Color(.31f,.44f,.56f):d.biome==1?new Color(.40f,.32f,.24f):new Color(.30f,.42f,.33f);
            RenderSettings.ambientGroundColor=new Color(.17f,.22f,.24f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=d.biome==2?new Color(.50f,.68f,.78f):d.biome==1?new Color(.61f,.49f,.35f):new Color(.32f,.46f,.40f);
            RenderSettings.fogStartDistance=60;RenderSettings.fogEndDistance=130;
        }

        static void Atmosphere(Dressing d,Color color,bool snow)
        {
            var go=new GameObject(snow?"Gentle_Snowfall":"Sunlit_Dust");go.transform.SetParent(d.root,false);
            go.transform.localPosition=new Vector3(0,snow?7:2,0);
            var ps=go.AddComponent<ParticleSystem>();
            var main=ps.main;main.loop=true;main.playOnAwake=true;main.maxParticles=snow?95:35;
            main.startLifetime=snow?10f:7f;main.startSpeed=snow?.5f:.04f;main.startSize=snow?.075f:.035f;
            main.startColor=color;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(42,snow?1:2,26);
            var emission=ps.emission;emission.rateOverTime=snow?8f:4f;
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
            velocity.x=new ParticleSystem.MinMaxCurve(.1f);velocity.y=new ParticleSystem.MinMaxCurve(snow?-.5f:.06f);velocity.z=new ParticleSystem.MinMaxCurve(.03f);
            var lifetime=ps.colorOverLifetime;lifetime.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(color,0),new GradientColorKey(color,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(color.a,.15f),new GradientAlphaKey(color.a,.75f),new GradientAlphaKey(0,1)});
            lifetime.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=AtmosphereMaterial(d);
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        static Material AtmosphereMaterial(Dressing d)
        {
            string texturePath=d.folder+"/Soft_Particle.png";
            if(!File.Exists(texturePath))
            {
                var texture=new Texture2D(32,32,TextureFormat.RGBA32,false);
                for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                {
                    float distance=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
                    texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-distance),1.5f)));
                }
                texture.Apply();File.WriteAllBytes(texturePath,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            string path=d.folder+"/Atmosphere_Particles.mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m)
            {
                var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if(!shader)throw new InvalidOperationException("URP particle shader unavailable.");
                m=new Material(shader);AssetDatabase.CreateAsset(m,path);
            }
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            m.SetColor("_BaseColor",Color.white);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);
            m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType","Transparent");m.renderQueue=(int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);return m;
        }

        static void Mesa(Dressing d,Transform parent,Vector3 center,float rx,float rz,float height)
        {
            d.landmarks.Add(center);
            // Hand-shaped geological bands share silhouettes but step inward toward the summit.
            for(int i=0;i<3;i++)
            {
                float factor=1f-i*.19f;
                var material=Mat(d,"Stratum_"+i,new Color(.45f+i*.07f,.31f+i*.045f,.22f+i*.025f));
                Disc(d,"Mesa_"+parent.childCount+"_Band_"+i,parent,new Vector3(center.x,.41f+i*height*.3f,center.z),new Vector2(rx*factor,rz*factor),material,height*.36f);
            }
            Place(d,"detail-rocks",parent,new Vector3(center.x,.42f+height*.97f,center.z),rx*.65f,0,d.stone);
        }

        static void Tent(Dressing d,Transform parent,Vector3 position,float scale)
        {
            var root=Group("Canvas_Tent",parent);root.localPosition=position;
            Place(d,"wood-structure",root,Vector3.zero,scale,d.Next(-20,20),d.wood);
            Place(d,"tower-square-roof-b",root,new Vector3(0,.80f*scale,0),scale*1.1f,0,d.gold);
            Box("Tent_Door",root,new Vector3(0,.33f*scale,.43f*scale),new Vector3(.35f,.65f,.05f)*scale,d.wood);
            Banner(d,root,new Vector3(-scale,.0f,-.3f),1.6f*scale);
        }

        static void Banner(Dressing d,Transform parent,Vector3 p,float height)
        {
            Box("Banner_Pole",parent,p+Vector3.up*(height*.5f),new Vector3(.07f,height,.07f),d.metal);
            Box("Pennant",parent,p+new Vector3(.32f,height-.3f,0),new Vector3(.65f,.56f,.045f),d.gold);
            Cylinder("Pole_Cap",parent,p+Vector3.up*(height+.055f),new Vector3(.14f,.075f,.14f),d.glow);
        }

        static void Fence(Dressing d,Transform parent,Vector3 a,Vector3 b)
        {
            float distance=Vector3.Distance(a,b);
            for(float i=0;i<=distance;i+=1.25f)Box("Fence_Post",parent,Vector3.Lerp(a,b,i/distance)+Vector3.up*.35f,new Vector3(.12f,.8f,.12f),d.wood);
            for(int i=0;i<2;i++)Box("Fence_Rail",parent,(a+b)*.5f+Vector3.up*(.25f+i*.35f),new Vector3(distance,.09f,.09f),d.wood);
        }

        static void Disc(Dressing d,string name,Transform parent,Vector3 center,Vector2 radius,Material material,float height=.05f)
        {
            var geometry=new Geometry();const int n=14;
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                float ra=1f+.08f*Mathf.Sin(i*2.17f),rb=1f+.08f*Mathf.Sin((i+1)*2.17f);
                Vector3 p=new Vector3(Mathf.Sin(a)*radius.x*ra,0,Mathf.Cos(a)*radius.y*ra);
                Vector3 q=new Vector3(Mathf.Sin(b)*radius.x*rb,0,Mathf.Cos(b)*radius.y*rb);
                geometry.Triangle(Vector3.up*height,p+Vector3.up*height,q+Vector3.up*height);
                geometry.Quad(p,q,q+Vector3.up*height,p+Vector3.up*height);
            }
            // The object name is unique inside its authored group and stable across reruns.
            var go=MeshObject(d,name,parent,geometry.Create(name),material);go.transform.localPosition=center;
        }

        static GameObject MeshObject(Dressing d,string name,Transform parent,Mesh generated,Material material)
        {
            string groupKey=parent.name.Replace(' ','_');
            string path=d.folder+"/"+groupKey+"_"+name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh){EditorUtility.CopySerialized(generated,mesh);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(mesh);}
            else{mesh=generated;AssetDatabase.CreateAsset(mesh,path);}
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.receiveShadows=true;
            return go;
        }

        static GameObject Place(Dressing d,string model,Transform parent,Vector3 position,float scale,float yaw,Material material)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Kit+model+".fbx");
            if(!asset)throw new InvalidOperationException("Missing Kenney model: "+model);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
            go.transform.localPosition=position;go.transform.localScale=Vector3.one*scale;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>material).ToArray();
            foreach(var collider in go.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
            go.isStatic=true;return go;
        }

        static GameObject Box(string name,Transform parent,Vector3 position,Vector3 size,Material material)
            => Primitive(PrimitiveType.Cube,name,parent,position,size,material);
        static GameObject Cylinder(string name,Transform parent,Vector3 position,Vector3 size,Material material)
            => Primitive(PrimitiveType.Cylinder,name,parent,position,size,material);
        static GameObject Primitive(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;return go;
        }
        static Transform Group(string name,Transform parent){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static void RequireSavedEditMode()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before authoring map art.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your current scene edits before polishing maps.");
        }
    }
}
