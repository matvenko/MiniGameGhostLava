using TMPro;
using UnityEditor;
using UnityEngine;

// Puts the first-time tour into the open scene and points it at what it shows:
// the coin counter, the wallet and the settings gear on the HUD, the friendly ghost, and the guide
// book's art for its card. The tour draws everything else itself at runtime, so
// all this adds to the scene is one object.
//
// Safe to run again: it finds the tour it placed last time and rewires it.
internal static class FirstTimeTourBuilder
{
    private const string TourName = "FirstTimeTour";
    private const string BookArt = "Assets/UI/Icons/GuideBook/";
    private const string SettingsArt = "Assets/UI/Icons/Settings/";
    private const string FontDir = "Assets/UI/fonts/TMP/";

    [MenuItem("Tools/Maze Boo/Build First-Time Tour")]
    public static void Build()
    {
        Canvas canvas = HudScene.FindCanvas();
        if (canvas == null) return;

        var coins = canvas.transform.Find("Coins_Ui") as RectTransform;
        var wallet = canvas.transform.Find("Wallet_Ui") as RectTransform;
        var settings = canvas.transform.Find("Settings_Ui") as RectTransform;
        if (coins == null || wallet == null || settings == null)
        {
            Debug.LogError("[Tour] The HUD has no Coins_Ui, Wallet_Ui or Settings_Ui to point at. Build the " +
                           "coin counter, the wallet badge and the settings button first.");
            return;
        }

        GameObject go = null;
        foreach (GameObject top in canvas.gameObject.scene.GetRootGameObjects())
            if (top.name == TourName) go = top;
        if (go == null) go = new GameObject(TourName);

        var tour = go.GetComponent<FirstTimeTour>();
        if (tour == null) tour = go.AddComponent<FirstTimeTour>();

        var level = Object.FindAnyObjectByType<LevelManager>(FindObjectsInactive.Include);
        var friendly = level != null
            ? new SerializedObject(level).FindProperty("friendlyGhost").objectReferenceValue
            : null;

        var so = new SerializedObject(tour);
        so.FindProperty("coinCounter").objectReferenceValue = coins;
        so.FindProperty("shopButton").objectReferenceValue = wallet;
        so.FindProperty("settingsButton").objectReferenceValue = settings;
        so.FindProperty("friendlyGhost").objectReferenceValue = friendly;
        so.FindProperty("hudCanvas").objectReferenceValue = canvas;

        bool complete = true;
        complete &= Wire(so, "cardSprite", Load<Sprite>(BookArt + "card.png"));
        complete &= Wire(so, "chipSprite", Load<Sprite>(BookArt + "chip.png"));
        complete &= Wire(so, "tipSprite", Load<Sprite>(BookArt + "tip.png"));
        complete &= Wire(so, "closeSprite", Load<Sprite>(SettingsArt + "close.png"));
        complete &= Wire(so, "titleFont", Load<TMP_FontAsset>(FontDir + "Fredoka-SemiBold SDF.asset"));
        complete &= Wire(so, "labelFont", Load<TMP_FontAsset>(FontDir + "Fredoka-Medium SDF.asset"));
        complete &= Wire(so, "bodyFont", Load<TMP_FontAsset>(FontDir + "Fredoka-Regular SDF.asset"));
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!complete)
            Debug.LogWarning("[Tour] Some of the guide book's art is missing (see above) - the tour will fall " +
                             "back to plain panels. Tools/Build Guide Book makes it.");

        HudScene.Save(canvas);
        Debug.Log("[Tour] First-time tour placed in " + canvas.gameObject.scene.name + ".");
        Selection.activeGameObject = go;
    }

    // So the tour can be watched again from the start without clearing every
    // other preference the game keeps.
    [MenuItem("Tools/Maze Boo/Reset First-Time Tour")]
    public static void ResetSeen()
    {
        FirstTimeTour.ForgetAll();
        Debug.Log("[Tour] Everything the tour has shown is forgotten; it plays again from the next level start.");
    }

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogWarning("[Tour] Nothing at " + path + ".");
        return asset;
    }

    private static bool Wire(SerializedObject so, string field, Object value)
    {
        so.FindProperty(field).objectReferenceValue = value;
        return value != null;
    }
}
