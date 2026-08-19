using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Each enemy gets its own portal at a random walkable cell, visible the
// instant the level loads. A portal telegraphs for portalWarningDuration
// seconds before its enemy actually appears there; the player is held
// still for that same window so nobody moves until the countdown ends.
public class EnemySpawnManager : MonoBehaviour
{
    public static EnemySpawnManager Instance { get; private set; }

    [SerializeField] private List<GameObject> enemies = new List<GameObject>();
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private float portalWarningDuration = 3f;
    [SerializeField] private float minDistanceFromPlayer = 3f;
    [SerializeField] private float portalWorldY = 0.48f;
    // how many enemies (from the front of the list) actually spawn - lets
    // early levels ship with fewer enemies without touching the roster
    [SerializeField] private int activeEnemyCount = 1;

    private Transform _player;

    // Deadline for the current portal warning. The player is frozen until
    // then so the countdown reads as a shared "get ready" beat instead of
    // free movement while the enemies are still hidden. Time.time stalls
    // with timeScale, so a paused game keeps whatever is left of it.
    private static float _freezeUntil;
    public static bool PlayerFrozen => Time.time < _freezeUntil;

    public void SetActiveEnemyCount(int count)
    {
        activeEnemyCount = Mathf.Clamp(count, 0, enemies.Count);
    }

    void Awake()
    {
        Instance = this;
        foreach (var e in enemies)
        {
            if (e != null) e.SetActive(false);
        }
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

        TriggerSpawnSequence();
    }

    // called by GameOverManager after the player hits Continue - hides every
    // enemy again and reruns the exact same portal-warning spawn sequence
    // used at the start of the level, with freshly chosen random cells.
    public void RespawnEnemies()
    {
        StopAllCoroutines();
        foreach (var e in enemies)
        {
            if (e != null) e.SetActive(false);
        }
        TriggerSpawnSequence();
    }

    private void TriggerSpawnSequence()
    {
        _freezeUntil = Time.time + portalWarningDuration;

        if (SpawnCountdownController.Instance != null)
        {
            SpawnCountdownController.Instance.PlayCountdown(portalWarningDuration);
        }

        var usedCells = new List<Vector3>();
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null) continue;

            if (i >= activeEnemyCount)
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

        yield return new WaitForSeconds(portalWarningDuration);

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
