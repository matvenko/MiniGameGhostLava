using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
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

    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private GhostScript ghost;
    [SerializeField] private EnemySpawnManager enemySpawnManager;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private Material blockMaterial;
    [SerializeField] private Material lavaMaterial;
    [SerializeField] private int coinsFromLevel2 = 7;
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

    void Awake()
    {
        Instance = this;
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
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

        RegenerateLayout();
        EnemyPathGrid.Instance.Rebuild();

        int coinCount = _level >= 2 ? coinsFromLevel2 : 5;
        int spawned = SpawnCoins(coinCount);
        if (RewardSystem.Instance != null) RewardSystem.Instance.ResetForNewLevel(spawned);

        if (enemySpawnManager != null) enemySpawnManager.SetActiveEnemyCount(_level >= 2 ? 2 : 1);

        RespawnPlayerAndEnemies();
    }

    private void RegenerateLayout()
    {
        var blocksParent = GameObject.Find("Blocks").transform;
        var lavaParent = GameObject.Find("Lava").transform;

        // Block and Lava tiles sit at slightly different baseline heights by
        // design (lava is visually recessed) - capture both now, before any
        // conversion, so a tile changing type gets moved to match its new
        // type's height instead of keeping whatever height it started at.
        float blockY = blocksParent.childCount > 0 ? blocksParent.GetChild(0).position.y : 0f;
        float lavaY = lavaParent.childCount > 0 ? lavaParent.GetChild(0).position.y : 0f;
        _blockTileY = blockY;
        _lavaTileY = lavaY;

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
