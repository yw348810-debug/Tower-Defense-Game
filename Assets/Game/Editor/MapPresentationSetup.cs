using System;
using System.Linq;
using TowerDefense.Maps;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TowerDefense.EditorTools
{
    public static class MapPresentationSetup
    {
        const string Models = "Assets/ThirdParty/Kenney/TowerDefenseKit/Models/";

        public static void Configure(Scene scene)
        {
            var map = scene.GetRootGameObjects().Select(g=>g.GetComponent<LevelMap>()).First(m=>m);
            var camera = scene.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).First(c=>c);
            var rig = camera.GetComponent<MapCameraRig>();
            if (!rig) rig = camera.gameObject.AddComponent<MapCameraRig>();
            rig.map=map;rig.pitch=36f;rig.yaw=-7f;rig.fieldOfView=44f;
            rig.focusOffset=new Vector3(0,.5f,-2.5f);
            rig.gameplayViewport=new Rect(.025f,.10f,.95f,.77f);
            rig.ApplyFraming();

            bool canyon=map.levelId.Contains("Level02"), snow=map.levelId.Contains("Level03");
            if (!map.transform.Find("07_Outskirts"))
            {
                var outskirts=new GameObject("07_Outskirts").transform;
                outskirts.SetParent(map.transform,false);
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/"+
                    (canyon?"Kenney_Canyon_URP.mat":"Kenney_Default_URP.mat"));
                var ground=Place(snow?"snow-tile":"tile",outskirts,new Vector3(0,-.22f,0),1f,0,material);
                ground.name="Surrounding_Ground";
                ground.transform.localScale=new Vector3(200f,1f,200f);
                var random=new System.Random(snow?341:canyon?340:339);
                for(int z=-30;z<=36;z+=5)
                    for(int x=-38;x<=38;x+=5)
                    {
                        if(Mathf.Abs(x)<24&&Mathf.Abs(z)<17)continue;
                        if(random.NextDouble()>.52)continue;
                        bool nearForeground=z<-13&&Mathf.Abs(x)<24;
                        string model=canyon||nearForeground?"detail-rocks-large":random.Next(4)==0?"detail-rocks":"detail-tree-large";
                        if(snow)model="snow-"+model;
                        float scale=nearForeground?1.6f:2.5f+(float)random.NextDouble()*2f;
                        Place(model,outskirts,new Vector3(x+(float)random.NextDouble(),0,z+(float)random.NextDouble()),scale,random.Next(360),material);
                    }
            }
            var sun=scene.GetRootGameObjects().Select(g=>g.GetComponent<Light>()).First(l=>l);
            sun.intensity=snow?1.15f:1.25f;
            sun.color=snow?new Color(.88f,.94f,1f):new Color(1f,.95f,.83f);
            sun.transform.rotation=Quaternion.Euler(44,-38,0);
            sun.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=snow?new Color(.4f,.48f,.62f):new Color(.32f,.39f,.46f);
        }

        static GameObject Place(string model,Transform parent,Vector3 position,float scale,float rotation,Material material)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Models+model+".fbx");
            if(!asset)throw new InvalidOperationException("Missing environment model: "+model);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
            go.transform.localPosition=position;go.transform.localScale=Vector3.one*scale;
            go.transform.localRotation=Quaternion.Euler(0,rotation,0);
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>material).ToArray();
            go.isStatic=true;
            return go;
        }
    }
}
