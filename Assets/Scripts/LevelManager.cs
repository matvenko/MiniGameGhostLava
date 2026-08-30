using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sample;

// Owns level progression: shows the level-complete popup, and on "Next
// Level" procedurally reshuffles which of the existing floor cells are
// walkable vs lava (same footprint, new layout), guaranteeing every
// walkable cell stays reachable from every other one before applying the
// change, then respawns coins/player/enemies for the new level.
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    public bool IsLevelCompleteActive { get; private set; }
    public int CurrentLevel => _level;

    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private GhostScript ghost;
    [SerializeField] private EnemySpawnManager enemySpawnManager;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private Material blockMaterial;
    [SerializeField] private Material lavaMaterial;
    // Coins on the board per level, indexed by level - 1; a level past the
    // end of the table reuses its last entry, so level 6 onward keeps the
    // level-5 count.
    [SerializeField] private int[] coinsByLevel = { 12, 14, 16, 18, 20 };
    // Board footprint per level, indexed by level - 1; a level past the end of
    // the table reuses its last entry. Two cells wider and two taller each
    // level up to 4, holding there through 7, reaching its final 30x24 at 8.
    [SerializeField] private Vector2Int[] boardSizeByLevel =
    {
        new Vector2Int(22, 16),
        new Vector2Int(24, 18),
        new Vector2Int(26, 20),
        new Vector2Int(28, 22),
        new Vector2Int(28, 22),
        new Vector2Int(28, 22),
        new Vector2Int(28, 22),
        new Vector2Int(30, 24)
    };
    [SerializeField] private float lavaDensity = 0.27f;
    [SerializeField] private float coinHeightOffset = 0.83f;
    [SerializeField] private GameObject friendlyGhost;
    [SerializeField] private int friendlyGhostFromLevel = 3;
    [SerializeField] private float friendlyGhostMinDistanceFromPlayer = 3f;

    private static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    private int _level = 1;
    private float _blockTileY;
    private float _lavaTileY;
    private Vector2 _boardCentre;
    private float[] _wallRowY;
    private Transform _tilePool;
    private Transform _wallPool;

    void Awake()
    {
        Instance = this;
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
        UpdateLevelText();

        // The board is built here rather than in Start because the rest of the
        // scene reads it from Start: the enemies pick their spawn cells off the
        // Blocks parent, and the path grid is a static singleton that outlives
        // scene loads, so a restart would otherwise keep pathing on the
        // previous run's layout. Enemies can't rebuild it themselves either -
        // they spawn disabled and only reach Start() after the portal delay.
        CaptureBoardMetrics();
        ApplyLevelLayout();
        PlacePlayerOnBoard();
    }

    // Level 1's coins come from the same table as every other level rather
    // than from whatever happens to be hand-placed in the scene - otherwise
    // changing the table would silently skip the level the player sees first.
    // Awake laid the board out underneath them a moment ago, so the tiles they
    // land on are this level's, not the authored ones.
    void Start()
    {
        int spawned = SpawnCoins(CoinsForLevel(_level));
        if (RewardSystem.Instance != null) RewardSystem.Instance.ResetForNewLevel(spawned);
    }

    private int CoinsForLevel(int level)
    {
        if (coinsByLevel == null || coinsByLevel.Length == 0) return 0;
        return coinsByLevel[Mathf.Clamp(level - 1, 0, coinsByLevel.Length - 1)];
    }

    private void UpdateLevelText()
    {
        // The word LEVEL is lettered into the badge behind this, so all that is
        // left to say is the number.
        if (levelText != null) levelText.text = _level.ToString();
    }

    public void OnLevelComplete()
    {
        IsLevelCompleteActive = true;
        if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void OnNextLevelClicked()
    {
        IsLevelCompleteActive = false;
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        Time.timeScale = 1f;

        _level++;
        UpdateLevelText();

        if (TrapManager.Instance != null) TrapManager.Instance.ClearPlacedTraps();

        ApplyLevelLayout();

        int spawned = SpawnCoins(CoinsForLevel(_level));
        if (RewardSystem.Instance != null) RewardSystem.Instance.ResetForNewLevel(spawned);

        if (enemySpawnManager != null) enemySpawnManager.SetLevel(_level);

        RespawnPlayerAndEnemies();
    }

    private Vector2Int SizeForLevel(int level)
    {
        if (boardSizeByLevel == null || boardSizeByLevel.Length == 0) return new Vector2Int(30, 20);
        return boardSizeByLevel[Mathf.Clamp(level - 1, 0, boardSizeByLevel.Length - 1)];
    }

    // Everything that has to happen when the board changes shape: floor and
    // border resized to this level's footprint, lava laid out fresh on it (and
    // still guaranteed connected), then every system that caches the board put
    // back in sync. The three merged surfaces are only nudged if they have
    // already built themselves - on the very first frame their own OnEnable
    // may not have run yet, and it will pick up the finished board on its own.
    private void ApplyLevelLayout()
    {
        Vector2Int size = SizeForLevel(_level);
        ResizeBoard(size);
        ResizeWalls(size);
        RegenerateLayout();

        EnemyPathGrid.Instance.Rebuild();
        if (LiquidSurface.Instance != null) LiquidSurface.Instance.Refresh();
        if (GroundSurface.Instance != null) GroundSurface.Instance.Refresh();
        if (WallSurface.Instance != null) WallSurface.Instance.Refresh();

        if (cameraFollow != null)
        {
            // Out to the far face of the border, which is where the authored
            // limits sat: the wall is meant to be visible at the edge of the
            // view, not cut off at the last floor tile.
            float halfX = size.x * 0.5f + 1f;
            float halfZ = size.y * 0.5f + 1f;
            cameraFollow.ConfigureForMap(_boardCentre.x - halfX, _boardCentre.x + halfX,
                                         _boardCentre.y - halfZ, _boardCentre.y + halfZ);
        }
    }

    // Measured once from the authored board, before anything resizes it, so
    // every later footprint is centred on the same spot and reuses the same
    // tile heights - the board grows outward symmetrically instead of drifting
    // off toward one corner.
    private void CaptureBoardMetrics()
    {
        var blocksParent = GameObject.Find("Blocks").transform;
        var lavaParent = GameObject.Find("Lava").transform;

        var allTiles = new List<Transform>();
        foreach (Transform t in blocksParent) allTiles.Add(t);
        foreach (Transform t in lavaParent) allTiles.Add(t);
        if (allTiles.Count > 0)
        {
            _boardCentre = new Vector2(
                (allTiles.Min(t => t.position.x) + allTiles.Max(t => t.position.x)) * 0.5f,
                (allTiles.Min(t => t.position.z) + allTiles.Max(t => t.position.z)) * 0.5f);
        }

        if (blocksParent.childCount > 0) _blockTileY = blocksParent.GetChild(0).position.y;
        if (lavaParent.childCount > 0) _lavaTileY = lavaParent.GetChild(0).position.y;

        // The border is two rows of cubes at fixed heights; which cells they
        // sit on changes with the board, the heights never do.
        var wallsParent = GameObject.Find("Walls");
        if (wallsParent != null)
        {
            var rows = new List<float>();
            foreach (Transform t in wallsParent.transform)
            {
                float y = Mathf.Round(t.position.y * 1000f) / 1000f;
                if (!rows.Contains(y)) rows.Add(y);
            }
            rows.Sort();
            _wallRowY = rows.ToArray();
        }
    }

    // Lays the floor out as a size.x by size.y rectangle centred on the board.
    // Tiles are reused rather than rebuilt: whatever is already in the scene
    // gets moved onto the new grid, a shortfall is cloned off an existing tile
    // so the copies carry the same mesh, collider and material, and the surplus
    // is parked. Which of them end up lava is RegenerateLayout's business, and
    // it runs straight after this.
    //
    // Cells are one world unit, the same assumption the path grid and the
    // layout generator already make when they round a position to a cell.
    private void ResizeBoard(Vector2Int size)
    {
        var blocksParent = GameObject.Find("Blocks").transform;
        var lavaParent = GameObject.Find("Lava").transform;

        var tiles = new List<Transform>();
        foreach (Transform t in blocksParent) tiles.Add(t);
        foreach (Transform t in lavaParent) tiles.Add(t);
        if (_tilePool != null) foreach (Transform t in _tilePool) tiles.Add(t);
        if (tiles.Count == 0) return;

        Transform template = blocksParent.childCount > 0 ? blocksParent.GetChild(0) : tiles[0];
        float halfX = (size.x - 1) * 0.5f;
        float halfZ = (size.y - 1) * 0.5f;

        int i = 0;
        for (int gx = 0; gx < size.x; gx++)
        {
            for (int gz = 0; gz < size.y; gz++)
            {
                if (i >= tiles.Count) tiles.Add(Instantiate(template.gameObject, blocksParent).transform);

                var tile = tiles[i++];
                if (tile.parent == _tilePool) tile.SetParent(blocksParent, true);
                tile.position = new Vector3(_boardCentre.x - halfX + gx, tile.position.y, _boardCentre.y - halfZ + gz);
            }
        }

        for (; i < tiles.Count; i++) Park(tiles[i], ref _tilePool, "TilePool");
    }

    // The border is the one-cell ring just outside the floor, two rows tall.
    // It is rebuilt from the same rectangle the floor came from rather than
    // measured off the old wall, so it can never drift out of step with it.
    private void ResizeWalls(Vector2Int size)
    {
        var wallsObject = GameObject.Find("Walls");
        if (wallsObject == null || _wallRowY == null || _wallRowY.Length == 0) return;
        var wallsParent = wallsObject.transform;

        var cubes = new List<Transform>();
        foreach (Transform t in wallsParent) cubes.Add(t);
        if (_wallPool != null) foreach (Transform t in _wallPool) cubes.Add(t);
        if (cubes.Count == 0) return;

        Transform template = cubes[0];
        float halfX = (size.x + 1) * 0.5f;
        float halfZ = (size.y + 1) * 0.5f;

        int i = 0;
        for (int gx = 0; gx < size.x + 2; gx++)
        {
            for (int gz = 0; gz < size.y + 2; gz++)
            {
                // the ring is the border of that rectangle, nothing inside it
                if (gx > 0 && gx <= size.x && gz > 0 && gz <= size.y) continue;

                float x = _boardCentre.x - halfX + gx;
                float z = _boardCentre.y - halfZ + gz;
                foreach (float y in _wallRowY)
                {
                    if (i >= cubes.Count) cubes.Add(Instantiate(template.gameObject, wallsParent).transform);

                    var cube = cubes[i++];
                    if (cube.parent != wallsParent) cube.SetParent(wallsParent, true);
                    cube.position = new Vector3(x, y, z);
                }
            }
        }

        for (; i < cubes.Count; i++) Park(cubes[i], ref _wallPool, "WallPool");
    }

    // Surplus tiles are parked, not destroyed - the board grows again on later
    // levels. The holder itself is inactive, which is what takes its children
    // out of play, and it is somewhere none of the systems that walk the
    // Blocks/Lava/Walls children will ever look.
    private static void Park(Transform t, ref Transform pool, string poolName)
    {
        if (pool == null)
        {
            var go = new GameObject(poolName);
            go.hideFlags = HideFlags.DontSave;
            go.SetActive(false);
            pool = go.transform;
        }
        t.SetParent(pool, true);
    }

    // Level 1's authored start sat on the old hand-built board. The board under
    // it is laid out fresh now, so the character is moved onto a walkable tile
    // before its own Start() captures that position as its spawn point.
    private void PlacePlayerOnBoard()
    {
        if (ghost == null) return;
        var blocksParent = GameObject.Find("Blocks").transform;
        if (blocksParent.childCount == 0) return;

        var spot = blocksParent.GetChild(Random.Range(0, blocksParent.childCount));
        var ctrl = ghost.GetComponent<CharacterController>();

        // A CharacterController overrides transform writes made while it is
        // enabled, the same reason RespawnAt cycles it.
        if (ctrl != null) ctrl.enabled = false;
        ghost.transform.position = new Vector3(spot.position.x, ghost.transform.position.y, spot.position.z);
        if (ctrl != null) ctrl.enabled = true;
    }

    private void RegenerateLayout()
    {
        var blocksParent = GameObject.Find("Blocks").transform;
        var lavaParent = GameObject.Find("Lava").transform;

        // Block and Lava tiles sit at slightly different baseline heights by
        // design (lava is visually recessed), so a tile changing type gets moved
        // to match its new type's height. Both heights come from
        // CaptureBoardMetrics rather than from the current children: a resize
        // can leave one of the two parents empty, and there would be nothing
        // left to read the height off.
        var allTiles = new List<Transform>();
        foreach (Transform t in blocksParent) allTiles.Add(t);
        foreach (Transform t in lavaParent) allTiles.Add(t);

        float minX = allTiles.Min(t => t.position.x);
        float minZ = allTiles.Min(t => t.position.z);
        Vector2Int CellOf(Transform t) => new Vector2Int(Mathf.RoundToInt(t.position.x - minX), Mathf.RoundToInt(t.position.z - minZ));

        var cellToTile = new Dictionary<Vector2Int, Transform>();
        foreach (var t in allTiles) cellToTile[CellOf(t)] = t;

        var isLava = new Dictionary<Vector2Int, bool>();
        foreach (var c in cellToTile.Keys) isLava[c] = false;

        var shuffled = cellToTile.Keys.ToList();
        Shuffle(shuffled);

        int targetLavaCount = Mathf.RoundToInt(allTiles.Count * lavaDensity);
        int converted = 0;
        foreach (var c in shuffled)
        {
            if (converted >= targetLavaCount) break;
            isLava[c] = true;
            if (IsFullyConnected(cellToTile.Keys, isLava))
            {
                converted++;
            }
            else
            {
                isLava[c] = false;
            }
        }

        foreach (var kvp in cellToTile)
        {
            ApplyTileType(kvp.Value, isLava[kvp.Key], blocksParent, lavaParent);
        }
    }

    // BFS over every non-lava cell from an arbitrary walkable start; the
    // layout is only valid if that reaches every other walkable cell too -
    // this is what keeps the level always completable.
    private bool IsFullyConnected(IEnumerable<Vector2Int> allCells, Dictionary<Vector2Int, bool> isLava)
    {
        var walkable = allCells.Where(c => !isLava[c]).ToList();
        if (walkable.Count == 0) return false;

        var visited = new HashSet<Vector2Int> { walkable[0] };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(walkable[0]);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var d in Dirs)
            {
                var n = cur + d;
                if (!isLava.ContainsKey(n) || isLava[n] || visited.Contains(n)) continue;
                visited.Add(n);
                queue.Enqueue(n);
            }
        }

        return visited.Count == walkable.Count;
    }

    private void ApplyTileType(Transform tile, bool wantLava, Transform blocksParent, Transform lavaParent)
    {
        var mr = tile.GetComponent<MeshRenderer>();
        var bc = tile.GetComponent<BoxCollider>();
        var hazard = tile.GetComponent<LavaHazard>();

        Vector3 pos = tile.position;

        if (wantLava)
        {
            if (blockMaterial != null || lavaMaterial != null) mr.sharedMaterial = lavaMaterial;
            bc.center = new Vector3(0f, 0.05f, 0f);
            bc.size = new Vector3(1.18f, 1.3f, 1.18f);
            bc.isTrigger = true;
            if (hazard == null) tile.gameObject.AddComponent<LavaHazard>();
            pos.y = _lavaTileY;
            tile.position = pos;
            tile.SetParent(lavaParent, true);
        }
        else
        {
            if (blockMaterial != null) mr.sharedMaterial = blockMaterial;
            bc.center = Vector3.zero;
            bc.size = Vector3.one;
            bc.isTrigger = false;
            if (hazard != null) Destroy(hazard);
            pos.y = _blockTileY;
            tile.position = pos;
            tile.SetParent(blocksParent, true);
            // A tile arriving from the lava side had its renderer suppressed by
            // LiquidSurface, and LiquidSurface only ever revisits its own
            // children - so nothing would switch this one back on. The result is
            // walkable floor that reads as water: you can stand on it, but the
            // liquid plane is all you see. Rendering belongs with the tile type,
            // so it is restored here. If GroundSurface is drawing the merged
            // floor instead, its Refresh() runs straight after this and hides
            // the cubes again.
            mr.forceRenderingOff = false;
        }
    }

    private int SpawnCoins(int count)
    {
        var coinsParent = GameObject.Find("Coins").transform;
        for (int i = coinsParent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(coinsParent.GetChild(i).gameObject);
        }

        if (coinPrefab == null) return 0;

        var blocksParent = GameObject.Find("Blocks").transform;
        var candidates = new List<Transform>();
        foreach (Transform b in blocksParent) candidates.Add(b);
        Shuffle(candidates);

        int n = Mathf.Min(count, candidates.Count);
        for (int i = 0; i < n; i++)
        {
            Vector3 pos = candidates[i].position + new Vector3(0f, coinHeightOffset, 0f);
            Instantiate(coinPrefab, pos, Quaternion.identity, coinsParent);
        }
        return n;
    }

    private void RespawnPlayerAndEnemies()
    {
        var blocksParent = GameObject.Find("Blocks").transform;
        var candidates = new List<Transform>();
        foreach (Transform b in blocksParent) candidates.Add(b);
        var chosen = candidates[Random.Range(0, candidates.Count)];

        if (ghost != null) ghost.RespawnAt(chosen.position);
        if (cameraFollow != null) cameraFollow.SetControlEnabled(true);
        if (enemySpawnManager != null) enemySpawnManager.RespawnEnemies();

        SpawnOrHideFriendlyGhost(candidates, chosen.position);
    }

    // Starting at friendlyGhostFromLevel, places a fresh (un-caught) flee-AI
    // ghost each level; earlier levels just keep it hidden. It only ever
    // respawns on a level transition, so catching it makes it gone for the
    // rest of the current level as requested.
    private void SpawnOrHideFriendlyGhost(List<Transform> candidates, Vector3 playerPos)
    {
        if (friendlyGhost == null) return;

        if (_level < friendlyGhostFromLevel)
        {
            friendlyGhost.SetActive(false);
            return;
        }

        var farCandidates = candidates.Where(c => Vector3.Distance(c.position, playerPos) >= friendlyGhostMinDistanceFromPlayer).ToList();
        var pool = farCandidates.Count > 0 ? farCandidates : candidates;
        var spot = pool[Random.Range(0, pool.Count)];

        Vector3 pos = spot.position;
        pos.y = friendlyGhost.transform.position.y;
        friendlyGhost.transform.position = pos;

        var flee = friendlyGhost.GetComponent<FriendlyGhostFlee>();
        if (flee != null) flee.ResetState();

        friendlyGhost.SetActive(true);
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
