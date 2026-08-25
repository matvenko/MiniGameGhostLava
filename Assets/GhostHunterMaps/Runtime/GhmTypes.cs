using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhostHunterMaps
{
    // What a single board cell is. The game only distinguishes walkable floor
    // from hazard liquid; Wall exists so the border ring can be described by the
    // same grid instead of a second parallel structure.
    public enum GhmCell : byte
    {
        Water = 0,
        Ground = 1,
        Wall = 2
    }

    // Layers are the editor's unit of authoring. Each kind owns one visual
    // aspect of the board and carries only the fields that aspect needs; the
    // inspector panel switches on this.
    public enum GhmLayerKind
    {
        Ground,
        Water,
        Wall,
        Path,
        Decor
    }

    public enum GhmTextureCategory
    {
        Ground,
        Water,
        Wall,
        Path,
        Decor,
        Other
    }

    // How the walkable/liquid split is decided. ShuffleConnected is the rule the
    // shipped game already uses (flip random cells to water, keep only the flips
    // that leave every floor cell reachable); the rest are shape generators that
    // run first and then get repaired by the same connectivity pass, so every
    // algorithm is guaranteed to produce a completable board.
    public enum GhmAlgorithm
    {
        ShuffleConnected,
        Caves,
        Rivers,
        Rooms,
        Archipelago
    }

    // Where a decor rule is allowed to drop something. Combined as a mask: a
    // rule with Shore|OffPath only lands on cells near the water that no path
    // crosses.
    [Flags]
    public enum GhmPlacement
    {
        Anywhere = 0,
        Inland = 1 << 0,
        Shore = 1 << 1,
        OnPath = 1 << 2,
        OffPath = 1 << 3,
        PathEdge = 1 << 4,
        Corner = 1 << 5
    }

    // Flat quads lying on the ground read correctly from straight above; once
    // the camera is tilted, tall things (bushes, lanterns) need to stand up.
    // Lean is normally derived from the camera compression so both stay right.
    public enum GhmDecorStance
    {
        FollowCamera,
        FlatOnGround,
        Upright
    }

    public enum GhmDecorSource
    {
        Texture,
        Prefab
    }

    // One entry of the bottom-left texture catalogue. Slices cut out of an
    // imported sheet keep a pointer back to the sheet they came from, so the
    // catalogue can group them and re-slice without a second import.
    [Serializable]
    public class GhmTextureEntry
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "texture";
        public Texture2D texture;
        public GhmTextureCategory category = GhmTextureCategory.Decor;
        public Vector2 tiling = Vector2.one;
        public Color tint = Color.white;
        [Tooltip("Sheet this was sliced out of, if any.")]
        public Texture2D sourceSheet;
        public Rect sourceRect;
        [Tooltip("World size in cells this texture is authored for, used as the default decor scale.")]
        public float authoredCells = 1f;
        public bool favourite;
    }

    // One scattering rule inside a Decor layer: what to drop, how often, where
    // it is allowed to land and how much it is allowed to vary.
    [Serializable]
    public class GhmDecorRule
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "Decor";
        public bool enabled = true;

        public GhmDecorSource source = GhmDecorSource.Texture;
        public Texture2D texture;
        public GameObject prefab;
        public Material materialOverride;

        [Tooltip("Expected instances per 100 walkable cells.")]
        [Range(0f, 200f)] public float per100Cells = 12f;
        [Tooltip("Minimum distance in cells between two instances of this rule.")]
        [Range(0f, 6f)] public float minSpacing = 0.7f;
        [Tooltip("Instances per cluster. 1 scatters evenly, higher values group them.")]
        [Range(1, 8)] public int clusterSize = 1;
        [Range(0f, 2f)] public float clusterRadius = 0.6f;

        public GhmPlacement placement = GhmPlacement.Anywhere;
        [Tooltip("Cells from the water an Inland rule must keep clear of.")]
        [Range(0f, 6f)] public float inlandMargin = 1.2f;
        [Tooltip("Cells from the water a Shore rule stays inside.")]
        [Range(0f, 6f)] public float shoreBand = 1.1f;

        public Vector2 scaleRange = new Vector2(0.8f, 1.15f);
        public float baseScale = 1f;
        [Range(0f, 180f)] public float yawJitter = 180f;
        [Range(0f, 0.5f)] public float positionJitter = 0.32f;
        public float yOffset = 0.02f;
        public Color tint = Color.white;
        [Range(0f, 1f)] public float tintVariation = 0.12f;

        public GhmDecorStance stance = GhmDecorStance.FollowCamera;
        [Tooltip("Pivot height inside the quad. 0 puts the quad's bottom edge on the ground.")]
        [Range(0f, 1f)] public float pivot = 0f;

        public int minLevel = 1;
        public int maxLevel = 999;
        [Tooltip("Extra seed so two rules with identical settings do not stack on the same cells.")]
        public int seedSalt = 0;

        public GhmDecorRule Clone()
        {
            var c = (GhmDecorRule)MemberwiseClone();
            c.id = Guid.NewGuid().ToString("N");
            return c;
        }
    }

    // A Path layer walks a network between anchor cells and paints it onto the
    // floor as a separate translucent mesh, so the ground surface underneath is
    // never touched.
    [Serializable]
    public class GhmPathSettings
    {
        [Range(0, 12)] public int anchors = 4;
        [Range(0.3f, 3f)] public float width = 1f;
        [Range(0f, 1f)] public float wander = 0.35f;
        [Tooltip("How strongly a route prefers to merge into a path already laid down.")]
        [Range(0f, 1f)] public float reuse = 0.7f;
        [Tooltip("Keep routes this many cells away from the water where possible.")]
        [Range(0f, 4f)] public float shoreAvoidance = 0.8f;
        [Range(0f, 1f)] public float edgeSoftness = 0.55f;
        [Range(0f, 1f)] public float opacity = 0.9f;
        public float yOffset = 0.012f;
        public Vector2 tiling = new Vector2(0.5f, 0.5f);
        [Tooltip("Round off single-cell dead ends and staircase corners.")]
        public bool smooth = true;
        [Tooltip("Loop the last anchor back to the first, so the network has no dead end.")]
        public bool closeLoop = false;
    }

    // One authored layer. Kind decides which of these blocks the inspector
    // shows; everything else stays at its default and is ignored.
    [Serializable]
    public class GhmLayer
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "Layer";
        public GhmLayerKind kind = GhmLayerKind.Decor;
        public bool visible = true;
        public bool locked = false;

        public Material material;
        public Texture2D texture;
        public Color tint = Color.white;
        public Vector2 tiling = Vector2.one;
        public float yOffset = 0f;

        [Header("Ground")]
        [Range(1, 6)] public int facetsPerCell = 2;
        public float heightJitter = 0.035f;
        public float skirtDepth = 1.1f;
        [Range(0f, 1f)] public float colorVariation = 0.55f;
        [Range(2, 6)] public int colorZones = 4;
        [Range(1f, 12f)] public float zoneScale = 3.2f;
        [Range(1f, 4f)] public float zoneContrast = 2.4f;
        [Range(0f, 0.4f)] public float undulation = 0.09f;
        [Range(2f, 20f)] public float undulationScale = 6f;
        [Range(0.5f, 5f)] public float shoreWidth = 0.9f;
        [Range(0f, 0.3f)] public float shoreDip = 0.05f;
        [Range(0f, 0.3f)] public float waterClearance = 0.1f;

        [Header("Water")]
        public Material bedMaterial;
        public float surfaceYOffset = 0f;
        public float bedDepth = 0.6f;
        public float padding = 0.5f;
        [Range(1f, 12f)] public float verticesPerUnit = 4f;

        [Header("Wall")]
        [Range(0f, 0.4f)] public float crestWear = 0.12f;
        [Range(0f, 1f)] public float crestFlatness = 0.35f;
        public bool castShadows = true;
        [Range(1, 4)] public int wallRows = 2;

        [Header("Path")]
        public GhmPathSettings path = new GhmPathSettings();

        [Header("Decor")]
        public List<GhmDecorRule> rules = new List<GhmDecorRule>();

        [Header("Level range")]
        public int minLevel = 1;
        public int maxLevel = 999;

        public bool ActiveAtLevel(int level) => visible && level >= minLevel && level <= maxLevel;

        public static GhmLayer Create(GhmLayerKind kind, string name)
        {
            var l = new GhmLayer { kind = kind, name = name };
            if (kind == GhmLayerKind.Decor) l.rules.Add(new GhmDecorRule());
            return l;
        }
    }

    // A level range and the look it wears. Bands are how "levels 1-5 use this
    // ground, 6-10 use that one" is expressed: the band supplies material
    // overrides and generation numbers, the layers supply everything else.
    [Serializable]
    public class GhmLevelBand
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "Band";
        public int minLevel = 1;
        public int maxLevel = 5;

        [Header("Materials (empty = keep the layer's own)")]
        public Material groundMaterial;
        public Material waterMaterial;
        public Material wallMaterial;
        public Material bedMaterial;
        public Material pathMaterial;

        [Header("Tints")]
        public Color groundTint = Color.white;
        public Color waterTint = Color.white;
        public Color wallTint = Color.white;

        [Header("Generation")]
        public GhmAlgorithm algorithm = GhmAlgorithm.ShuffleConnected;
        [Range(0f, 0.6f)] public float waterDensity = 0.27f;
        [Tooltip("Smallest pool the cleanup pass keeps. Single stray cells are filled back in.")]
        [Range(1, 12)] public int minPoolSize = 2;
        [Range(0f, 3f)] public float decorDensityScale = 1f;
        public bool drawPaths = true;

        public bool Covers(int level) => level >= minLevel && level <= maxLevel;
    }
}
