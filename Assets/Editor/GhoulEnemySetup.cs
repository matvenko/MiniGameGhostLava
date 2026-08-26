using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-shot setup for the Sketchfab ghoul: import settings, a URP material,
// a looping chase controller, and the scene object EnemySpawnManager clones
// its third enemy kind from.
//
// This has to be a menu command rather than hand-authored asset files: the
// mesh, skeleton and animation clip inside an FBX only get their identities
// when Unity's model importer runs, so nothing can reference them until it
// has. Running it twice is safe - it replaces what it made last time.
public static class GhoulEnemySetup
{
    private const string ModelPath = "Assets/ghoul/source/Choul_Chase.fbx";
    private const string TextureFolder = "Assets/ghoul/textures";
    private const string MaterialPath = "Assets/ghoul/source/Materials/StingrayPBS1.mat";
    private const string ControllerPath = "Assets/ghoul/Ghoul.controller";
    private const string ScenePath = "Assets/LavaScene.unity";

    // The name the FBX gives its one material. The remap below is keyed on it,
    // which is what makes the model itself use our URP material instead of
    // importing a Standard-shader copy that renders magenta under URP.
    private const string FbxMaterialName = "StingrayPBS1";
    private const string EnemyName = "EnemyGhoul";

    // The mesh is authored 224 units tall. The board is a 1-unit grid and the
    // two ghosts stand about a unit, so this lands the ghoul at ~1.23 - a head
    // taller than them, which is rather the point of it.
    private const float ModelHeightUnits = 224f;
    private const float ModelScale = 0.0055f;

    // Floor tiles sit at y = -0.08 and are a unit thick, so their top face -
    // where the ghoul's feet belong - is here. The spawner keeps whatever
    // height the template has and only moves it in X/Z.
    private const float FloorTopY = 0.42f;

    // Runs itself once, rather than sitting in a menu waiting to be found: the
    // ask was for the ghoul to be in the game, not for a button that puts it
    // there. The guard is the spawner's own third slot - once that holds the
    // ghoul this never fires again, so there is no state to reset and nothing
    // to undo on the next assembly reload.
    [InitializeOnLoadMethod]
    private static void AutoSetUpOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return;

            var spawner = Object.FindAnyObjectByType<EnemySpawnManager>(FindObjectsInactive.Include);
            if (spawner == null) return;

            var kinds = new SerializedObject(spawner).FindProperty("enemyKinds");
            bool alreadyDone = kinds != null && kinds.arraySize >= 3 &&
                               kinds.GetArrayElementAtIndex(2).FindPropertyRelative("template").objectReferenceValue != null;
            if (alreadyDone) return;

            Run();
        };
    }

    [MenuItem("Tools/Ghost Lava/Set Up Ghoul Enemy")]
    public static void Run()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            Debug.LogError($"[Ghoul] No model at {ModelPath}.");
            return;
        }

        var spawner = Object.FindAnyObjectByType<EnemySpawnManager>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            Debug.LogError($"[Ghoul] Open {ScenePath} first - the ghoul is set up as a scene object there, " +
                           "and this will not discard whatever you have open to do it.");
            return;
        }

        ConfigureTextures();
        ConfigureModel();

        var material = CreateMaterial();
        RemapModelMaterial(material);

        var controller = CreateController();
        WireIntoScene(spawner, controller, material);
    }

    private static void ConfigureTextures()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

            bool isNormal = path.Contains("Normal");
            // Metallic, roughness and AO are measurements rather than colour;
            // reading them through sRGB would bend every value in them.
            bool isData = isNormal || path.Contains("Metallic") || path.Contains("Roughness") || path.Contains("AO");

            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isData;
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureModel()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);

        importer.globalScale = ModelScale;
        importer.useFileScale = false;

        // Generic, not Humanoid, and not by preference: the rig has a waist,
        // spine, arms and a tail and no legs at all, so there is nothing for a
        // Humanoid avatar's lower body to map onto.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;

        // Unity 6 dropped external material location outright, so the remap
        // added further down is the only way to point the model at a material
        // of our own instead of the Standard-shader one it builds itself.
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

        // The file holds one take, a one-second cycle of the ghoul lunging
        // forward, and it is the only thing this enemy ever plays - so it has
        // to loop or the ghoul freezes a second after it spawns.
        var clips = importer.defaultClipAnimations;
        if (clips.Length > 0)
        {
            clips[0].name = "Ghoul_Chase";
            clips[0].loopTime = true;
            importer.clipAnimations = clips;
        }

        importer.SaveAndReimport();
    }

    private static Material CreateMaterial()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.shader = shader;

        material.SetTexture("_BaseMap", LoadTexture("Base_Color"));
        material.SetTexture("_BumpMap", LoadTexture("Normal_OpenGL"));
        material.SetTexture("_OcclusionMap", LoadTexture("Mixed_AO"));
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_OCCLUSIONMAP");

        // URP Lit takes smoothness from an alpha channel, and the set ships
        // roughness as its own greyscale map instead - which URP has no input
        // for. The ghoul is stone and cloth, so a flat low smoothness reads
        // closer than a metallic map would.
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.2f);
        material.SetFloat("_OcclusionStrength", 1f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static Texture LoadTexture(string suffix)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains(suffix)) return AssetDatabase.LoadAssetAtPath<Texture>(path);
        }
        Debug.LogWarning($"[Ghoul] No texture matching '{suffix}' in {TextureFolder}.");
        return null;
    }

    private static void RemapModelMaterial(Material material)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), FbxMaterialName), material);
        importer.SaveAndReimport();
    }

    private static RuntimeAnimatorController CreateController()
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

        if (clip == null)
        {
            Debug.LogWarning("[Ghoul] The model imported without an animation clip - the ghoul will stand still.");
            return null;
        }

        AssetDatabase.DeleteAsset(ControllerPath);
        return AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
    }

    private static void WireIntoScene(EnemySpawnManager spawner, RuntimeAnimatorController controller, Material material)
    {
        var enemiesParent = GameObject.Find("Enemies");
        if (enemiesParent == null)
        {
            Debug.LogError("[Ghoul] No 'Enemies' object in the scene to put it under.");
            return;
        }

        var previous = enemiesParent.transform.Find(EnemyName);
        if (previous != null) Object.DestroyImmediate(previous.gameObject);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var ghoul = (GameObject)PrefabUtility.InstantiatePrefab(model, enemiesParent.transform);
        ghoul.name = EnemyName;
        ghoul.transform.position = new Vector3(0f, FloorTopY, 0f);
        ghoul.transform.rotation = Quaternion.identity;

        // The importer remap should already have done this, but that path has
        // been shifting between Unity versions and a magenta ghoul is a silly
        // way to lose an afternoon - so the instance is painted directly too.
        foreach (var renderer in ghoul.GetComponentsInChildren<Renderer>(true))
        {
            var slots = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = material;
            renderer.sharedMaterials = slots;
        }

        var animator = ghoul.GetComponent<Animator>();
        if (animator == null) animator = ghoul.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        // The chaser drives the transform itself; root motion would fight it.
        animator.applyRootMotion = false;

        // EnemyChaser catches the player through a trigger and needs some
        // collider to do it with - the model ships without one.
        float height = ModelHeightUnits * ModelScale;
        var capsule = ghoul.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, height * 0.5f, 0f);
        capsule.height = height;
        capsule.radius = 0.3f;
        capsule.isTrigger = true;

        // Adding this brings the Rigidbody with it (RequireComponent), and
        // EnemyChaser makes it kinematic itself on the first frame.
        var chaser = ghoul.AddComponent<EnemyChaser>();
        var chaserFields = new SerializedObject(chaser);
        // Optimal pathing and the quickest of the three: the ghost enemies
        // wander or plod, and this one is meant to be the thing you run from.
        chaserFields.FindProperty("strategy").enumValueIndex = 0;
        chaserFields.FindProperty("speed").floatValue = 3f;
        chaserFields.ApplyModifiedProperties();

        var spawnerFields = new SerializedObject(spawner);
        var kinds = spawnerFields.FindProperty("enemyKinds");
        while (kinds.arraySize < 3) kinds.InsertArrayElementAtIndex(kinds.arraySize);

        var ghoulKind = kinds.GetArrayElementAtIndex(2);
        ghoulKind.FindPropertyRelative("template").objectReferenceValue = ghoul;
        var counts = ghoulKind.FindPropertyRelative("countByLevel");
        counts.arraySize = 1;
        counts.GetArrayElementAtIndex(0).intValue = 1;
        spawnerFields.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(ghoul.scene);
        EditorSceneManager.SaveScene(ghoul.scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Ghoul] Ready: {height:0.00} units tall, one per level from level 1. Press Play.");
    }
}
