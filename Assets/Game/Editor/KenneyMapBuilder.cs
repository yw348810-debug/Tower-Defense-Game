using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TowerDefense.Maps;

namespace TowerDefense.EditorTools
{
    /// <summary>Creates editable scene assets from the imported Kenney modular kit.</summary>
    public static class KenneyMapBuilder
    {
        const string Kit = "Assets/ThirdParty/Kenney/TowerDefenseKit";
        const string Maps = "Assets/Game/Scenes";
        const string Materials = "Assets/Game/Materials";
        const float TileSize = 2f;
        const int Width = 21, Height = 13;
        static Material standard, autumn, stone, foundation, padAccent, routeA, routeB, core, snowCap;

        sealed class Layout
        {
            public string id, title;
            public int gold;
            public Vector2Int[][] routes;
            public Vector2Int[] slots;
        }

        static Vector2Int P(int x, int z) => new Vector2Int(x, z);
        static Vector3 World(Vector2Int p, float y = .24f) => new Vector3((p.x - 10) * TileSize, y, (p.y - 6) * TileSize);

        static Layout[] Layouts() => new[]
        {
            new Layout { id="Level01_ForestOutpost",title="林间哨站",gold=220,
                routes=new[]{new[]{P(0,9),P(4,9),P(4,3),P(13,3),P(13,9),P(20,9)},
                    new[]{P(0,3),P(4,3),P(13,3),P(13,9),P(20,9)}},
                slots=new[]{P(2,7),P(6,7),P(6,1),P(11,5),P(16,7),P(18,11)} },
            new Layout { id="Level02_CanyonConfluence",title="岩谷汇流",gold=220,
                routes=new[]{new[]{P(0,10),P(6,10),P(6,6),P(13,6),P(13,3),P(20,3)},
                    new[]{P(0,2),P(8,2),P(8,6),P(13,6),P(13,3),P(20,3)}},
                slots=new[]{P(2,8),P(8,11),P(7,8),P(2,4),P(6,4),P(10,1),P(11,4),P(17,5)} },
            new Layout { id="Level03_CoreFortress",title="核心要塞",gold=240,
                routes=new[]{new[]{P(0,10),P(6,10),P(6,8),P(17,8),P(17,6),P(20,6)},
                    new[]{P(0,2),P(6,2),P(6,4),P(17,4),P(17,6),P(20,6)}},
                slots=new[]{P(3,8),P(9,10),P(11,9),P(15,10),P(3,4),P(9,2),P(11,3),P(15,2),P(18,8),P(18,4)} }
        };

        [MenuItem("Tools/Tower Defense/Create Missing Kenney Maps")]
        public static void BuildMissingMaps()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before building maps.");
            Directory.CreateDirectory(Maps);
            Directory.CreateDirectory(Materials);
            Directory.CreateDirectory("Captures/Maps");
            AssetDatabase.Refresh();
            PrepareMaterials();
            var original = SceneManager.GetActiveScene();
            var existingRoots = Enumerable.Range(0, SceneManager.sceneCount)
                .SelectMany(i => SceneManager.GetSceneAt(i).GetRootGameObjects()).ToArray();
            var active = existingRoots.Select(g => g.activeSelf).ToArray();
            var created = new List<string>();
            try
            {
                // Isolate preview lighting; restore every original root's state in finally.
                foreach (var root in existingRoots) root.SetActive(false);
                var layouts = Layouts();
                for (int i = 0; i < layouts.Length; i++)
                {
                    string path = Maps + "/" + layouts[i].id + ".unity";
                    if (File.Exists(path)) continue;
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    try
                    {
                        SceneManager.SetActiveScene(scene);
                        BuildMap(layouts[i], i);
                        EditorSceneManager.SaveScene(scene, path);
                        Capture(scene, "Captures/Maps/" + layouts[i].id + ".png");
                        created.Add(path);
                    }
                    finally { EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally
            {
                for (int i = 0; i < existingRoots.Length; i++)
                    if (existingRoots[i]) existingRoots[i].SetActive(active[i]);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Kenney map creation complete: " + string.Join(", ", created));
        }

        static Material Material(string name, Texture texture, Color color)
        {
            string path = Materials + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result) return result;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is unavailable.");
            result = new Material(shader) { name = name, color = color, enableInstancing = true };
            result.SetFloat("_Smoothness", 0f);
            if (texture) result.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        static void PrepareMaterials()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "/Models/Textures/colormap.png");
            var variant = AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "/Models/Textures/variation-a.png");
            if (!texture || !variant) throw new InvalidOperationException("Kenney palette textures are missing.");
            foreach (var tex in new[] { texture, variant })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
                if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled)
                {
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            standard = Material("Kenney_Default_URP", texture, Color.white);
            autumn = Material("Kenney_Canyon_URP", variant, new Color(1f,.96f,.87f));
            stone = Material("Fortress_Stone", null, new Color(.40f,.46f,.49f));
            foundation = Material("Map_Foundation", null, new Color(.19f,.24f,.24f));
            padAccent = Material("Build_Pad_Accent", null, new Color(.86f,.83f,.56f));
            routeA = Material("Entrance_A", null, new Color(.93f,.38f,.16f));
            routeB = Material("Entrance_B", null, new Color(.18f,.65f,.86f));
            core = Material("Base_Core", null, new Color(.23f,.88f,.73f));
            snowCap = Material("Fortress_Snow", null, new Color(.84f,.91f,.98f));
            core.EnableKeyword("_EMISSION");
            core.SetColor("_EmissionColor", new Color(.12f,.5f,.3f));
            EditorUtility.SetDirty(core);
            // All source FBXs remain reusable with a single shared URP palette material.
            var paths = AssetDatabase.FindAssets("t:Model", new[] { Kit + "/Models" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            foreach (var path in paths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var embedded = model.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                    .Where(m => m && m != standard).Distinct().ToArray();
                if (embedded.Length == 0) continue;
                foreach (var mat in embedded)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mat.name), standard);
                importer.SaveAndReimport();
            }
        }

        static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static GameObject Model(string name, Transform parent, Vector3 position, float scale = 2f, float rotation = 0f, Material material = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "/Models/" + name + ".fbx");
            if (!asset) throw new InvalidOperationException("Missing Kenney model: " + name);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, rotation, 0);
            go.transform.localScale = Vector3.one * scale;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => material ? material : standard).ToArray();
            go.isStatic = true;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = true;
            return go;
        }

        static List<Vector2Int> Expand(Vector2Int[] corners)
        {
            var result = new List<Vector2Int> { corners[0] };
            for (int i = 1; i < corners.Length; i++)
            {
                var from = corners[i-1]; var to = corners[i];
                if (from.x != to.x && from.y != to.y) throw new InvalidOperationException("Roads must follow the tile grid.");
                var step = new Vector2Int(Math.Sign(to.x-from.x), Math.Sign(to.y-from.y));
                while (from != to) { from += step; result.Add(from); }
            }
            return result;
        }

        // Direction mask: north=1, east=2, south=4, west=8. FBX corners open north/west.
        static int Direction(Vector2Int delta) => delta.y > 0 ? 1 : delta.x > 0 ? 2 : delta.y < 0 ? 4 : 8;
        static int RotateMask(int mask) => ((mask << 1) & 15) | (mask >> 3);

        static void BuildMap(Layout data, int index)
        {
            var mapRoot = Group(data.id);
            var map = mapRoot.gameObject.AddComponent<LevelMap>();
            map.levelId = data.id; map.displayName = data.title; map.initialGold = data.gold;
            var ground = Group("01_Terrain", mapRoot);
            var roads = Group("02_RoadTiles", mapRoot);
            var routeRoot = Group("03_EnemyRoutes", mapRoot);
            var slotRoot = Group("04_BuildSlots", mapRoot);
            var landmarks = Group("05_Entrances_And_Base", mapRoot);
            var decor = Group("06_Scenery", mapRoot);
            Material palette = index == 1 ? autumn : standard;
            var expanded = data.routes.Select(Expand).ToArray();
            var masks = new Dictionary<Vector2Int, int>();
            foreach (var path in expanded)
                for (int i = 0; i < path.Count; i++)
                {
                    int mask = 0;
                    if (i > 0) mask |= Direction(path[i-1] - path[i]);
                    if (i < path.Count-1) mask |= Direction(path[i+1] - path[i]);
                    if (i == 0) mask |= 8; // Entrances extend to the left board edge.
                    if (i == path.Count-1) mask |= 2;
                    masks[path[i]] = (masks.TryGetValue(path[i], out int old) ? old : 0) | mask;
                }
            Box("Island_Foundation", ground, new Vector3(0,-.35f,0), new Vector3(42.4f,.7f,26.4f), foundation);
            for (int z=0; z<Height; z++) for (int x=0; x<Width; x++)
            {
                var p = P(x,z);
                string model="tile"; float rotation=0;
                bool road = masks.TryGetValue(p, out int mask);
                if (road)
                {
                    int count = Convert.ToString(mask,2).Count(c=>c=='1');
                    int basis;
                    if (count==4) { model="tile-crossing"; basis=15; }
                    else if (count==3) { model="tile-split"; basis=11; }
                    else if (mask==5 || mask==10) { model="tile-straight"; basis=5; }
                    else { model="tile-corner-square"; basis=9; }
                    while (basis != mask) { basis=RotateMask(basis); rotation+=90; if (rotation>270) throw new InvalidOperationException("Invalid road mask."); }
                }
                if(index==2) model="snow-"+model;
                var tile=Model(model,road ? roads : ground,World(p,0),TileSize,rotation,palette);
                tile.name=(road?"Road_":"Ground_")+x.ToString("00")+"_"+z.ToString("00");
            }
            var surface=ground.gameObject.AddComponent<BoxCollider>();
            surface.center=new Vector3(0,.1f,0); surface.size=new Vector3(42,.2f,26);
            var routes=new List<EnemyRoute>();
            for(int i=0;i<data.routes.Length;i++)
            {
                var route=Group("Route_"+(char)('A'+i),routeRoot).gameObject.AddComponent<EnemyRoute>();
                route.routeId=((char)('A'+i)).ToString();
                route.waypoints=data.routes[i].Select((p,n)=> { var t=Group("Waypoint_"+n.ToString("00"),route.transform);t.position=World(p);return t;}).ToArray();
                routes.Add(route);
                var start=World(data.routes[i][0],.23f);
                Model("spawn-square",landmarks,start,2.1f,0,i==0?routeA:routeB).name="Entrance_"+route.routeId;
                for(int k=-1;k<=1;k+=2)
                    Box("Entrance_"+route.routeId+"_Post",landmarks,start+new Vector3(0,.85f,k*.85f),new Vector3(.3f,1.7f,.3f),i==0?routeA:routeB);
            }
            map.routes=routes.ToArray();
            map.buildSlots=data.slots.Select((p,n)=>
            {
                var root=Group("T"+(n+1).ToString("00"),slotRoot);
                root.position=World(p,.41f);
                var slot=root.gameObject.AddComponent<BuildSlot>();
                slot.slotId="T"+(n+1); slot.previewRadius=5.5f;
                slot.serviceArea=index==0?"A":index==1?(n<3?"A":n<6?"B":"Shared"):(n<4?"A":n<8?"B":"Exit");
                Model("tower-round-base",root,Vector3.zero,1.55f);
                Model("selection-a",root,new Vector3(0,.35f,0),1.65f,0,padAccent);
                var collider=root.gameObject.AddComponent<BoxCollider>();
                collider.center=new Vector3(0,.2f,0);collider.size=new Vector3(1.65f,.45f,1.65f);
                return slot;
            }).ToArray();
            var end=data.routes[0].Last();
            map.baseAnchor=Group("BaseAnchor",landmarks); map.baseAnchor.position=World(end);
            var baseVisual=Group("Core_Visual",landmarks); baseVisual.position=World(end,.24f);
            Model("tower-square-bottom-a",baseVisual,Vector3.zero,2f);
            Model("tower-square-top-a",baseVisual,new Vector3(0,1,0),2f);
            Model(index==2?"snow-detail-crystal-large":"detail-crystal-large",baseVisual,new Vector3(0,1.85f,0),2.1f,25,core);
            var occupied=new HashSet<Vector2Int>(masks.Keys.Concat(data.slots));
            var random=new System.Random(103+index);
            for(int z=0;z<Height;z++) for(int x=0;x<Width;x++)
            {
                var p=P(x,z);
                if(occupied.Any(q=>Math.Abs(q.x-x)<=1&&Math.Abs(q.y-z)<=1))continue;
                if(index==2 && x>=1 && x<=14 && Math.Abs(z-6)<=1)continue;
                if(random.NextDouble()>(x==0||x==20||z==0||z==12?.7:.36))continue;
                string model=index==0?(random.Next(3)==0?"detail-rocks-large":"detail-tree-large"):
                    index==1?(random.Next(4)==0?"detail-crystal":"detail-rocks-large"):
                    (random.Next(3)==0?"snow-detail-tree":"snow-detail-rocks");
                Model(model,decor,World(p,.4f),2f+(float)random.NextDouble()*1.6f,random.Next(360),palette);
            }
            if(index==2)
            {
                for(int x=2;x<=13;x+=2)
                {
                    Box("Fortress_Divider",decor,World(P(x,6),.95f),new Vector3(3.3f,1.1f,.65f),stone);
                    Box("Wall_Snow",decor,World(P(x,6),1.53f),new Vector3(3.4f,.08f,.75f),snowCap);
                    Box("Wall_Cap",decor,World(P(x,6),1.55f),new Vector3(.7f,.3f,.9f),snowCap);
                }
                for(int z=0;z<=12;z+=3)
                    if(z!=6) Model("tower-square-bottom-a",decor,World(P(20,z),.4f),1.5f);
            }
            var light=new GameObject("Sun").AddComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.35f; light.color=new Color(1f,.96f,.87f);
            if(index==2) { light.intensity=1.15f; light.color=new Color(.88f,.94f,1f); }
            light.shadows=LightShadows.Soft;light.shadowBias=.035f; light.transform.rotation=Quaternion.Euler(52,-35,0);
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=index==2?new Color(.65f,.73f,.86f):new Color(.66f,.73f,.8f);
            RenderSettings.fog=false; RenderSettings.skybox=null;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag="MainCamera";
            camera.orthographic=false;
            camera.transform.position=new Vector3(0,36,-29);
            camera.transform.LookAt(new Vector3(0,0,0));
            camera.nearClipPlane=.1f;camera.farClipPlane=150;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=index==2?new Color(.15f,.22f,.32f):index==1?new Color(.18f,.20f,.22f):new Color(.12f,.19f,.24f);
            camera.gameObject.AddComponent<AudioListener>();
            var errors=map.GetValidationErrors();
            if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
            MapPresentationSetup.Configure(SceneManager.GetActiveScene());
        }

        static void Capture(Scene scene, string path)
        {
            var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
            var texture=new RenderTexture(1440,900,24);
            var image=new Texture2D(1440,900,TextureFormat.RGB24,false);
            var old=RenderTexture.active;
            try
            {
                camera.targetTexture=texture;
                var rig=camera.GetComponent<TowerDefense.Maps.MapCameraRig>();
                if(rig)rig.ApplyFraming();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active=texture;
                image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
                File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=old;
                var rig=camera.GetComponent<TowerDefense.Maps.MapCameraRig>();
                if(rig)rig.ApplyFraming();
                UnityEngine.Object.DestroyImmediate(image);texture.Release();UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
