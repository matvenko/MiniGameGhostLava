using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class IceThemeBuilder
{
    const string Folder="Assets/Art/Ice";
    [MenuItem("MazeBoo/Environment/Install Ice Cavern Theme")]
    public static void Install()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Install outside Play mode.");
        var themes=Object.FindFirstObjectByType<BoardThemes>();
        var forest=Object.FindFirstObjectByType<ForestEnvironment>();
        if(themes==null||forest==null)throw new System.InvalidOperationException("Open LavaScene first.");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art","Ice");
        var surface=Shader.Find("MazeBoo/Ice Cavern Surface");
        var waterShader=Shader.Find("MazeBoo/Glacial Rift Water");
        if(surface==null||waterShader==null)throw new System.InvalidOperationException("Ice shaders have not imported.");
        var ground=Material("Frozen ground",surface,new Color(.17f,.33f,.44f),.38f,0,0);
        var wall=Material("Glacial masonry",surface,new Color(.16f,.36f,.49f),.73f,.22f,1);
        var snow=Material("Snow shelves",surface,new Color(.27f,.43f,.54f),.88f,0,0);
        var crystal=Material("Blue ice crystals",surface,new Color(.11f,.37f,.53f),.12f,1,0);
        var water=Material("Rift water",waterShader,Color.white,0,0,0);
        var bed=Material("Rift depths",surface,new Color(.018f,.035f,.07f),0,0,0);
        Undo.RecordObject(themes,"Install ice theme");Undo.RecordObject(forest,"Assign ice scenery materials");
        var so=new SerializedObject(themes);var list=so.FindProperty("themes");int ice=-1;
        for(int i=0;i<list.arraySize;i++)
        {
            var row=list.GetArrayElementAtIndex(i);
            if(row.FindPropertyRelative("name").stringValue=="Ice Cavern")ice=i;
            else if(row.FindPropertyRelative("fromLevel").intValue==11)row.FindPropertyRelative("fromLevel").intValue=17;
        }
        if(ice<0){ice=list.arraySize;list.InsertArrayElementAtIndex(ice);}
        var theme=list.GetArrayElementAtIndex(ice);
        theme.FindPropertyRelative("name").stringValue="Ice Cavern";
        theme.FindPropertyRelative("fromLevel").intValue=11;
        Set(theme,"blockTile",ground);Set(theme,"ground",ground);Set(theme,"lavaTile",water);
        Set(theme,"liquid",water);Set(theme,"liquidBed",bed);Set(theme,"wall",wall);
        theme.FindPropertyRelative("sunColour").colorValue=new Color(.73f,.87f,1);
        theme.FindPropertyRelative("sunIntensity").floatValue=1.05f;
        theme.FindPropertyRelative("flatAmbient").boolValue=true;
        theme.FindPropertyRelative("ambient").colorValue=new Color(.36f,.44f,.57f);
        theme.FindPropertyRelative("skyAmbientIntensity").floatValue=1;
        theme.FindPropertyRelative("fog").boolValue=true;
        theme.FindPropertyRelative("fogColour").colorValue=new Color(.075f,.13f,.21f);
        theme.FindPropertyRelative("fogDensity").floatValue=.014f;
        theme.FindPropertyRelative("flatBackground").boolValue=true;
        theme.FindPropertyRelative("backgroundColour").colorValue=new Color(.045f,.08f,.14f);
        so.ApplyModifiedProperties();forest.snowMaterial=snow;forest.crystalMaterial=crystal;
        EditorUtility.SetDirty(themes);EditorUtility.SetDirty(forest);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(themes.gameObject.scene);
        EditorSceneManager.SaveScene(themes.gameObject.scene);
        Debug.Log("Installed Ice Cavern at 11–16; previous cave resumes at 17. Meadow and Burnt rows preserved.");
    }
    static void Set(SerializedProperty p,string name,Material mat)=>p.FindPropertyRelative(name).objectReferenceValue=mat;
    static Material Material(string name,Shader shader,Color colour,float snow,float crystal,float masonry)
    {
        string path=Folder+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.shader=shader;
        if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",colour);
        if(mat.HasProperty("_SnowCoverage"))mat.SetFloat("_SnowCoverage",snow);
        if(mat.HasProperty("_Crystal"))mat.SetFloat("_Crystal",crystal);
        if(mat.HasProperty("_Masonry"))mat.SetFloat("_Masonry",masonry);
        EditorUtility.SetDirty(mat);return mat;
    }
}
