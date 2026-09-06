using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sample;

// A friendly ghost that's scared of the player: instead of chasing, it
// flees along the walkable grid, always stepping toward whichever neighbor
// cell is furthest from the player. Catching it makes it vanish with a
// small animation, and pays out - it is worth twenty ordinary coins, and it
// only appears once a level, so running one down is the biggest single thing
// on the board.
[RequireComponent(typeof(Rigidbody))]
public class FriendlyGhostFlee : MonoBehaviour
{
    [SerializeField] private float speed = 2f;
    [SerializeField] private float repathInterval = 0.4f;
    [SerializeField] private float waypointTolerance = 0.15f;
    [SerializeField] private float vanishDuration = 0.6f;
    // distance-based backup for the trigger collider so a chase that ends
    // with the player right on top of it still registers as a catch even
    // if the mesh-collider trigger doesn't fire cleanly
    [SerializeField] private float captureRadius = 0.85f;
    [Tooltip("Coins paid into the wallet for catching it.")]
    [SerializeField] private int catchReward = 1000;

    private Rigidbody _rb;
    private Transform _target;
    private readonly List<Vector3> _path = new List<Vector3>();
    private float _repathTimer;
    private bool _caught;

    // How it stands when it is not being whisked away. The vanish leaves the
    // transform shrunk, spun and better than a metre in the air, and every one
    // of those would otherwise be carried into its next life: a ghost hovering
    // a metre up is out of reach of its own capture radius, so it cannot be
    // caught again, and each catch lifts it another metre.
    private float _restingHeight;
    private Quaternion _restingRotation = Quaternion.identity;
    private bool _restingKnown;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;

        RememberResting();

        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    // The scene authors how high this ghost floats. Read once, and read before
    // anything has had a chance to move it: the first placement can arrive
    // while the object is still inactive, which is before Awake has run.
    private void RememberResting()
    {
        if (_restingKnown) return;
        _restingHeight = transform.position.y;
        _restingRotation = transform.rotation;
        _restingKnown = true;
    }

    void OnEnable()
    {
        ResetState();
    }

    // Puts the ghost down for a new level. The move goes through the rigidbody
    // as well as the transform: it is kinematic, so a transform written on its
    // own leaves the body still standing on last level's tile, and the next
    // MovePosition drags the ghost back there - onto whatever the new board put
    // in that spot.
    public void PlaceAt(Vector3 spot)
    {
        RememberResting();
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        Vector3 landing = new Vector3(spot.x, _restingHeight, spot.z);
        transform.SetPositionAndRotation(landing, _restingRotation);
        if (_rb != null)
        {
            _rb.position = landing;
            _rb.rotation = _restingRotation;
        }
        ResetState();
    }

    // Called by LevelManager right before repositioning + reactivating this
    // ghost for a new level, so a previous capture doesn't carry over.
    public void ResetState()
    {
        RememberResting();
        _caught = false;
        _path.Clear();
        _repathTimer = 0f;
        transform.localScale = Vector3.one;
        // Undo the rest of the vanish, in case this is a plain re-enable rather
        // than a placement - height included, or it comes back uncatchable.
        transform.rotation = _restingRotation;
        transform.position = new Vector3(transform.position.x, _restingHeight, transform.position.z);
        if (_rb != null) _rb.position = transform.position;

        if (_target == null)
        {
            foreach (var ctrl in FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (ctrl.gameObject.name == "Ghost") { _target = ctrl.transform; break; }
            }
        }

        EnemyPathGrid.Instance.EnsureBuilt();
    }

    void Update()
    {
        if (_caught) return;

        // Distance-based catch check, independent of collider trigger
        // events - if the player is right on top of it, that's a catch
        // regardless of whether the mesh-collider trigger fired. Measured
        // across the board only: the two float at different heights, and how
        // high a ghost hovers is not something the player can chase.
        if (_target != null)
        {
            Vector3 toPlayer = _target.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude <= captureRadius)
            {
                Caught();
                return;
            }
        }

        if (_target == null || EnemyPathGrid.Instance.AllNodes.Count == 0) return;

        _repathTimer -= Time.deltaTime;
        Vector3 start = EnemyPathGrid.Instance.NearestNode(transform.position);

        if (_repathTimer <= 0f || _path.Count == 0)
        {
            _repathTimer = repathInterval;
            RecalculateFleePath(start);
        }

        FaceMovementDirection();
    }

    void FixedUpdate()
    {
        if (_caught || _path.Count == 0) return;

        Vector3 current = _rb.position;
        Vector3 destination = _path[0];
        Vector3 toDestination = new Vector3(destination.x - current.x, 0f, destination.z - current.z);

        if (toDestination.magnitude <= waypointTolerance)
        {
            // snap exactly onto the node instead of leaving it wherever the
            // last step happened to land - otherwise sub-tolerance drift
            // can quietly accumulate step after step until, after enough
            // waypoints, the path visibly cuts corners near lava tiles
            // (whose collider is intentionally a bit oversized) even though
            // every chosen node was always a valid walkable one.
            _rb.MovePosition(new Vector3(destination.x, current.y, destination.z));
            _path.RemoveAt(0);
            return;
        }

        Vector3 move = toDestination.normalized * speed * Time.fixedDeltaTime;
        if (move.magnitude > toDestination.magnitude) move = move.normalized * toDestination.magnitude;
        _rb.MovePosition(current + move);
    }

    private void FaceMovementDirection()
    {
        if (_path.Count == 0) return;
        Vector3 toDestination = new Vector3(_path[0].x - transform.position.x, 0f, _path[0].z - transform.position.z);
        if (toDestination.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(toDestination.normalized);
    }

    // Greedy one-step lookahead like the "dumb but fast" chaser, just
    // maximizing distance instead of minimizing it - picks whichever
    // neighbor puts the most space between itself and the player.
    private void RecalculateFleePath(Vector3 start)
    {
        _path.Clear();
        if (_target == null) return;

        Vector3 best = start;
        float bestDistSq = -1f;
        foreach (var n in EnemyPathGrid.Instance.GetNeighbors(start))
        {
            float d = (n - _target.position).sqrMagnitude;
            if (d > bestDistSq)
            {
                bestDistSq = d;
                best = n;
            }
        }
        if (best != start) _path.Add(best);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_caught || !other.CompareTag("Ghost")) return;
        Caught();
    }

    private void Caught()
    {
        if (_caught) return;
        _caught = true;
        AudioManager.Play(GameSound.Reward);
        _path.Clear();

        // Paid the moment it is caught rather than at the end of the vanish, so
        // the reward cannot be lost to a level change landing mid-animation.
        if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(catchReward);

        StartCoroutine(VanishRoutine());
    }

    // excited little hop, then spins and shrinks while rising - reads as
    // "caught and whisked away" rather than just popping out of existence
    private IEnumerator VanishRoutine()
    {
        float popDuration = vanishDuration * 0.25f;
        float shrinkDuration = vanishDuration - popDuration;
        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.position;

        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float p = t / popDuration;
            transform.localScale = startScale * Mathf.Lerp(1f, 1.3f, EaseOutBack(p));
            yield return null;
        }

        Vector3 popScale = transform.localScale;
        t = 0f;
        while (t < shrinkDuration)
        {
            t += Time.deltaTime;
            float p = t / shrinkDuration;
            transform.localScale = Vector3.Lerp(popScale, Vector3.zero, p);
            transform.position = startPos + Vector3.up * (p * 1.2f);
            transform.Rotate(0f, 720f * Time.deltaTime, 0f, Space.World);
            yield return null;
        }

        gameObject.SetActive(false);
    }

    private static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = p - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
