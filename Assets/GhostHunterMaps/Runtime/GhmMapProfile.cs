using System.Collections.Generic;
using UnityEngine;

namespace GhostHunterMaps
{
    // The whole authored document: board size, camera framing, the layer stack,
    // the texture catalogue and the per-level bands. One asset is one map style;
    // the editor window edits it and the runtime binder reads it back, so what
    // was previewed and what the game shows come from the same numbers.
    [CreateAssetMenu(menuName = "Ghost Hunter Maps/Map Profile", fileName = "GhostMapProfile")]
    public class GhmMapProfile : ScriptableObject
    {
        public const string ResourcesFolder = "GhostHunterMaps";
        public const string DefaultResourceName = "ActiveMapProfile";

        [Header("Board")]
        [Tooltip("Cells across the board on the world X axis.")]
        [Range(4, 80)] public int width = 15;
        [Tooltip("Cells along the world Z axis.")]
        [Range(4, 80)] public int height = 10;
        [Tooltip("World units per cell. The shipped board is laid out on a 1.0 pitch.")]
        public float cellSize = 1f;
        [Tooltip("World XZ the board stays centred on while width and height change.")]
        public Vector2 boardCenter = new Vector2(1.52f, 1f);
        [Tooltip("World Y of a walkable tile's centre.")]
        public float floorY = -0.08f;
        [Tooltip("How far a water tile is recessed below the floor.")]
        public float waterDrop = 0.18f;
        [Tooltip("Cells of wall ring around the floor footprint.")]
        [Range(0, 4)] public int wallMargin = 1;

        [Header("Generation")]
        public int seed = 20260825;
        [Tooltip("Level the preview is currently showing.")]
        public int previewLevel = 1;
        [Tooltip("Re-roll the layout for every level instead of keeping one fixed board.")]
        public bool perLevelLayout = true;
        [Tooltip("Let the published board use these algorithms instead of the layout LevelManager shuffles on its own.")]
        public bool overrideRuntimeLayout = true;
        [Tooltip("Smallest island of floor the cleanup keeps; smaller specks are flooded.")]
        [Range(1, 20)] public int minIslandSize = 4;
        [Tooltip("Keep at least this share of the board walkable, whatever the algorithm asks for.")]
        [Range(0.25f, 0.95f)] public float minWalkableShare = 0.55f;

        [Header("Camera")]
        [Tooltip("1 is straight down. Lower values tilt the camera, squashing the board along Z by exactly this factor.")]
        [Range(0.3f, 1f)] public float compression = 1f;
        [Tooltip("Height the camera rides above the player.")]
        [Range(3f, 30f)] public float cameraHeight = 7f;
        [Range(20f, 90f)] public float fieldOfView = 60f;
        [Tooltip("Yaw of the whole rig, in degrees.")]
        [Range(-180f, 180f)] public float cameraYaw = 0f;
        [Tooltip("Clamp the camera so the empty world outside the wall never shows.")]
        public bool clampCameraToMap = true;

        [Header("Lighting (preview + publish)")]
        public Color sunColor = new Color(1f, 0.96f, 0.88f);
        [Range(0f, 3f)] public float sunIntensity = 1.15f;
        [Range(0f, 90f)] public float sunPitch = 52f;
        [Range(-180f, 180f)] public float sunYaw = -35f;
        public Color ambientColor = new Color(0.42f, 0.46f, 0.52f);

        [Header("Content")]
        public List<GhmLayer> layers = new List<GhmLayer>();
        public List<GhmLevelBand> bands = new List<GhmLevelBand>();
        public List<GhmTextureEntry> catalog = new List<GhmTextureEntry>();

        [Header("Publish")]
        [Tooltip("Scene the publish button writes the board into.")]
        public string targetScenePath = "Assets/LavaScene.unity";
        public bool publishCamera = true;
        public bool publishLighting = false;
        public bool publishDecor = true;
        public bool publishPaths = true;

        // Cell (0,0) is the centre of the corner tile; the board grows
        // symmetrically around boardCenter so resizing never drags the level off
        // whatever the rest of the scene is arranged around.
        public Vector3 CellToWorld(int x, int z, bool water)
        {
            float ox = boardCenter.x - (width - 1) * 0.5f * cellSize;
            float oz = boardCenter.y - (height - 1) * 0.5f * cellSize;
            return new Vector3(ox + x * cellSize, water ? floorY - waterDrop : floorY, oz + z * cellSize);
        }

        public Vector3 Origin => CellToWorld(0, 0, false);

        public Bounds FloorBounds
        {
            get
            {
                Vector3 min = CellToWorld(0, 0, false);
                Vector3 max = CellToWorld(width - 1, height - 1, false);
                var b = new Bounds((min + max) * 0.5f, Vector3.zero);
                b.Encapsulate(min - new Vector3(cellSize, 0f, cellSize) * 0.5f);
                b.Encapsulate(max + new Vector3(cellSize, 0f, cellSize) * 0.5f);
                return b;
            }
        }

        // The camera is a plain offset follow, so the tilt has to be expressed
        // as an offset plus a rotation. compression is cos(tilt from vertical),
        // which is exactly the factor the board is foreshortened by, so the
        // slider reads as "how squashed the map looks".
        public float TiltFromVertical => Mathf.Acos(Mathf.Clamp(compression, 0.05f, 1f)) * Mathf.Rad2Deg;
        public float CameraPitch => 90f - TiltFromVertical;

        public Vector3 CameraOffset
        {
            get
            {
                float pitch = Mathf.Max(CameraPitch, 1f) * Mathf.Deg2Rad;
                float back = cameraHeight / Mathf.Tan(pitch);
                float yaw = cameraYaw * Mathf.Deg2Rad;
                return new Vector3(-Mathf.Sin(yaw) * back, cameraHeight, -Mathf.Cos(yaw) * back);
            }
        }

        public Quaternion CameraRotation => Quaternion.Euler(CameraPitch, cameraYaw, 0f);

        public GhmLevelBand BandForLevel(int level)
        {
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i].Covers(level)) return bands[i];
            }
            return bands.Count > 0 ? bands[bands.Count - 1] : null;
        }

        public GhmLayer FirstLayer(GhmLayerKind kind)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].kind == kind) return layers[i];
            }
            return null;
        }

        public IEnumerable<GhmLayer> LayersOfKind(GhmLayerKind kind, int level)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].kind == kind && layers[i].ActiveAtLevel(level)) yield return layers[i];
            }
        }

        // Materials come from the band when it overrides them, otherwise from
        // the layer. Resolved in one place so the preview and the publisher can
        // never disagree about which one wins.
        public Material ResolveMaterial(GhmLayer layer, GhmLevelBand band)
        {
            if (band != null)
            {
                switch (layer.kind)
                {
                    case GhmLayerKind.Ground: if (band.groundMaterial != null) return band.groundMaterial; break;
                    case GhmLayerKind.Water: if (band.waterMaterial != null) return band.waterMaterial; break;
                    case GhmLayerKind.Wall: if (band.wallMaterial != null) return band.wallMaterial; break;
                    case GhmLayerKind.Path: if (band.pathMaterial != null) return band.pathMaterial; break;
                }
            }
            return layer.material;
        }

        public Color ResolveTint(GhmLayer layer, GhmLevelBand band)
        {
            if (band == null) return layer.tint;
            switch (layer.kind)
            {
                case GhmLayerKind.Ground: return layer.tint * band.groundTint;
                case GhmLayerKind.Water: return layer.tint * band.waterTint;
                case GhmLayerKind.Wall: return layer.tint * band.wallTint;
            }
            return layer.tint;
        }

        public int LevelSeed(int level) => perLevelLayout ? seed + level * 7919 : seed;

        // A profile with nothing in it produces an empty preview, which reads as
        // a bug rather than as an empty document; every new asset starts as the
        // board the game currently ships with.
        public void EnsureDefaults()
        {
            if (layers.Count == 0)
            {
                layers.Add(GhmLayer.Create(GhmLayerKind.Ground, "Ground"));
                layers.Add(GhmLayer.Create(GhmLayerKind.Water, "Water"));
                layers.Add(GhmLayer.Create(GhmLayerKind.Wall, "Wall"));
                var p = GhmLayer.Create(GhmLayerKind.Path, "Paths");
                p.tint = new Color(0.76f, 0.63f, 0.44f, 1f);
                layers.Add(p);
                var d = GhmLayer.Create(GhmLayerKind.Decor, "Decor");
                d.rules.Clear();
                d.rules.Add(new GhmDecorRule { name = "Flowers", per100Cells = 18f, placement = GhmPlacement.OffPath, baseScale = 0.35f, seedSalt = 1 });
                d.rules.Add(new GhmDecorRule { name = "Small stones", per100Cells = 10f, placement = GhmPlacement.OffPath, baseScale = 0.3f, seedSalt = 2 });
                d.rules.Add(new GhmDecorRule { name = "Shore reeds", per100Cells = 14f, placement = GhmPlacement.Shore, baseScale = 0.45f, seedSalt = 3 });
                layers.Add(d);
            }

            if (bands.Count == 0)
            {
                bands.Add(new GhmLevelBand { name = "Meadow", minLevel = 1, maxLevel = 5, waterDensity = 0.24f });
                bands.Add(new GhmLevelBand { name = "Deep garden", minLevel = 6, maxLevel = 10, waterDensity = 0.3f, algorithm = GhmAlgorithm.Rivers });
                bands.Add(new GhmLevelBand { name = "Flooded", minLevel = 11, maxLevel = 999, waterDensity = 0.36f, algorithm = GhmAlgorithm.Archipelago });
            }
        }
    }
}
