using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Copies imported art into project-owned URP prefabs; source packs stay intact.
public static class ForestEnvironmentBuilder
{
    const string Root = "Assets/Art/Forest";
    const string Fantasy = "Assets/Fantasy Forest Environment Free Sample/";
    const string Nature = "Assets/NatureStarterKit2/Nature/";
    const string Painted = "Assets/Silver_Cats/Hand_Painted_Nature_Kit_LITE/";
    static readonly Dictionary<Material,Material> converted = new Dictionary<Material,Material>();

    [MenuItem("Tools/Forest/Install woodland environment")]
    public static void Install()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop play mode before installing the forest.");
        if(GameObject.Find("Blocks")==null) throw new System.InvalidOperationException("Open the gameplay scene first.");
        Directory.CreateDirectory(Root+"/Materials"); Directory.CreateDirectory(Root+"/Prefabs"); Directory.CreateDirectory(Root+"/Meshes");
        AssetDatabase.Refresh(); converted.Clear();
        var trees = new List<GameObject>();
        trees.Add(ConvertPrefab(Fantasy+"Meshes/Prefabs/tree_1.prefab", "Fantasy canopy", 8.5f, true));
        trees.Add(trees[0]);trees.Add(trees[0]);
        trees.Add(ConvertPrefab(Nature+"tree01.prefab", "Birch", 8f, true));
        trees.Add(ConvertPrefab(Nature+"tree02.prefab", "Broadleaf", 8.5f, true));
        trees.Add(ConvertPrefab(Nature+"tree03.prefab", "Woodland tree", 7.8f, true));
        trees.Add(ConvertPrefab(Painted+"Prefabs/Pine_Tree.prefab", "Painted pine", 10.5f, true));
        var bushes = new List<GameObject>();
        for(int i=1;i<=6;i++) bushes.Add(ConvertPrefab(Nature+"bush0"+i+".prefab", "Bush "+i, 1.3f, false));
        var details = new List<GameObject>();
        details.Add(ConvertPrefab(Painted+"Prefabs/Fern.prefab", "Fern", .65f, false));
        details.Add(ConvertPrefab(Painted+"Prefabs/Dandelion_02.prefab", "Dandelion", .48f, false));
        details.Add(ConvertPrefab(Painted+"Prefabs/Plantain.prefab", "Plantain", .4f, false));
        details.Add(ConvertPrefab(Painted+"Prefabs/Boletus_Edulis.prefab", "Mushrooms", .36f, false));
        details.Add(ConvertPrefab(Fantasy+"Meshes/Prefabs/grass01.prefab", "Tuft", .6f, false));
        details.Add(ConvertPrefab(Painted+"Prefabs/Stump.prefab", "Stump", .65f, true));

        var themes=Object.FindFirstObjectByType<BoardThemes>();
        var serialized=new SerializedObject(themes);
        var meadow=serialized.FindProperty("themes").GetArrayElementAtIndex(0);
        Material original=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_BlockGrass2.mat");
        var grass=CopyMaterial(original,"Forest board grass");
        grass.SetTexture("_TopMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Fantasy+"Textures/grass01.tga"));
        grass.SetTexture("_TopMap2",AssetDatabase.LoadAssetAtPath<Texture2D>(Painted+"Textures/Forest/Forest_Grass_01_d.png"));
        grass.SetFloat("_TopBlend",.55f);grass.SetFloat("_MacroVariation",.16f);
        grass.SetFloat("_TopScale",2.8f); grass.SetFloat("_SeamStrength",.12f); grass.SetFloat("_SeamWidth",.025f);
        grass.SetFloat("_CellVariation",.055f); grass.SetColor("_BaseColor",new Color(1.1f,1.08f,.9f));
        grass.SetColor("_AmbientColor",new Color(.65f,.72f,.67f));
        var soil=CopyMaterial(grass,"Forest soil"); soil.SetFloat("_SeamStrength",0); soil.SetFloat("_CellVariation",0);
        soil.SetFloat("_TopScale",3.8f); soil.SetColor("_BaseColor",new Color(.8f,.98f,.82f));
        soil.SetFloat("_TopBlend",.42f);soil.SetFloat("_MacroVariation",.25f);
        var wall=CopyMaterial(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_WallBlocks.mat"),"Forest wall");
        wall.SetColor("_BaseColor",new Color(.8f,.86f,.67f)); wall.SetColor("_CapTint",new Color(.68f,.78f,.48f));
        meadow.FindPropertyRelative("blockTile").objectReferenceValue=grass;
        meadow.FindPropertyRelative("wall").objectReferenceValue=wall;
        meadow.FindPropertyRelative("sunColour").colorValue=new Color(1f,.91f,.73f);
        meadow.FindPropertyRelative("sunIntensity").floatValue=1.35f;
        meadow.FindPropertyRelative("flatAmbient").boolValue=true;
        meadow.FindPropertyRelative("ambient").colorValue=new Color(.40f,.49f,.45f);
        meadow.FindPropertyRelative("fog").boolValue=true;
        meadow.FindPropertyRelative("fogColour").colorValue=new Color(.22f,.35f,.30f);
        meadow.FindPropertyRelative("fogDensity").floatValue=.006f;
        meadow.FindPropertyRelative("flatBackground").boolValue=true;
        meadow.FindPropertyRelative("backgroundColour").colorValue=new Color(.22f,.35f,.30f);
        Undo.RecordObject(themes,"Woodland theme"); serialized.ApplyModifiedProperties();
        foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if(light.type==LightType.Directional)
        {
            Undo.RecordObject(light,"Forest sunlight"); Undo.RecordObject(light.transform,"Forest sunlight angle");
            light.transform.rotation=Quaternion.Euler(48,-35,0); light.color=new Color(1f,.91f,.73f); light.intensity=1.35f;
            light.shadows=LightShadows.Soft; light.shadowStrength=.7f; light.shadowBias=.035f; light.shadowNormalBias=.2f;
            var cycle=light.GetComponent<DayNightCycle>(); if(cycle!=null){Undo.RecordObject(cycle,"Forest lighting");cycle.enabled=false;}
        }
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.40f,.49f,.45f);
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.22f,.35f,.30f); RenderSettings.fogDensity=.006f;
        var lm=Object.FindFirstObjectByType<LevelManager>();
        var ls=new SerializedObject(lm);ls.FindProperty("blockMaterial").objectReferenceValue=grass;ls.ApplyModifiedProperties();
        var camera=Object.FindFirstObjectByType<CameraFollow>();
        if(camera!=null)
        {
            var cameraSettings=new SerializedObject(camera);
            cameraSettings.FindProperty("overviewPadding").floatValue=1.15f;
            cameraSettings.ApplyModifiedProperties();
        }
        foreach(var renderer in GameObject.Find("Blocks").GetComponentsInChildren<Renderer>())
        {Undo.RecordObject(renderer,"Forest grass");renderer.sharedMaterial=grass;}
        Object.FindFirstObjectByType<WallSurface>().SetMaterial(wall);
        var forest=Object.FindFirstObjectByType<ForestEnvironment>();
        if(forest==null){var go=new GameObject("Forest Environment");Undo.RegisterCreatedObjectUndo(go,"Create forest");forest=go.AddComponent<ForestEnvironment>();}
        Undo.RecordObject(forest,"Configure forest");
        forest.trees=trees.ToArray();forest.bushes=bushes.ToArray();forest.details=details.ToArray();forest.groundMaterial=soil;
        Preview();
        EditorUtility.SetDirty(forest); EditorUtility.SetDirty(grass);EditorUtility.SetDirty(soil);EditorUtility.SetDirty(wall);
        BoardSurfacePolish.Apply(false);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(forest.gameObject.scene);
        EditorSceneManager.SaveScene(forest.gameObject.scene);
        Debug.Log("Forest installed: URP vegetation, adaptive scenery and meadow lighting.");
    }

    [MenuItem("Tools/Forest/Rebuild preview")]
    public static void Preview()
    {
        var forest=Object.FindFirstObjectByType<ForestEnvironment>(); if(forest==null)return;
        Bounds bounds=new Bounds(); bool first=true;
        foreach(string name in new[]{"Blocks","Lava"}) foreach(Transform tile in GameObject.Find(name).transform)
        {if(first){bounds=new Bounds(tile.position,Vector3.one);first=false;}else bounds.Encapsulate(new Bounds(tile.position,Vector3.one));}
        forest.Rebuild(new Vector2(bounds.center.x,bounds.center.z),new Vector2(bounds.size.x,bounds.size.z),1);
    }

    static GameObject ConvertPrefab(string path,string name,float height,bool shadows)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(source==null)throw new System.IO.FileNotFoundException(path);
        var wrapper=new GameObject(name);var instance=new GameObject("Geometry");
        instance.transform.SetParent(wrapper.transform,false);
        // Read geometry directly: instantiating/removing legacy Tree components
        // can clear their shared TreeData references in recent Unity versions.
        var keep=new HashSet<Renderer>();
        foreach(var group in source.GetComponentsInChildren<LODGroup>(true))
        {var lods=group.GetLODs();if(lods.Length>0)foreach(var renderer in lods[0].renderers)if(renderer!=null)keep.Add(renderer);}
        if(keep.Count==0)foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))keep.Add(renderer);
        foreach(var original in keep)
        {
            var mesh=original.GetComponent<MeshFilter>().sharedMesh;
            if(mesh==null)throw new System.InvalidOperationException("Source mesh missing: "+path);
            var part=new GameObject(original.name,typeof(MeshFilter),typeof(MeshRenderer));
            part.transform.SetParent(instance.transform,false);
            var matrix=source.transform.worldToLocalMatrix*original.transform.localToWorldMatrix;
            part.transform.localPosition=matrix.GetColumn(3);part.transform.localRotation=matrix.rotation;part.transform.localScale=matrix.lossyScale;
            part.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=part.GetComponent<MeshRenderer>();
            var mats=original.sharedMaterials;
            for(int i=0;i<mats.Length;i++)mats[i]=ConvertMaterial(mats[i]);
            renderer.sharedMaterials=mats;renderer.enabled=true;
            renderer.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;
            renderer.receiveShadows=true;
        }
        Bounds b=new Bounds();bool first=true;
        foreach(var renderer in instance.GetComponentsInChildren<Renderer>()) {if(first){b=renderer.bounds;first=false;}else b.Encapsulate(renderer.bounds);}
        float scale=height/Mathf.Max(.01f,b.size.y);
        instance.transform.localScale*=scale;
        instance.transform.localPosition=new Vector3(-b.center.x*scale,-b.min.y*scale,-b.center.z*scale);
        var lod=wrapper.AddComponent<LODGroup>();
        lod.SetLODs(new[]{new LOD(shadows?.012f:.004f,instance.GetComponentsInChildren<Renderer>())});lod.RecalculateBounds();
        var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Root+"/Prefabs/"+name+".prefab");
        Object.DestroyImmediate(wrapper);return prefab;
    }

    static Material ConvertMaterial(Material source)
    {
        if(source==null)return null;
        if(AssetDatabase.GetAssetPath(source).StartsWith(Root+"/Materials/"))return source;
        if(converted.TryGetValue(source,out var existing))return existing;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long id);
        string path=Root+"/Materials/"+guid+"_"+id+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find("Forest/Foliage URP"));AssetDatabase.CreateAsset(mat,path);}
        mat.shader=Shader.Find("Forest/Foliage URP");mat.name=Path.GetFileNameWithoutExtension(path);
        Texture texture=source.HasProperty("_BaseMap")?source.GetTexture("_BaseMap"):null;
        if(texture==null && source.HasProperty("_MainTex"))texture=source.GetTexture("_MainTex");
        mat.SetTexture("_BaseMap",texture); mat.SetColor("_BaseColor",Color.white);
        bool cutout=source.name.ToLowerInvariant().Contains("leaf")||source.name.ToLowerInvariant().Contains("branches")||source.name.ToLowerInvariant().Contains("grassmesh")||source.name=="Atlas";
        mat.SetFloat("_AlphaClip",cutout?1:0);mat.SetFloat("_Cutoff",.4f);mat.SetFloat("_Cull",cutout?0:2);
        mat.SetColor("_AmbientColor",new Color(.6f,.7f,.63f));
        if(cutout){mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;}
        else{mat.DisableKeyword("_ALPHATEST_ON");mat.renderQueue=2000;}
        mat.enableInstancing=true;EditorUtility.SetDirty(mat);converted[source]=mat;return mat;
    }

    static Material CopyMaterial(Material source,string name)
    {
        string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(source);AssetDatabase.CreateAsset(mat,path);}else if(mat!=source)mat.CopyPropertiesFromMaterial(source);
        mat.name=name;return mat;
    }
}
