using System.Collections.Generic;
using UnityEngine;
using Sample;

// Chases the player across the walkable (Blocks) grid using BFS
// pathfinding, moving tile-center to tile-center so it never cuts
// through lava or walls. Catching the player triggers the same fatal
// sequence as falling into lava.
[RequireComponent(typeof(Rigidbody))]
public class EnemyChaser : MonoBehaviour
{
    [SerializeField] private float speed = 4f;
    [SerializeField] private float repathInterval = 0.4f;
    [SerializeField] private float waypointTolerance = 0.15f;

    private Rigidbody _rb;
    private Transform _target;
    private readonly List<Vector3> _nodes = new List<Vector3>();
    private readonly Dictionary<Vector3, List<Vector3>> _adjacency = new Dictionary<Vector3, List<Vector3>>();
    private readonly List<Vector3> _path = new List<Vector3>();
    private float _repathTimer;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;

        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        foreach (var ctrl in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
        {
            if (ctrl.gameObject.name == "Ghost")
            {
                _target = ctrl.transform;
                break;
            }
        }

        BuildGraph();
    }

    void Update()
    {
        if (_target == null || _nodes.Count == 0) return;

        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f)
        {
            _repathTimer = repathInterval;
            Vector3 start = NearestNode(transform.position);
            Vector3 goal = NearestNode(_target.position);
            RecalculatePath(start, goal);
        }

        FaceMovementDirection();
    }

    void FixedUpdate()
    {
        if (_target == null) return;

        Vector3 current = _rb.position;

        // Once the grid path is used up (or start/goal were the same node),
        // steer straight at the player's real position instead of stopping -
        // the BFS only reasons about tile centers, so it can leave a small
        // gap that never closes on its own.
        Vector3 destination = _path.Count > 0 ? _path[0] : _target.position;
        Vector3 toDestination = new Vector3(destination.x - current.x, 0f, destination.z - current.z);

        if (_path.Count > 0 && toDestination.magnitude <= waypointTolerance)
        {
            _path.RemoveAt(0);
            return;
        }

        if (toDestination.sqrMagnitude < 0.0001f) return;

        Vector3 move = toDestination.normalized * speed * Time.fixedDeltaTime;
        if (move.magnitude > toDestination.magnitude) move = toDestination;

        _rb.MovePosition(current + move);
    }

    private void FaceMovementDirection()
    {
        if (_target == null) return;
        Vector3 destination = _path.Count > 0 ? _path[0] : _target.position;
        Vector3 toDestination = new Vector3(destination.x - transform.position.x, 0f, destination.z - transform.position.z);
        if (toDestination.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(toDestination.normalized);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ghost")) return;
        var ghost = other.GetComponentInParent<GhostScript>();
        if (ghost != null) ghost.CaughtByEnemy();
    }

    //---------------------------------------------------------------------
    // walkable graph built once from the current Blocks tiles
    //---------------------------------------------------------------------
    private void BuildGraph()
    {
        var blocksParent = GameObject.Find("Blocks");
        if (blocksParent == null) return;

        foreach (Transform b in blocksParent.transform)
        {
            Vector3 pos = Round(b.position);
            _nodes.Add(pos);
            _adjacency[pos] = new List<Vector3>();
        }

        foreach (var a in _nodes)
        {
            foreach (var b in _nodes)
            {
                if (a == b) continue;
                float d = Vector3.Distance(a, b);
                if (d > 0.9f && d < 1.1f)
                {
                    _adjacency[a].Add(b);
                }
            }
        }
    }

    private Vector3 NearestNode(Vector3 worldPos)
    {
        Vector3 best = _nodes[0];
        float bestDistSq = float.MaxValue;
        foreach (var n in _nodes)
        {
            float dx = n.x - worldPos.x;
            float dz = n.z - worldPos.z;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = n;
            }
        }
        return best;
    }

    private void RecalculatePath(Vector3 start, Vector3 goal)
    {
        _path.Clear();
        if (start == goal) return;

        var visited = new HashSet<Vector3> { start };
        var prev = new Dictionary<Vector3, Vector3>();
        var queue = new Queue<Vector3>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) break;

            foreach (var n in _adjacency[cur])
            {
                if (visited.Contains(n)) continue;
                visited.Add(n);
                prev[n] = cur;
                queue.Enqueue(n);
            }
        }

        if (!visited.Contains(goal)) return;

        var node = goal;
        while (node != start)
        {
            _path.Add(node);
            node = prev[node];
        }
        _path.Reverse();
    }

    private static Vector3 Round(Vector3 v)
    {
        return new Vector3(Mathf.Round(v.x * 100f) / 100f, v.y, Mathf.Round(v.z * 100f) / 100f);
    }
}
