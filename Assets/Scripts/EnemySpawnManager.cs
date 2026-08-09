using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Each enemy gets its own portal at a random walkable cell, visible the
// instant the level loads (no global delay - the player can already move).
// A portal telegraphs for portalWarningDuration seconds before its enemy
// actually appears there, giving the player a chance to spot it and leave.
public class EnemySpawnManager : MonoBehaviour
{
    [SerializeField] private List<GameObject> enemies = new List<GameObject>();
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private float portalWarningDuration = 3f;
    [SerializeField] private float minDistanceFromPlayer = 3f;
    [SerializeField] private float portalWorldY = 0.48f;

    private Transform _player;

    void Awake()
    {
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

        var usedCells = new List<Vector3>();
        foreach (var e in enemies)
        {
            if (e == null) continue;
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
