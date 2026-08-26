using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Each enemy gets its own portal at a random walkable cell, visible the
// instant the level loads. The portals then burn for as long as the camera
// takes to open the level - the board held in frame while the countdown runs,
// then the move in to the player - so the enemies are standing there the
// moment it settles. The player is held still for that same window, so nobody
// moves until the camera has arrived.
//
// Several kinds of enemy share that sequence - the slower optimal-pathing
// ghost, the faster greedy axe ghost, the ghoul. The scene carries one
// authored instance of each and this manager clones it into a pool, so a
// level can field several of the same kind without the scene having to hold
// a hand-placed object for every one of them.
public class EnemySpawnManager : MonoBehaviour
{
    public static EnemySpawnManager Instance { get; private set; }

    // One kind of enemy: the authored scene object every copy is cloned from,
    // and how many of it each level fields. Keeping the two together is what
    // lets a new kind be a single entry here rather than another parallel set
    // of fields threaded through every method below.
    [System.Serializable]
    private class EnemyKind
    {
        [Tooltip("Scene object this kind is cloned from. Its EnemyChaser tuning is what every copy inherits.")]
        public GameObject template;

        [Tooltip("How many of this kind a level fields, indexed by level - 1. A level past the end of the list reuses the last entry.")]
        public int[] countByLevel = { 1 };

        [System.NonSerialized] public List<GameObject> Pool;
        [System.NonSerialized] public int Active;
    }

    [SerializeField] private EnemyKind[] enemyKinds = new EnemyKind[0];
    [SerializeField] private GameObject portalPrefab;
    [Tooltip("Only used when there is no CameraFollow to open the level; normally the camera intro sets the length of the warning.")]
    [SerializeField] private float fallbackWarningDuration = 3f;
    [SerializeField] private float minDistanceFromPlayer = 3f;
    [SerializeField] private float portalWorldY = 0.48f;

    private Transform _player;
    private CameraFollow _camera;

    // How long the opening beat lasts. The camera owns the shape of it, and
    // everything else here is timed against that shot rather than against a
    // duration of its own, so the two can never drift apart.
    private float WarningDuration => _camera != null ? _camera.IntroDuration : fallbackWarningDuration;

    // Deadline for the current portal warning. The player is frozen until
    // then so the countdown reads as a shared "get ready" beat instead of
    // free movement while the enemies are still hidden. Time.time stalls
    // with timeScale, so a paused game keeps whatever is left of it.
    private static float _freezeUntil;
    public static bool PlayerFrozen => Time.time < _freezeUntil;

    // Sets the roster for the next spawn sequence. Kept separate from the
    // spawn itself so the counts are already in place by the time
    // RespawnEnemies runs on a level change.
    public void SetLevel(int level)
    {
        foreach (var kind in enemyKinds)
        {
            kind.Active = CountForLevel(kind.countByLevel, level, kind.Pool.Count);
        }
    }

    private static int CountForLevel(int[] table, int level, int poolSize)
    {
        if (table == null || table.Length == 0) return 0;
        int index = Mathf.Clamp(level - 1, 0, table.Length - 1);
        return Mathf.Clamp(table[index], 0, poolSize);
    }

    void Awake()
    {
        Instance = this;
        foreach (var kind in enemyKinds)
        {
            kind.Pool = new List<GameObject>();
            BuildPool(kind);
        }
    }

    // The authored scene object is both the first pool entry and the template
    // for the rest, so every clone inherits its EnemyChaser tuning (strategy,
    // speed) instead of that having to be duplicated by hand. Cloning happens
    // while the template is already inactive, so the copies come out inactive
    // too and none of them runs Start() before its portal has finished
    // telegraphing.
    private static void BuildPool(EnemyKind kind)
    {
        if (kind.template == null) return;

        kind.template.SetActive(false);
        kind.Pool.Add(kind.template);

        int size = MaxCount(kind.countByLevel);
        for (int i = 1; i < size; i++)
        {
            var clone = Instantiate(kind.template, kind.template.transform.parent);
            clone.name = kind.template.name + " " + (i + 1);
            kind.Pool.Add(clone);
        }
    }

    private static int MaxCount(int[] table)
    {
        int max = 0;
        if (table != null)
        {
            foreach (int count in table) max = Mathf.Max(max, count);
        }
        return max;
    }

    void Start()
    {
        foreach (var ctrl in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
        {
            if (ctrl.gameObject.name == "Ghost")
            {
                _player = ctrl.transform;
                break;
            }
        }

        _camera = FindFirstObjectByType<CameraFollow>();

        // A fresh scene starts at level 1, but read it off the LevelManager
        // anyway so this stays correct if a run ever begins further in.
        SetLevel(LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : 1);
        TriggerSpawnSequence();
    }

    // called by GameOverManager after the player hits Continue - hides every
    // enemy again and reruns the exact same portal-warning spawn sequence
    // used at the start of the level, with freshly chosen random cells.
    public void RespawnEnemies()
    {
        StopAllCoroutines();
        foreach (var kind in enemyKinds)
        {
            foreach (var e in kind.Pool)
            {
                if (e != null) e.SetActive(false);
            }
        }
        TriggerSpawnSequence();
    }

    private void TriggerSpawnSequence()
    {
        _freezeUntil = Time.time + WarningDuration;

        if (_camera != null) _camera.PlayIntro();

        if (SpawnCountdownController.Instance != null)
        {
            // Counts down the board shot rather than the whole warning:
            // reaching one is what sends the camera in towards the player.
            SpawnCountdownController.Instance.PlayCountdown(
                _camera != null ? _camera.OverviewDuration : WarningDuration);
        }

        // One shared list of taken cells across every kind, so two enemies
        // can't be handed the same corner of the board.
        var usedCells = new List<Vector3>();
        foreach (var kind in enemyKinds) SpawnKind(kind, usedCells);
    }

    private void SpawnKind(EnemyKind kind, List<Vector3> usedCells)
    {
        for (int i = 0; i < kind.Pool.Count; i++)
        {
            var e = kind.Pool[i];
            if (e == null) continue;

            if (i >= kind.Active)
            {
                e.SetActive(false);
                continue;
            }

            Vector3 cell = PickRandomCell(usedCells);
            usedCells.Add(cell);
            StartCoroutine(SpawnOne(e, cell));
        }
    }

    private IEnumerator SpawnOne(GameObject enemy, Vector3 cell)
    {
        Vector3 portalPos = new Vector3(cell.x, portalWorldY, cell.z);
        GameObject portal = null;
        if (portalPrefab != null)
        {
            portal = Instantiate(portalPrefab, portalPos, Quaternion.Euler(90f, 0f, 0f));
        }

        yield return new WaitForSeconds(WarningDuration);

        enemy.transform.position = new Vector3(cell.x, enemy.transform.position.y, cell.z);
        enemy.SetActive(true);

        if (portal != null) Destroy(portal);
    }

    private Vector3 PickRandomCell(List<Vector3> avoid)
    {
        var blocksParent = GameObject.Find("Blocks");
        var candidates = new List<Vector3>();
        foreach (Transform b in blocksParent.transform)
        {
            if (_player != null && Vector3.Distance(b.position, _player.position) < minDistanceFromPlayer) continue;

            bool tooClose = false;
            foreach (var used in avoid)
            {
                if (Vector3.Distance(b.position, used) < 1.5f) { tooClose = true; break; }
            }
            if (tooClose) continue;

            candidates.Add(b.position);
        }

        if (candidates.Count == 0)
        {
            foreach (Transform b in blocksParent.transform) candidates.Add(b.position);
        }

        return candidates[Random.Range(0, candidates.Count)];
    }
}
