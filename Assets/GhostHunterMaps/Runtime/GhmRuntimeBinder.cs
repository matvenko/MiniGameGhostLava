using System.Reflection;
using UnityEngine;

namespace GhostHunterMaps
{
    // The published map's runtime half. Drop one of these in the scene, point it
    // at a profile, and the board the editor previewed is what the game plays.
    //
    // It never edits the game's scripts. It moves the existing tiles, asks the
    // existing surface components to refresh, sets their materials on the
    // renderer they already created, and adds the two things the game has no
    // concept of - paths and decor - as its own child objects.
    //
    // Levels are followed by polling LevelManager.CurrentLevel in LateUpdate:
    // that runs after the level manager has finished reshuffling for the new
    // level, which is exactly when this needs to lay its own layout over the top.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GhmRuntimeBinder : MonoBehaviour
    {
        public const string HostName = "GhostHunterMaps";

        [Header("Profile")]
        [SerializeField] private GhmMapProfile profile;
        [Tooltip("Loaded from Resources when no profile is assigned in the scene.")]
        [SerializeField] private string resourceFallback = GhmMapProfile.ResourcesFolder + "/" + GhmMapProfile.DefaultResourceName;

        [Header("What to drive")]
        [Tooltip("Re-lay the walkable/water split from the profile's algorithm after the game shuffles its own.")]
        [SerializeField] private bool applyLayout = true;
        [SerializeField] private bool applyMaterials = true;
        [SerializeField] private bool applyCamera = true;
        [SerializeField] private bool buildPaths = true;
        [SerializeField] private bool buildDecor = true;

        [Header("Editor")]
        [Tooltip("Level the board shows while the game is not running.")]
        [SerializeField] private int editorLevel = 1;
        [SerializeField] private bool rebuildInEditor = true;

        private GhmSceneBuilder.Result _built;
        private int _appliedLevel = -1;
        private GhmBoard _board;

        public GhmMapProfile Profile
        {
            get
            {
                if (profile == null && !string.IsNullOrEmpty(resourceFallback))
                    profile = Resources.Load<GhmMapProfile>(resourceFallback);
                return profile;
            }
            set { profile = value; }
        }

        public GhmBoard CurrentBoard => _board;
        public int EditorLevel { get => editorLevel; set => editorLevel = value; }

        private int ActiveLevel
        {
            get
            {
                if (Application.isPlaying && LevelManager.Instance != null) return LevelManager.Instance.CurrentLevel;
                return Mathf.Max(1, editorLevel);
            }
        }

        void OnEnable()
        {
            _appliedLevel = -1;
            Rebuild();
        }

        void OnDisable() => Clear();

        void Start()
        {
            if (Application.isPlaying) Rebuild();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (ActiveLevel != _appliedLevel) Rebuild();
        }

        void OnValidate()
        {
            if (!rebuildInEditor || Application.isPlaying) return;
            // OnValidate runs during serialization, where creating and destroying
            // objects is illegal; defer to the next editor tick.
            _appliedLevel = -1;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled) Rebuild();
            };
#endif
        }

        [ContextMenu("Rebuild")]
        public void Rebuild() => Rebuild(Application.isPlaying);

        // Publishing is what writes scene state; outside play mode this
        // component only maintains its own DontSave overlays. Otherwise merely
        // having it in the scene would move tiles and re-aim the camera on every
        // domain reload, quietly dirtying the scene behind the user.
        [ContextMenu("Rebuild (including tiles, materials and camera)")]
        public void RebuildWithSceneState() => Rebuild(true);

        public void Rebuild(bool writeSceneState)
        {
            Clear();

            var p = Profile;
            if (p == null) return;

            int level = ActiveLevel;
            _board = GhmGenerator.Generate(p, level);
            _appliedLevel = level;

            if (writeSceneState)
            {
                if (applyLayout && p.overrideRuntimeLayout) ApplyLayout(p, _board);
                if (applyMaterials) ApplyMaterials(p, level);
                if (applyCamera) ApplyCamera(p);
            }

            if (buildPaths || buildDecor)
            {
                _built = GhmSceneBuilder.Build(transform, _board, p, level,
                    includeSurfaces: false,
                    includePaths: buildPaths && p.publishPaths,
                    includeDecor: buildDecor && p.publishDecor,
                    flags: HideFlags.DontSave);
            }
        }

        public void Clear()
        {
            GhmSceneBuilder.Dispose(_built);
            _built = null;
        }

        // ------------------------------------------------------------------

        private void ApplyLayout(GhmMapProfile p, GhmBoard board)
        {
            var tiles = GhmTileGrid.Apply(p, board, allowResize: true);
            foreach (var extra in tiles.removed) GhmSceneBuilder.DestroyObject(extra.gameObject);

            // The two shared surfaces are generated from which cells are what, so
            // both have to be rebuilt now that the answer changed.
            if (LiquidSurface.Instance != null)
            {
                LiquidSurface.Instance.Build();
                LiquidSurface.Instance.Refresh();
            }
            if (GroundSurface.Instance != null) GroundSurface.Instance.Refresh();
            if (WallSurface.Instance != null) WallSurface.Instance.Refresh();

            if (Application.isPlaying)
            {
                EnemyPathGrid.Instance.Rebuild();
                GhmTileGrid.ResettleEntities(p, board);
            }
        }

        private void ApplyMaterials(GhmMapProfile p, int level)
        {
            var band = p.BandForLevel(level);
            if (band == null) return;

            var ground = FirstLayer(p, GhmLayerKind.Ground, level);
            var water = FirstLayer(p, GhmLayerKind.Water, level);
            var wall = FirstLayer(p, GhmLayerKind.Wall, level);

            if (water != null && LiquidSurface.Instance != null)
            {
                var mat = p.ResolveMaterial(water, band);
                if (mat != null) LiquidSurface.Instance.SetLiquidMaterial(mat);
            }

            // The floor and wall keep their material on the child renderer the
            // surface component generates, so it is set there rather than on the
            // component - no reaching into private serialized state at runtime.
            if (ground != null && GroundSurface.Instance != null)
                SetGeneratedMaterial(GroundSurface.Instance.transform, "GroundSurface", p.ResolveMaterial(ground, band));

            if (wall != null && WallSurface.Instance != null)
                SetGeneratedMaterial(WallSurface.Instance.transform, "WallSurface", p.ResolveMaterial(wall, band));
        }

        private static void SetGeneratedMaterial(Transform host, string childName, Material material)
        {
            if (material == null) return;
            var child = host.Find(childName);
            if (child == null) return;
            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = material;
        }

        // CameraFollow drives position from a serialized offset and never touches
        // rotation, so a tilted rig is "rotate the camera, and move the offset
        // back by the matching amount". ConfigureForMap is called first for the
        // clamp bounds, then the offset it computed is replaced with the tilted
        // one.
        private void ApplyCamera(GhmMapProfile p)
        {
            var cam = Camera.main;
            if (cam == null) return;

            cam.transform.rotation = p.CameraRotation;
            cam.fieldOfView = p.fieldOfView;

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) return;

            if (p.clampCameraToMap)
            {
                var bounds = p.FloorBounds;
                follow.ConfigureForMap(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z);
            }

            SetPrivateField(follow, "offset", p.CameraOffset);
        }

        // The offset is a private serialized field on a script this tool is not
        // allowed to modify. Reflection is the price of leaving it untouched;
        // being serialized, the field survives IL2CPP stripping.
        private static void SetPrivateField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info != null && info.FieldType == value.GetType()) info.SetValue(target, value);
        }

        private static GhmLayer FirstLayer(GhmMapProfile p, GhmLayerKind kind, int level)
        {
            foreach (var l in p.LayersOfKind(kind, level)) return l;
            return null;
        }
    }
}
