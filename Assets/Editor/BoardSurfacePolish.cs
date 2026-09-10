using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BoardSurfacePolish
{
    const string Root="Assets/Art/Forest/";

    [MenuItem("Tools/Forest/Polish board surfaces")]
    public static void Install() => Apply(true);

    public static void Apply(bool save)
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Edit the saved scene outside Play Mode.");
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/BoardGrass_v2.png");
        if(texture==null)return;
        var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        if(importer.wrapMode!=TextureWrapMode.Repeat||importer.maxTextureSize!=2048||importer.anisoLevel!=8)
        {
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;
            importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;
            importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        }
        var grass=Material("Meadow Grass v2",Shader.Find("Custom/BlockGround_URP"),AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Forest board grass.mat"));
        grass.SetTexture("_TopMap",texture);grass.SetFloat("_TopScale",6.5f);grass.SetFloat("_TopBlend",0);
        grass.SetFloat("_TextureVariation",.25f);grass.SetFloat("_MacroVariation",.065f);
        grass.SetFloat("_DetailStrength",.65f);grass.SetColor("_GrassBaseColor",new Color(.40f,.56f,.21f));
        grass.SetColor("_BaseColor",new Color(.95f,1f,.94f));grass.SetColor("_AmbientColor",new Color(.56f,.63f,.58f));
        grass.SetFloat("_SeamStrength",.055f);grass.SetFloat("_SeamWidth",.018f);grass.SetFloat("_CellVariation",.035f);
        grass.SetFloat("_SideBrightness",1.4f);grass.SetFloat("_Smoothness",0);

        var water=Material("Meadow Water v2",Shader.Find("Custom/StylizedWater_URP"));
        water.SetFloat("_MeadowShore",1);
        water.SetColor("_DeepColor",new Color(.025f,.35f,.43f));water.SetColor("_LightColor",new Color(.09f,.59f,.65f));
        water.SetColor("_CreaseColor",new Color(.4f,.83f,.8f));water.SetColor("_SparkleColor",new Color(.84f,.98f,.94f));
        water.SetColor("_ShoreColor",new Color(.19f,.7f,.63f));water.SetColor("_AmbientColor",new Color(.72f,.88f,.92f));
        water.SetFloat("_SunStrength",.38f);water.SetFloat("_ShadowStrength",.25f);water.SetFloat("_GlintStrength",.09f);
        water.SetFloat("_CellScale",3.8f);water.SetFloat("_CellContrast",.65f);water.SetFloat("_CreaseStrength",.22f);
        water.SetFloat("_CreaseWidth",.075f);water.SetFloat("_DetailWeight",.24f);water.SetFloat("_BroadWeight",.26f);
        water.SetFloat("_Speed",.13f);water.SetFloat("_Warp",.32f);water.SetFloat("_WarpSpeed",.85f);
        water.SetFloat("_SparkleStrength",.1f);

        var stone=Material("Mossy Masonry v2",Shader.Find("Forest/Mossy Stone URP"));
        stone.SetColor("_StoneColor",new Color(.46f,.50f,.47f));stone.SetColor("_MossColor",new Color(.19f,.34f,.075f));
        stone.SetFloat("_MossCoverage",.28f);stone.SetColor("_AmbientColor",new Color(.56f,.61f,.60f));
        stone.SetTexture("_StoneMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AureDevGames/Water Stylized Shader Orto & Perspective Camera/Textures/MossyRock/mossy_rock_diff_2k.png"));
        var bank=Material("Meadow Bank Grass v2",Shader.Find("Forest/Foliage URP"));
        bank.SetColor("_BaseColor",new Color(.32f,.5f,.10f));bank.SetFloat("_AlphaClip",0);bank.SetFloat("_Cull",0);
        bank.SetColor("_AmbientColor",new Color(.56f,.63f,.58f));EditorUtility.SetDirty(bank);
        var themes=Object.FindFirstObjectByType<BoardThemes>();Undo.RecordObject(themes,"Polish meadow surfaces");
        var settings=new SerializedObject(themes);var meadow=settings.FindProperty("themes").GetArrayElementAtIndex(0);
        meadow.FindPropertyRelative("blockTile").objectReferenceValue=grass;
        meadow.FindPropertyRelative("liquid").objectReferenceValue=water;
        meadow.FindPropertyRelative("wall").objectReferenceValue=stone;settings.ApplyModifiedProperties();
        var manager=Object.FindFirstObjectByType<LevelManager>();Undo.RecordObject(manager,"Polish board grass");
        var managerSettings=new SerializedObject(manager);managerSettings.FindProperty("blockMaterial").objectReferenceValue=grass;managerSettings.ApplyModifiedProperties();
        foreach(var renderer in GameObject.Find("Blocks").GetComponentsInChildren<Renderer>())
        {Undo.RecordObject(renderer,"Polish grass");renderer.sharedMaterial=grass;}
        var liquid=Object.FindFirstObjectByType<LiquidSurface>();Undo.RecordObject(liquid,"Polish pool water");
        var liquidSettings=new SerializedObject(liquid);liquidSettings.FindProperty("bankMaterial").objectReferenceValue=bank;liquidSettings.ApplyModifiedProperties();
        liquid.SetLiquidMaterial(water);liquid.Refresh();
        var walls=Object.FindFirstObjectByType<WallSurface>();Undo.RecordObject(walls,"Bevelled mossy stonework");walls.SetMaterial(stone);walls.Refresh();
        EditorUtility.SetDirty(grass);EditorUtility.SetDirty(water);EditorUtility.SetDirty(stone);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        if(save){AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(manager.gameObject.scene);}
    }

    static Material Material(string name,Shader shader,Material source=null)
    {
        string path=Root+"Materials/"+name+".mat";var result=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(result==null){result=source!=null?new Material(source):new Material(shader);result.name=name;AssetDatabase.CreateAsset(result,path);}
        result.shader=shader;return result;
    }
}
