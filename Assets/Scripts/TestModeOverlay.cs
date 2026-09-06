using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A small bar of test controls: pay the wallet, and finish the level on the
// spot. Both are things a run normally takes a long time to reach, and both
// have to be reachable on the phone rather than only in the editor - a level
// that only misbehaves on the fifth board is not a level anyone should have to
// play four boards to see again.
//
// It shows itself in the editor and in development builds and nowhere else, so
// a release APK handed to a player has no cheat bar in it and there is nothing
// to remember to switch off before shipping.
//
// Built in code rather than authored into the scene: it is a tool, it wants no
// art, and a scene that carries no cheat buttons cannot accidentally ship them
// wired to something.
public class TestModeOverlay : MonoBehaviour
{
    [Tooltip("Paid into the wallet each time the coin button is pressed.")]
    [SerializeField] private int coinsPerPress = 1000;
    [Tooltip("Leave off to build the bar in a release player too - for a tester who is not running a development build.")]
    [SerializeField] private bool developmentBuildsOnly = true;

    private TextMeshProUGUI _levelLabel;

    // It puts itself into the game rather than being placed in the scene. A
    // scene that carries no cheat buttons cannot ship them by accident, there
    // is nothing to delete before a release build, and the tool cannot be lost
    // to someone tidying the hierarchy. It rides in the gameplay scene's own
    // object list, so it leaves with the level like everything else there.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Debug.isDebugBuild && !Application.isEditor) return;
        // The board is what these controls act on: no level manager, no bar.
        if (LevelManager.Instance == null) return;
        if (FindAnyObjectByType<TestModeOverlay>(FindObjectsInactive.Include) != null) return;
        new GameObject("Test Mode").AddComponent<TestModeOverlay>();
    }

    void Start()
    {
        if (developmentBuildsOnly && !Debug.isDebugBuild && !Application.isEditor)
        {
            enabled = false;
            return;
        }
        Build();
    }

    void Update()
    {
        if (_levelLabel == null) return;
        _levelLabel.text = LevelManager.Instance != null ? "LEVEL " + LevelManager.Instance.CurrentLevel : "TEST";
    }

    private void Build()
    {
        var root = new GameObject("Test Mode", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above the shop and the pause card: the point of it is to be reachable
        // while one of those is up.
        canvas.sortingOrder = 500;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;

        // Bottom centre, clear of the joystick on the left and the ability bar
        // on the right, and clear of the HUD along the top.
        var bar = Rect(root.transform, "Bar", new Vector2(0, 74), new Vector2(560, 108));
        bar.anchorMin = bar.anchorMax = new Vector2(.5f, 0f);
        Box(bar, "Backing", Vector2.zero, new Vector2(560, 108), new Color(.04f, .05f, .12f, .82f));
        _levelLabel = Label(bar, "Level", "TEST", new Vector2(0, 36), new Vector2(540, 30), 22, new Color(.62f, .70f, .92f));

        Button(bar, "Coins", "+" + coinsPerPress, new Vector2(-136, -14), new Vector2(250, 60),
            new Color(.16f, .52f, .25f), GiveCoins);
        Button(bar, "Skip", "SKIP LEVEL", new Vector2(136, -14), new Vector2(250, 60),
            new Color(.42f, .26f, .58f), SkipLevel);
    }

    private void GiveCoins()
    {
        if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(coinsPerPress);
        // The shop reads the wallet when it opens, so it is only worth
        // refreshing while it is already up.
        if (ShopUIController.Instance != null && ShopUIController.Instance.IsOpen)
            ShopUIController.Instance.Refresh();
    }

    // Finishes the board through the same door the last coin does, so the level
    // complete card, its Next button and everything they set up behave exactly
    // as they do in a played-out level.
    private void SkipLevel()
    {
        if (LevelManager.Instance == null || LevelManager.Instance.IsLevelCompleteActive) return;
        LevelManager.Instance.OnLevelComplete();
    }

    // ---- the little bit of UI it needs -------------------------------------

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        node.SetParent(parent, false);
        node.anchorMin = node.anchorMax = new Vector2(.5f, .5f);
        node.anchoredPosition = position;
        node.sizeDelta = size;
        return node;
    }

    private static Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color colour)
    {
        var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 position, Vector2 size,
        float fontSize, Color colour)
    {
        var label = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.color = colour;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static void Button(Transform parent, string name, string text, Vector2 position, Vector2 size,
        Color colour, UnityEngine.Events.UnityAction action)
    {
        var image = Box(parent, name, position, size, colour);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colours = button.colors;
        colours.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colours.pressedColor = new Color(.7f, .7f, .7f);
        button.colors = colours;
        button.onClick.AddListener(action);
        Label(image.transform, "Label", text, Vector2.zero, size, 26, Color.white);
    }
}
