using UnityEngine;

// What the board is made of, and when it changes.
//
// The layout is the same game at every level - walkable rock, a hazard to fall
// into, a wall around it - so a new setting is a change of material and light
// rather than a change of level. The meadow runs the early levels; from the
// level the cave theme names, the grass becomes basalt, the water becomes lava
// and the sun goes out.
//
// Everything a theme touches is scene-wide state that would otherwise be set in
// three different components and the lighting window, which is exactly why it is
// gathered here: a theme is one row in the inspector, and adding a third setting
// later is another row rather than another pass through the game.
public class BoardThemes : MonoBehaviour
{
    public static BoardThemes Instance { get; private set; }

    [System.Serializable]
    public class Theme
    {
        [Tooltip("Shown in the inspector only.")]
        public string name = "Theme";
        [Tooltip("First level this theme is used on. The highest one a level reaches wins.")]
        public int fromLevel = 1;

        [Header("Surfaces")]
        [Tooltip("The walkable tiles themselves. This is the floor the player sees while the merged ground surface is switched off.")]
        public Material blockTile;
        [Tooltip("The hazard tiles under the liquid.")]
        public Material lavaTile;
        [Tooltip("The merged ground mesh, for boards that use it instead of the tiles.")]
        public Material ground;
        public Material liquid;
        [Tooltip("Optional floor of the pool, seen through the liquid.")]
        public Material liquidBed;
        public Material wall;

        [Header("Light")]
        [Tooltip("Off for anywhere the sun does not reach.")]
        public bool dayNightCycle = true;
        [ColorUsage(false, true)] public Color sunColour = Color.white;
        public float sunIntensity = 1.2f;
        [ColorUsage(false, true)] public Color ambient = new Color(.44f, .47f, .52f);

        [Header("Air")]
        public bool fog;
        public Color fogColour = new Color(.05f, .04f, .06f);
        [Range(0f, .2f)] public float fogDensity = .035f;
        [Tooltip("What fills the frame past the wall. Off keeps the skybox.")]
        public bool flatBackground;
        public Color backgroundColour = new Color(.05f, .035f, .05f);
    }

    [SerializeField] private Theme[] themes = new Theme[0];
    [Tooltip("The sun. Left empty, the first directional light in the scene is used.")]
    [SerializeField] private Light sun;
    [Tooltip("The camera that frames the board. Left empty, the scene's camera is used - it is not tagged MainCamera, so Camera.main does not find it.")]
    [SerializeField] private Camera boardCamera;

    private Theme _applied;

    void Awake()
    {
        Instance = this;
        if (sun == null)
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional) continue;
                sun = light;
                break;
            }
        }

        if (boardCamera == null) boardCamera = Camera.main;
        if (boardCamera == null)
        {
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // The one that draws the board: not a portrait studio's, not a
                // UI camera's, just the first that renders the world.
                if (camera.targetTexture != null) continue;
                boardCamera = camera;
                break;
            }
        }
    }

    // Called by LevelManager as each level's board is laid out, before the
    // surfaces rebuild - so the level that changes theme rebuilds once, with the
    // new materials already in place.
    public void ApplyForLevel(int level)
    {
        Theme theme = ThemeFor(level);
        if (theme == null || theme == _applied) return;
        _applied = theme;
        Apply(theme);
    }

    private Theme ThemeFor(int level)
    {
        Theme best = null;
        foreach (var theme in themes)
        {
            if (theme == null || theme.fromLevel > level) continue;
            if (best == null || theme.fromLevel >= best.fromLevel) best = theme;
        }
        return best;
    }

    private void Apply(Theme theme)
    {
        // Tiles first: the level manager lays them out immediately after this,
        // and skins each one as it decides what it is.
        if (LevelManager.Instance != null) LevelManager.Instance.SetTileMaterials(theme.blockTile, theme.lavaTile);
        if (GroundSurface.Instance != null) GroundSurface.Instance.SetMaterial(theme.ground);
        if (LiquidSurface.Instance != null) LiquidSurface.Instance.SetMaterials(theme.liquid, theme.liquidBed);
        if (WallSurface.Instance != null) WallSurface.Instance.SetMaterial(theme.wall);

        // The day/night cycle writes the sun and the ambient every frame, so
        // underground it has to be switched off rather than merely overridden.
        if (sun != null)
        {
            var cycle = sun.GetComponent<DayNightCycle>();
            if (cycle != null) cycle.enabled = theme.dayNightCycle;
            if (!theme.dayNightCycle)
            {
                sun.color = theme.sunColour;
                sun.intensity = theme.sunIntensity;
            }
        }

        if (!theme.dayNightCycle)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = theme.ambient;
        }

        RenderSettings.fog = theme.fog;
        if (theme.fog)
        {
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = theme.fogColour;
            RenderSettings.fogDensity = theme.fogDensity;
        }

        if (boardCamera != null)
        {
            boardCamera.clearFlags = theme.flatBackground ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            if (theme.flatBackground) boardCamera.backgroundColor = theme.backgroundColour;
        }
    }
}
