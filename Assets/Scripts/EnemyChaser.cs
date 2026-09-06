using System.Collections.Generic;
using UnityEngine;
using Sample;

// Moves an enemy across the walkable (Blocks) grid using BFS pathfinding,
// tile-center to tile-center so it never cuts through lava or walls. Most
// enemies path to the player; a wanderer paths to somewhere of its own
// choosing (see PathingStrategy). Either way, touching the player triggers the
// same fatal sequence as falling into lava.
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
    //
    // Wander doesn't chase at all: it walks the board on its own errands,
    // picking somewhere to be and going there. It is still fatal to touch, so
    // it reads as a haunting rather than a hunt - and because it never aims at
    // the player, it is the one enemy that can be walked around on purpose.
    public enum PathingStrategy { Optimal, Greedy, Wander }

    [SerializeField] private PathingStrategy strategy = PathingStrategy.Optimal;
    [SerializeField] private float speed = 4f;

    [Header("Wander")]
    [Tooltip("How long a wanderer keeps walking toward one spot before it changes its mind.")]
    [SerializeField] private float wanderInterest = 6f;
    [Tooltip("Tiles it will not bother crossing the board for, and the trip length it likes.")]
    [SerializeField] private float wanderMinDistance = 3f;
    [SerializeField] private float wanderPreferredDistance = 7f;
    [Tooltip("Places it considers before setting off. More means a fussier, less random-looking route.")]
    [SerializeField] private int wanderSamples = 12;
    [SerializeField] private float repathInterval = 0.4f;
    [SerializeField] private float waypointTolerance = 0.15f;
    [SerializeField] private float separationRadius = 0.8f;
    [SerializeField] private float separationStrength = 2f;

    // The authored speed is the hard-mode speed; normal plays it back slower
    // (see DifficultySettings). Read per step rather than cached, so a mode
    // chosen after this component woke up still applies. Lane-giving compares
    // authored speeds, which rank the hunters the same either way.
    private float ChaseSpeed => DifficultySettings.EnemySpeed(speed);
    // How close a faster enemy has to get before this one gives way, and how
    // long it stays out of the lane once it has - long enough to be passed.
    [SerializeField] private float yieldRadius = 1.6f;
    [SerializeField] private float yieldDuration = 0.8f;

    private Rigidbody _rb;
    private Transform _target;
    private readonly List<Vector3> _path = new List<Vector3>();
    private float _repathTimer;
    private Vector3 _lastGoal;
    private bool _hasGoal;
    private float _stunTimer;
    private float _yieldTimer;
    private Vector3 _wanderGoal;
    private bool _hasWanderGoal;
    private float _wanderTimer;

    private bool Wanders => strategy == PathingStrategy.Wander;
    private Animator _animator;
    private float _animatorSpeed = 1f;
    private FreezeVisual _freezeVisual;

    public bool IsStunned => _stunTimer > 0f;

    // Called by Trap when this enemy walks onto one, and by the freeze ability
    // for every enemy at once. Being stopped has to be visible from across the
    // board, so it is shown twice over: the animator is held on the frame it
    // was on - a ghost caught mid-drift stays mid-drift - and FreezeVisual
    // closes a shell of ice over it. Neither touches the enemy's own
    // materials, which come from three different packs and share no colour
    // property to tint.
    public void Stun(float duration)
    {
        _stunTimer = Mathf.Max(_stunTimer, duration);

        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        if (_animator != null && _animator.speed > 0f)
        {
            _animatorSpeed = _animator.speed;
            _animator.speed = 0f;
        }

        if (_freezeVisual == null) _freezeVisual = gameObject.AddComponent<FreezeVisual>();
        _freezeVisual.Show();
    }

    // Coming to: the pose starts moving again and the ice breaks off.
    private void EndStun()
    {
        if (_animator != null) _animator.speed = _animatorSpeed;
        if (_freezeVisual != null) _freezeVisual.Shatter();
    }

    private static readonly List<EnemyChaser> AllChasers = new List<EnemyChaser>();

    // Every enemy currently on the board, for anything that needs to know where
    // they are rather than to touch them - the teleport ability measures how
    // clear a tile is against this before dropping the player onto it.
    public static IReadOnlyList<EnemyChaser> Active => AllChasers;

    // Every enemy currently on the board, stunned at once - what the freeze
    // ability spends a charge on. Returns how many were caught, so a press on
    // an empty board (the portal warm-up, where they are all still inactive)
    // can be told from one that did something and refunded by not spending.
    public static int StunAll(float duration)
    {
        foreach (var chaser in AllChasers) chaser.Stun(duration);
        return AllChasers.Count;
    }

    void OnEnable()
    {
        AllChasers.Add(this);

        // Respawns (new level, player death) re-enable the same object, and
        // the queued path would otherwise survive a level regeneration -
        // sending the enemy walking a route computed for the old layout,
        // straight over tiles that are now lava. Start every activation with
        // no path and no goal so the first Update repaths against the
        // current grid.
        _path.Clear();
        _hasGoal = false;
        _hasWanderGoal = false;
        _stunTimer = 0f;
        _repathTimer = 0f;
        _yieldTimer = 0f;

        // A respawn can happen with the ice still on. Nothing shattered - the
        // enemy is being put back on the board - so it just goes.
        if (_animator != null) _animator.speed = _animatorSpeed;
        if (_freezeVisual != null) _freezeVisual.HideImmediate();
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
        if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive) return;

        if (_stunTimer > 0f)
        {
            _stunTimer -= Time.deltaTime;
            // drop the queued path so it repaths from wherever it's standing
            // once it comes to, instead of resuming a now-stale route
            if (_stunTimer <= 0f)
            {
                _path.Clear();
                EndStun();
            }
            return;
        }

        if (EnemyPathGrid.Instance.AllNodes.Count == 0) return;
        if (_target == null && !Wanders) return;

        _repathTimer -= Time.deltaTime;
        _yieldTimer -= Time.deltaTime;
        _wanderTimer -= Time.deltaTime;
        Vector3 start = EnemyPathGrid.Instance.NearestNode(transform.position);
        Vector3 goal = Wanders ? WanderGoal(start) : EnemyPathGrid.Instance.NearestNode(_target.position);

        // Standing aside outranks chasing for as long as it takes to be passed.
        // Repathing mid-step would compute a route straight back down the lane
        // this is trying to clear, which is the whole thing it must not do.
        if (_yieldTimer > 0f)
        {
            FaceMovementDirection();
            return;
        }

        if (TryStepAside(start))
        {
            FaceMovementDirection();
            return;
        }

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
        if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive) return;
        if (_stunTimer > 0f) return;
        if (_target == null && !Wanders) return;

        Vector3 current = _rb.position;

        // Second line of defence behind OnEnable's reset: if the queued
        // waypoint is no longer walkable (the layout changed under us), drop
        // the whole path rather than walk to it, and let Update repath.
        if (_path.Count > 0 && !IsOverWalkable(_path[0]))
        {
            _path.Clear();
            _hasGoal = false;
            return;
        }

        // With no queued step, only close the gap directly when we're
        // already on the player's own tile (short-range, inherently safe).
        // If a real path is needed but hasn't been computed yet, stay put
        // rather than free-steer toward the player and risk walking onto
        // lava - Update() will supply a fresh grid step immediately.
        if (_path.Count == 0)
        {
            // A wanderer has nowhere it is owed: with no queued step it simply
            // stands until Update lays the next one, rather than closing on the
            // player the way a chaser does.
            if (Wanders) return;
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

        // chaseDir is grid-derived and safe, but separation pushes in an
        // arbitrary direction to unstack crowded enemies - which can shove
        // one clean off the walkable tiles onto lava. Take the separation
        // only when the resulting step stays on the grid, otherwise fall
        // back to the pure chase step.
        Vector3 separation = GetSeparation(current);
        Vector3 move = ClampToWalkable(current, (chaseDir * ChaseSpeed + separation * separationStrength) * Time.fixedDeltaTime, toDestination.magnitude);
        if (move == Vector3.zero)
        {
            move = ClampToWalkable(current, chaseDir * ChaseSpeed * Time.fixedDeltaTime, toDestination.magnitude);
        }

        _rb.MovePosition(current + move);
    }

    // Where a wanderer is headed. It keeps one errand until it arrives, loses
    // interest, or the board is rebuilt under it - anything shorter reads as
    // twitching rather than as a ghost going somewhere.
    private Vector3 WanderGoal(Vector3 start)
    {
        if (!_hasWanderGoal || _wanderTimer <= 0f || start == _wanderGoal || !IsOverWalkable(_wanderGoal))
        {
            _wanderGoal = PickWanderGoal(start);
            _hasWanderGoal = true;
            // Staggered so a pair of wanderers never turns on the same frame.
            _wanderTimer = wanderInterest * Random.Range(.7f, 1.3f);
        }
        return _wanderGoal;
    }

    // Somewhere worth walking to: far enough to be a trip, near the length it
    // likes, and - this is what keeps the ghosts off each other - in the part of
    // the board the others are furthest from. Sampled rather than searched: the
    // grid can be a thousand tiles and the answer only has to be plausible.
    private Vector3 PickWanderGoal(Vector3 start)
    {
        var nodes = EnemyPathGrid.Instance.AllNodes;
        if (nodes.Count == 0) return start;

        Vector3 best = start;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < Mathf.Max(1, wanderSamples); i++)
        {
            Vector3 candidate = nodes[Random.Range(0, nodes.Count)];
            float trip = Vector3.Distance(start, candidate);
            if (trip < wanderMinDistance) continue;
            // Elbow room counts up to a point - past a few tiles of clearance
            // one empty corner is as good as another, and the trip length is
            // what should decide between them.
            float score = Mathf.Min(ClearanceAt(candidate), 6f)
                          - Mathf.Abs(trip - wanderPreferredDistance) * .35f;
            if (score <= bestScore) continue;
            bestScore = score;
            best = candidate;
        }

        // On a board too small for a trip - or a very unlucky sample - step to a
        // neighbour instead, so it always has somewhere to be going.
        if (float.IsNegativeInfinity(bestScore))
        {
            var neighbours = EnemyPathGrid.Instance.GetNeighbors(start);
            if (neighbours.Count > 0) best = neighbours[Random.Range(0, neighbours.Count)];
        }
        return best;
    }

    private float ClearanceAt(Vector3 point)
    {
        float nearest = float.PositiveInfinity;
        foreach (var other in AllChasers)
        {
            if (other == this || other == null) continue;
            Vector3 diff = point - other.transform.position;
            diff.y = 0f;
            nearest = Mathf.Min(nearest, diff.magnitude);
        }
        return float.IsPositiveInfinity(nearest) ? 6f : nearest;
    }

    // Caps the step at the remaining distance, then rejects it outright
    // (returns zero) if it would land off the walkable tiles.
    private Vector3 ClampToWalkable(Vector3 current, Vector3 move, float maxDistance)
    {
        if (move.magnitude > maxDistance) move = move.normalized * maxDistance;
        return IsOverWalkable(current + move) ? move : Vector3.zero;
    }

    // A position counts as on the grid when the nearest walkable node is the
    // tile it's standing over - i.e. within half a tile on both axes.
    private static bool IsOverWalkable(Vector3 pos)
    {
        Vector3 node = EnemyPathGrid.Instance.NearestNode(pos);
        return Mathf.Abs(node.x - pos.x) <= 0.5f && Mathf.Abs(node.z - pos.z) <= 0.5f;
    }

    // A slower enemy pulls over and lets a faster one through rather than being
    // herded along in front of it down a one-tile-wide lane. Only the slower of
    // the pair ever moves, which is what makes it read as giving way instead of
    // the two of them jostling - and it means the rule can never deadlock,
    // since equal speeds mean nobody yields.
    //
    // Returns true when a detour was taken, in which case the queued path is
    // now the single side cell and the caller must not repath over it.
    private bool TryStepAside(Vector3 myNode)
    {
        var overtaker = FindOvertaker();
        if (overtaker == null) return false;

        Vector3 lane = overtaker.transform.forward;
        lane.y = 0f;
        if (lane.sqrMagnitude < 0.0001f) return false;
        lane.Normalize();

        // The cell worth ducking into is the one most side-on to the lane it is
        // coming down; anything along that lane is still in the way. Distance
        // from the overtaker breaks ties, so it doesn't step into its lap.
        Vector3 best = myNode;
        float bestScore = float.MaxValue;
        foreach (var n in EnemyPathGrid.Instance.GetNeighbors(myNode))
        {
            Vector3 dir = n - myNode;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;

            float alignment = Mathf.Abs(Vector3.Dot(dir.normalized, lane));
            float crowding = 1f / Mathf.Max(0.01f, (n - overtaker.transform.position).magnitude);
            float score = alignment + crowding * 0.25f;
            if (score < bestScore)
            {
                bestScore = score;
                best = n;
            }
        }

        // Nowhere to go - a dead end or a one-cell corridor. Carry on chasing
        // and let separation do what little it can.
        if (best == myNode) return false;

        _path.Clear();
        _path.Add(best);
        // Cleared so the tick after the wait repaths immediately from wherever
        // it ended up, rather than waiting out the normal repath interval.
        _hasGoal = false;
        _yieldTimer = yieldDuration;
        return true;
    }

    // The nearest chaser that is meaningfully faster, close, and actually
    // bearing down on us. Speed alone is not enough: something quicker on the
    // far side of the board is nobody's problem, and neither is one that
    // happens to be quick but is heading somewhere else.
    private EnemyChaser FindOvertaker()
    {
        Vector3 me = transform.position;
        EnemyChaser best = null;
        float bestDistSq = yieldRadius * yieldRadius;

        foreach (var other in AllChasers)
        {
            if (other == this || other == null || other.IsStunned) continue;
            if (other.speed <= speed + 0.05f) continue;

            Vector3 toMe = me - other.transform.position;
            toMe.y = 0f;
            float distSq = toMe.sqrMagnitude;
            if (distSq > bestDistSq || distSq < 0.0001f) continue;

            Vector3 heading = other.transform.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Dot(heading.normalized, toMe.normalized) < 0.6f) continue;

            bestDistSq = distSq;
            best = other;
        }

        return best;
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
                // Weighted so a faster enemy shoulders a slower one aside
                // instead of the two deflecting each other equally. Without
                // this the sidestep above gets half undone by the shove it
                // takes back from the enemy it is making room for.
                float weight = other.speed >= speed ? 1f : 0.35f;
                push += diff.normalized * ((separationRadius - dist) * weight);
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
        TryCatch(other);
    }

    // Also on stay, so a player who walked through a stunned enemy is caught
    // the moment it comes to rather than standing inside it safely - Enter
    // has already fired by then and won't fire again.
    private void OnTriggerStay(Collider other)
    {
        TryCatch(other);
    }

    private void TryCatch(Collider other)
    {
        if (IsStunned) return;
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
