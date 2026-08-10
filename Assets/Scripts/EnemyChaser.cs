using System.Collections.Generic;
using UnityEngine;
using Sample;

// Chases the player across the walkable (Blocks) grid using BFS
// pathfinding, moving tile-center to tile-center so it never cuts
// through lava or walls. Catching the player triggers the same fatal
// sequence as falling into lava.
//
// The grid itself (nodes/adjacency/nearest-node lookup) lives in the
// shared EnemyPathGrid rather than being rebuilt and re-scanned per
// enemy - see that class for why.
[RequireComponent(typeof(Rigidbody))]
public class EnemyChaser : MonoBehaviour
{
    // Optimal = full BFS shortest path (smart, so it's slower to compensate).
    // Greedy = only looks at its immediate neighbors each step with no
    // lookahead, so it can wander into dead ends and take longer routes
    // (dumb, so it's faster to compensate). Giving enemies different
    // strategies keeps them from marching the exact same route in lockstep.
    public enum PathingStrategy { Optimal, Greedy }

    [SerializeField] private PathingStrategy strategy = PathingStrategy.Optimal;
    [SerializeField] private float speed = 4f;
    [SerializeField] private float repathInterval = 0.4f;
    [SerializeField] private float waypointTolerance = 0.15f;
    [SerializeField] private float separationRadius = 0.8f;
    [SerializeField] private float separationStrength = 2f;

    private Rigidbody _rb;
    private Transform _target;
    private readonly List<Vector3> _path = new List<Vector3>();
    private float _repathTimer;
    private Vector3 _lastGoal;
    private bool _hasGoal;

    private static readonly List<EnemyChaser> AllChasers = new List<EnemyChaser>();

    void OnEnable()
    {
        AllChasers.Add(this);
    }

    void OnDisable()
    {
        AllChasers.Remove(this);
    }

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

        EnemyPathGrid.Instance.EnsureBuilt();

        // stagger repath timing per-instance so multiple enemies don't
        // recompute (and react) in perfect lockstep
        _repathTimer = Random.Range(0f, repathInterval);
    }

    void Update()
    {
        if (_target == null || EnemyPathGrid.Instance.AllNodes.Count == 0) return;

        _repathTimer -= Time.deltaTime;
        Vector3 start = EnemyPathGrid.Instance.NearestNode(transform.position);
        Vector3 goal = EnemyPathGrid.Instance.NearestNode(_target.position);

        // Repath on the usual timer, but also the instant the queued path
        // runs dry while we're not yet on the player's tile - otherwise
        // FixedUpdate has nothing safe to walk toward and (for Greedy
        // especially, which only ever queues one step at a time) that gap
        // used to fall back to unconstrained steering that could cut
        // straight across a lava tile.
        bool pathExhausted = _path.Count == 0 && start != goal;
        if (_repathTimer <= 0f || pathExhausted || !_hasGoal || goal != _lastGoal)
        {
            _repathTimer = repathInterval;
            _lastGoal = goal;
            _hasGoal = true;
            RecalculatePath(start, goal);
        }

        FaceMovementDirection();
    }

    void FixedUpdate()
    {
        if (_target == null) return;

        Vector3 current = _rb.position;

        // With no queued step, only close the gap directly when we're
        // already on the player's own tile (short-range, inherently safe).
        // If a real path is needed but hasn't been computed yet, stay put
        // rather than free-steer toward the player and risk walking onto
        // lava - Update() will supply a fresh grid step immediately.
        if (_path.Count == 0)
        {
            Vector3 start = EnemyPathGrid.Instance.NearestNode(current);
            Vector3 goal = EnemyPathGrid.Instance.NearestNode(_target.position);
            if (start != goal) return;
        }

        // Once the grid path is used up (or start/goal were the same node),
        // close the last bit of distance to the player directly - but only
        // along one axis at a time, so it can never cut a lava corner
        // diagonally the way the player deliberately can.
        Vector3 destination = _path.Count > 0 ? _path[0] : _target.position;
        Vector3 toDestination = new Vector3(destination.x - current.x, 0f, destination.z - current.z);

        if (_path.Count > 0 && toDestination.magnitude <= waypointTolerance)
        {
            _path.RemoveAt(0);
            return;
        }

        if (toDestination.sqrMagnitude < 0.0001f) return;

        Vector3 chaseDir;
        if (_path.Count > 0)
        {
            chaseDir = toDestination.normalized;
        }
        else if (Mathf.Abs(toDestination.x) > Mathf.Abs(toDestination.z))
        {
            chaseDir = new Vector3(Mathf.Sign(toDestination.x), 0f, 0f);
        }
        else
        {
            chaseDir = new Vector3(0f, 0f, Mathf.Sign(toDestination.z));
        }

        Vector3 separation = GetSeparation(current);
        Vector3 move = (chaseDir * speed + separation * separationStrength) * Time.fixedDeltaTime;
        if (move.magnitude > toDestination.magnitude) move = move.normalized * toDestination.magnitude;

        _rb.MovePosition(current + move);
    }

    // gently pushes this enemy away from any other chaser that's crowding
    // it, so multiple enemies don't stack on the exact same tile
    private Vector3 GetSeparation(Vector3 current)
    {
        Vector3 push = Vector3.zero;
        foreach (var other in AllChasers)
        {
            if (other == this || other == null) continue;
            Vector3 diff = current - other._rb.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist > 0.001f && dist < separationRadius)
            {
                push += diff.normalized * (separationRadius - dist);
            }
        }
        return push;
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

    private void RecalculatePath(Vector3 start, Vector3 goal)
    {
        _path.Clear();
        if (start == goal) return;

        if (strategy == PathingStrategy.Optimal)
        {
            RecalculateBFS(start, goal);
        }
        else
        {
            RecalculateGreedy(start, goal);
        }
    }

    // true shortest path across the whole grid
    private void RecalculateBFS(Vector3 start, Vector3 goal)
    {
        var visited = new HashSet<Vector3> { start };
        var prev = new Dictionary<Vector3, Vector3>();
        var queue = new Queue<Vector3>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == goal) break;

            foreach (var n in EnemyPathGrid.Instance.GetNeighbors(cur))
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

    // only picks whichever neighbor is closest (straight-line) to the goal,
    // one step at a time, with no lookahead - so it has no idea a direction
    // leads into a dead end until it's already walked into it
    private void RecalculateGreedy(Vector3 start, Vector3 goal)
    {
        Vector3 best = start;
        float bestDistSq = float.MaxValue;
        foreach (var n in EnemyPathGrid.Instance.GetNeighbors(start))
        {
            float d = (n - goal).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = n;
            }
        }
        if (best != start) _path.Add(best);
    }
}
