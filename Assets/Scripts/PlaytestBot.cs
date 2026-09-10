using System.Collections.Generic;
using UnityEngine;
using Sample;

// How a test bot plays: every way a person is worse than a program, as a number.
//
// A bot reading the board out of the code knows where every hunter is, this
// frame, to the millimetre, and could dodge them all forever - which says
// nothing about whether a child can finish level four. So everything a player
// is limited by is written down here instead: how late they see things, how far
// they look, how straight they steer, how much they plan, how often they forget
// they have a freeze in their pocket.
//
// The two profiles are a first guess, not a measurement. The way to make them
// true is to record real players with the same PlaytestLog and move these
// numbers until the bot dies where they die.
[System.Serializable]
public class PlaytestBotProfile
{
    public string name;

    [Header("Seeing")]
    [Tooltip("Seconds between something happening and the bot reacting to it.")]
    public float reactionTime;
    [Tooltip("Tiles. Hunters further away than this are not being watched at all.")]
    public float awareness;
    [Tooltip("Judges a hunter by how far it has to walk, not how close it looks - lava between the two is noticed.")]
    public bool pathDistanceDanger;
    [Tooltip("Leads a moving hunter by the reaction delay instead of reacting to where it was.")]
    public bool predictsMotion;

    [Header("Planning")]
    [Tooltip("Plans the cheapest route to the cheapest coin, rather than heading for whichever coin looks nearest.")]
    public bool plansRoute;
    [Tooltip("Tiles over which a hunter makes the floor around it feel dangerous.")]
    public float dangerRadius;
    [Tooltip("How far out of its way the bot goes to stay off dangerous floor.")]
    public float dangerWeight;
    [Tooltip("A hunter this close (tiles, or steps with path distance) and coins stop mattering.")]
    public float fleeDistance;
    [Tooltip("And once running, this far before it feels safe to go back to the coins - nobody turns round the moment the gap opens by a hair.")]
    public float safeDistance;
    [Tooltip("Runs for open floor with a way out, rather than just stepping away from the nearest hunter.")]
    public bool smartFlee;

    [Header("Hands")]
    [Tooltip("Degrees the stick wanders off the line the bot means to walk.")]
    public float aimError;
    [Tooltip("How close to a tile's middle the bot gets before turning the corner. Bigger cuts corners - and lava.")]
    public float cornerTolerance;
    [Tooltip("Average seconds between moments of looking away.")]
    public float distractionEvery;
    public float distractionMin;
    public float distractionMax;

    [Header("Abilities")]
    [Tooltip("Uses abilities ahead of trouble - crowds, dead ends, corridors - rather than only in a panic.")]
    public bool proactiveAbilities;
    [Tooltip("A hunter this close is a panic.")]
    public float panicDistance;
    [Tooltip("Chance a panic goes by without the bot remembering to use anything.")]
    public float forgetChance;
    [Tooltip("Seconds after one ability before another is considered.")]
    public float abilityCooldown;

    [Header("Friendly ghost and shop")]
    public float friendlyGhostRange;
    [Tooltip("Only goes after the friendly ghost when no hunter is near it.")]
    public bool friendlyGhostOnlyWhenSafe;
    [Tooltip("Keeps a life in reserve and a stock of each ability, rather than buying whatever it can afford.")]
    public bool strategicShopping;

    // Aimed at the 10-15 year olds normal mode is for: slower to react, watches
    // only what is near, heads for the coin it can see, cuts corners, forgets
    // its abilities, and spends on impulse.
    public static readonly PlaytestBotProfile Kid = new PlaytestBotProfile
    {
        name = "Kid",
        reactionTime = .30f,
        awareness = 4.5f,
        pathDistanceDanger = false,
        predictsMotion = false,
        plansRoute = false,
        dangerRadius = 3f,
        dangerWeight = 6f,
        fleeDistance = 2.2f,
        safeDistance = 3.5f,
        smartFlee = false,
        aimError = 12f,
        cornerTolerance = .4f,
        distractionEvery = 25f,
        distractionMin = .4f,
        distractionMax = 1f,
        proactiveAbilities = false,
        panicDistance = 1.6f,
        forgetChance = .4f,
        abilityCooldown = 2f,
        friendlyGhostRange = 6f,
        friendlyGhostOnlyWhenSafe = false,
        strategicShopping = false
    };

    // Sixteen and up, which is who hard is for.
    public static readonly PlaytestBotProfile Teen = new PlaytestBotProfile
    {
        name = "Teen",
        reactionTime = .19f,
        awareness = 8f,
        pathDistanceDanger = true,
        predictsMotion = true,
        plansRoute = true,
        dangerRadius = 4f,
        dangerWeight = 8f,
        fleeDistance = 2f,
        safeDistance = 3.5f,
        smartFlee = true,
        aimError = 4f,
        cornerTolerance = .12f,
        distractionEvery = 90f,
        distractionMin = .2f,
        distractionMax = .4f,
        proactiveAbilities = true,
        panicDistance = 1.3f,
        forgetChance = .05f,
        abilityCooldown = 1.2f,
        friendlyGhostRange = 8f,
        friendlyGhostOnlyWhenSafe = true,
        strategicShopping = true
    };
}

// Plays the board for a test run: moves the character through the same stick
// the player's thumb does, spends abilities and coins through the same managers
// the buttons do, and presses Next on the level complete card. Nothing here
// changes a rule of the game - it only decides what to press.
public class PlaytestBot : MonoBehaviour
{
    private const float ThinkInterval = .1f;
    private const float Far = 99f;

    private static PlaytestBot _driver;

    // GhostScript asks this every frame. While a bot is on the board its answer
    // replaces every stick, key and pad - standing still included.
    public static bool TrySteer(out Vector3 direction)
    {
        if (_driver == null || !_driver.isActiveAndEnabled)
        {
            direction = Vector3.zero;
            return false;
        }
        direction = _driver._steer;
        return true;
    }

    private PlaytestBotProfile _p;
    private GhostScript _player;
    private FriendlyGhostFlee _friendly;
    private Vector3 _steer;

    // The walkable grid, copied into arrays so the planner can run ten times a
    // second without allocating. Rebuilt whenever EnemyPathGrid is.
    private int _gridVersion = -1;
    private readonly List<Vector3> _nodes = new List<Vector3>();
    private readonly Dictionary<Vector3, int> _index = new Dictionary<Vector3, int>();
    private int[][] _adj;
    private float[] _enemyDist;
    private float[] _cost;
    private int[] _prev;
    private int[] _depth;
    private readonly Queue<int> _queue = new Queue<int>();
    private readonly List<KeyValuePair<float, int>> _heap = new List<KeyValuePair<float, int>>();

    // What the bot has seen, kept for as long as its reaction time: it always
    // acts on the oldest snapshot it has, never on this frame.
    private struct Seen
    {
        public Vector3 position;
        public Vector3 velocity;
        public bool stunned;
    }

    private struct Snapshot
    {
        public float time;
        public Seen[] enemies;
    }

    private readonly List<Snapshot> _history = new List<Snapshot>();
    private readonly Dictionary<EnemyChaser, Vector3> _lastSeenAt = new Dictionary<EnemyChaser, Vector3>();
    private float _lastSnapshot = -1f;
    private Seen[] _perceived = new Seen[0];

    private readonly List<Vector3> _path = new List<Vector3>();
    private int _waypoint;
    private float _thinkTimer;
    private Coin _targetCoin;
    private bool _fleeing;
    private float _escapeRoom;
    private float _distractedUntil;
    private float _noise;
    private float _noiseTarget;
    private float _noiseTimer;
    private float _abilityReadyAt;
    private bool _inPanic;
    private Vector3 _stuckFrom;
    private float _stuckTimer;
    private float _unstickUntil;
    private Vector3 _unstickDir;
    private float _levelCompleteWait = -1f;

    public void Init(PlaytestBotProfile profile)
    {
        _p = profile;
        _driver = this;
    }

    void OnDestroy()
    {
        if (_driver == this) _driver = null;
    }

    void Update()
    {
        if (_p == null) return;
        if (_player == null) _player = FindAnyObjectByType<GhostScript>();
        if (_player == null) return;

        if (HandleLevelComplete() || Time.deltaTime <= 0f
            || (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive))
        {
            _steer = Vector3.zero;
            return;
        }

        EnsureGrid();
        Observe();

        if (_player.IsDead || EnemySpawnManager.PlayerFrozen || _nodes.Count == 0)
        {
            _steer = Vector3.zero;
            _path.Clear();
            return;
        }

        if (Distracted())
        {
            _steer = Vector3.zero;
            return;
        }

        _thinkTimer -= Time.deltaTime;
        if (_thinkTimer <= 0f)
        {
            _thinkTimer = ThinkInterval;
            Think();
        }

        Steer();
    }

    // ---- between levels -----------------------------------------------------

    // The card holds the game at a standstill, so the wait is counted in frames
    // of real time - a beat to look at it, a visit to the shop, then Next.
    private bool HandleLevelComplete()
    {
        var level = LevelManager.Instance;
        if (level == null || !level.IsLevelCompleteActive)
        {
            _levelCompleteWait = -1f;
            return false;
        }

        if (_levelCompleteWait < 0f) _levelCompleteWait = 0f;
        _levelCompleteWait += Mathf.Max(Time.unscaledDeltaTime, 1f / 60f);
        if (_levelCompleteWait < .6f) return true;

        _levelCompleteWait = -1f;
        Shop();
        _path.Clear();
        _targetCoin = null;
        _history.Clear();
        _lastSeenAt.Clear();
        level.NextLevel();
        return true;
    }

    private void Shop()
    {
        var shop = ShopManager.Instance;
        if (shop == null || EconomyManager.Instance == null) return;
        var lives = LivesManager.Instance;

        if (_p.strategicShopping)
        {
            // A spare life first, then a working stock of each ability - but
            // never spending the price of the next life on anything else while
            // there is room for one.
            for (int guard = 0; guard < 16; guard++)
            {
                if (lives != null && lives.CurrentLives < 3 && shop.BuyExtraLife()) continue;
                int reserve = lives != null && lives.CurrentLives < LivesManager.HardCap ? shop.GetExtraLifeCost() : 0;
                int wallet = EconomyManager.Instance.TotalCoins;
                if (Owned(AbilityBarUI.Ability.Freeze) < 2 && wallet - shop.GetFreezeCost() >= reserve && shop.BuyFreeze()) continue;
                if (Owned(AbilityBarUI.Ability.Shield) < 2 && wallet - shop.GetShieldCost() >= reserve && shop.BuyShield()) continue;
                if (Owned(AbilityBarUI.Ability.Teleport) < 1 && wallet - shop.GetTeleportCost() >= reserve && shop.BuyTeleport()) continue;
                if (Owned(AbilityBarUI.Ability.Trap) < 2 && wallet - shop.GetTrapCost() >= reserve && shop.BuyTrap()) continue;
                break;
            }
            return;
        }

        // On impulse: a couple of things at most, whatever it can afford, with a
        // life grabbed first when it is down to its last one.
        int purchases = Random.Range(0, 3);
        for (int i = 0; i < purchases; i++)
        {
            if (lives != null && lives.CurrentLives <= 1 && Random.value < .6f && shop.BuyExtraLife()) continue;

            var options = new List<AbilityBarUI.Ability>();
            if (shop.CanBuyTrap() && Owned(AbilityBarUI.Ability.Trap) < AbilityBarUI.MaxCount) options.Add(AbilityBarUI.Ability.Trap);
            if (shop.CanBuyFreeze() && Owned(AbilityBarUI.Ability.Freeze) < AbilityBarUI.MaxCount) options.Add(AbilityBarUI.Ability.Freeze);
            if (shop.CanBuyShield() && Owned(AbilityBarUI.Ability.Shield) < AbilityBarUI.MaxCount) options.Add(AbilityBarUI.Ability.Shield);
            if (shop.CanBuyTeleport() && Owned(AbilityBarUI.Ability.Teleport) < AbilityBarUI.MaxCount) options.Add(AbilityBarUI.Ability.Teleport);
            if (options.Count == 0) break;

            switch (options[Random.Range(0, options.Count)])
            {
                case AbilityBarUI.Ability.Trap: shop.BuyTrap(); break;
                case AbilityBarUI.Ability.Freeze: shop.BuyFreeze(); break;
                case AbilityBarUI.Ability.Shield: shop.BuyShield(); break;
                case AbilityBarUI.Ability.Teleport: shop.BuyTeleport(); break;
            }
        }
    }

    // ---- seeing -------------------------------------------------------------

    private void EnsureGrid()
    {
        var grid = EnemyPathGrid.Instance;
        grid.EnsureBuilt();
        if (_adj != null && grid.Version == _gridVersion) return;
        _gridVersion = grid.Version;

        _nodes.Clear();
        _index.Clear();
        foreach (var node in grid.AllNodes)
        {
            _index[node] = _nodes.Count;
            _nodes.Add(node);
        }

        int count = _nodes.Count;
        _adj = new int[count][];
        for (int i = 0; i < count; i++)
        {
            var neighbours = grid.GetNeighbors(_nodes[i]);
            _adj[i] = new int[neighbours.Count];
            for (int j = 0; j < neighbours.Count; j++) _adj[i][j] = _index[neighbours[j]];
        }
        _enemyDist = new float[count];
        _cost = new float[count];
        _prev = new int[count];
        _depth = new int[count];
        _path.Clear();
        _targetCoin = null;
    }

    private int NodeOf(Vector3 position)
    {
        if (_nodes.Count == 0) return -1;
        return _index.TryGetValue(EnemyPathGrid.Instance.NearestNode(position), out int i) ? i : -1;
    }

    private void Observe()
    {
        float now = Time.time;
        if (now - _lastSnapshot >= .05f)
        {
            float dt = _lastSnapshot < 0f ? 0f : now - _lastSnapshot;
            _lastSnapshot = now;

            var enemies = EnemyChaser.Active;
            var seen = new Seen[enemies.Count];
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                Vector3 at = enemy.transform.position;
                Vector3 velocity = Vector3.zero;
                if (dt > 0f && _lastSeenAt.TryGetValue(enemy, out Vector3 before)) velocity = (at - before) / dt;
                velocity.y = 0f;
                // A respawn moves a hunter across the board in one frame; that is
                // not a speed anyone would lead a target by.
                velocity = Vector3.ClampMagnitude(velocity, 5f);
                _lastSeenAt[enemy] = at;
                seen[i] = new Seen { position = at, velocity = velocity, stunned = enemy.IsStunned };
            }
            _history.Add(new Snapshot { time = now, enemies = seen });
        }

        // Keep the newest snapshot that is already old enough to have been
        // reacted to, and everything newer; act on that one.
        float cutoff = now - _p.reactionTime;
        int keep = 0;
        for (int i = 0; i < _history.Count; i++)
            if (_history[i].time <= cutoff) keep = i;
        if (keep > 0) _history.RemoveRange(0, keep);
        _perceived = _history.Count > 0 ? _history[0].enemies : new Seen[0];
    }

    private Vector3 Perceived(Seen seen) =>
        _p.predictsMotion ? seen.position + seen.velocity * _p.reactionTime : seen.position;

    private bool Watching(Seen seen, Vector3 from) =>
        !seen.stunned && Planar(Perceived(seen), from) <= _p.awareness;

    // ---- thinking -----------------------------------------------------------

    private void Think()
    {
        _path.Clear();
        _waypoint = 0;
        int me = NodeOf(_player.transform.position);
        if (me < 0) return;

        bool shielded = _player.ShieldActive;
        MeasureThreat(shielded);
        Route(me);

        NearestHunter(out float near, out int crowd, out Vector3 nearAt);
        float threat = _enemyDist[me];
        _fleeing = !shielded && (threat <= _p.fleeDistance || (_fleeing && threat < _p.safeDistance));
        int goal = _fleeing ? FleeGoal(me) : ChooseTarget(me);

        UseAbilities(me, near, crowd, nearAt);

        if (goal >= 0) BuildPath(me, goal);
    }

    // How far every tile is from the nearest hunter the bot is watching - by
    // walking distance for a player who reads the maze, straight line for one
    // who does not. A shield makes them nobody's problem for a while.
    private void MeasureThreat(bool ignoreHunters)
    {
        for (int i = 0; i < _nodes.Count; i++) _enemyDist[i] = Far;
        if (ignoreHunters) return;

        Vector3 me = _player.transform.position;
        if (_p.pathDistanceDanger)
        {
            _queue.Clear();
            foreach (var seen in _perceived)
            {
                if (!Watching(seen, me)) continue;
                int at = NodeOf(Perceived(seen));
                if (at < 0 || _enemyDist[at] == 0f) continue;
                _enemyDist[at] = 0f;
                _queue.Enqueue(at);
            }
            while (_queue.Count > 0)
            {
                int current = _queue.Dequeue();
                foreach (int next in _adj[current])
                {
                    if (_enemyDist[next] <= _enemyDist[current] + 1f) continue;
                    _enemyDist[next] = _enemyDist[current] + 1f;
                    _queue.Enqueue(next);
                }
            }
            return;
        }

        foreach (var seen in _perceived)
        {
            if (!Watching(seen, me)) continue;
            Vector3 at = Perceived(seen);
            for (int i = 0; i < _nodes.Count; i++)
                _enemyDist[i] = Mathf.Min(_enemyDist[i], Planar(_nodes[i], at));
        }
    }

    private float StepCost(int node)
    {
        float d = _enemyDist[node];
        if (d >= Far) return 1f;
        float closeness = Mathf.Max(0f, 1f - d / _p.dangerRadius);
        float cost = 1f + _p.dangerWeight * closeness * closeness;
        if (d < 1f) cost += 40f;
        return cost;
    }

    // Cheapest way from here to every tile, where a tile near a hunter costs
    // more to cross. Dijkstra over a small binary heap.
    private void Route(int from)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            _cost[i] = float.MaxValue;
            _prev[i] = -1;
        }
        _heap.Clear();
        _cost[from] = 0f;
        Push(0f, from);

        while (_heap.Count > 0)
        {
            var top = Pop();
            int current = top.Value;
            if (top.Key > _cost[current]) continue;
            foreach (int next in _adj[current])
            {
                float cost = _cost[current] + StepCost(next);
                if (cost >= _cost[next]) continue;
                _cost[next] = cost;
                _prev[next] = current;
                Push(cost, next);
            }
        }
    }

    private void BuildPath(int from, int goal)
    {
        if (goal != from && _prev[goal] < 0) return;
        for (int node = goal; node != -1; node = node == from ? -1 : _prev[node])
            _path.Add(_nodes[node]);
        _path.Reverse();
    }

    private void NearestHunter(out float near, out int crowd, out Vector3 nearAt)
    {
        near = Far;
        crowd = 0;
        nearAt = Vector3.zero;
        Vector3 me = _player.transform.position;
        foreach (var seen in _perceived)
        {
            if (!Watching(seen, me)) continue;
            Vector3 at = Perceived(seen);
            float d = Planar(at, me);
            if (d <= 3.5f) crowd++;
            if (d >= near) continue;
            near = d;
            nearAt = at;
        }
    }

    private int ChooseTarget(int me)
    {
        if (FriendlyGhostWorthIt(out int ghostNode)) return ghostNode;

        var coins = Coin.Active;
        if (coins.Count == 0) return -1;
        bool stillOut = _targetCoin != null && Contains(coins, _targetCoin);

        if (!_p.plansRoute)
        {
            // The coin that looks closest, and then that coin until it is
            // taken - not re-weighed every step. Only a hunter standing right
            // over it makes it someone else's coin for now.
            if (stillOut && Guarded(_targetCoin)) stillOut = false;
            if (!stillOut)
            {
                Coin open = null;
                Coin any = null;
                float bestOpen = float.MaxValue;
                float bestAny = float.MaxValue;
                foreach (var coin in coins)
                {
                    float d = Planar(coin.transform.position, _player.transform.position);
                    if (d < bestAny)
                    {
                        bestAny = d;
                        any = coin;
                    }
                    if (d < bestOpen && !Guarded(coin))
                    {
                        bestOpen = d;
                        open = coin;
                    }
                }
                _targetCoin = open != null ? open : any;
            }
            return NodeOf(_targetCoin.transform.position);
        }

        // The cheapest coin to reach, sticking with the current one unless
        // another is clearly better, so the route does not flicker.
        Coin cheapest = null;
        float cheapestCost = float.MaxValue;
        foreach (var coin in coins)
        {
            int at = NodeOf(coin.transform.position);
            if (at < 0 || _cost[at] >= cheapestCost) continue;
            cheapestCost = _cost[at];
            cheapest = coin;
        }
        if (stillOut)
        {
            int current = NodeOf(_targetCoin.transform.position);
            if (current >= 0 && _cost[current] <= cheapestCost * 1.25f) cheapest = _targetCoin;
        }
        _targetCoin = cheapest;
        return cheapest != null ? NodeOf(cheapest.transform.position) : -1;
    }

    private bool Guarded(Coin coin)
    {
        int at = NodeOf(coin.transform.position);
        return at >= 0 && _enemyDist[at] < 1.5f;
    }

    private bool FriendlyGhostWorthIt(out int node)
    {
        node = -1;
        if (_friendly == null) _friendly = FindAnyObjectByType<FriendlyGhostFlee>(FindObjectsInactive.Include);
        if (_friendly == null || !_friendly.isActiveAndEnabled) return false;

        Vector3 at = _friendly.transform.position;
        float d = Planar(at, _player.transform.position);
        if (d > _p.friendlyGhostRange) return false;
        node = NodeOf(at);
        if (node < 0) return false;
        if (_p.friendlyGhostOnlyWhenSafe && (_enemyDist[node] < 4f || _cost[node] > d * 2.5f + 3f)) return false;
        return true;
    }

    // Where to run: a few tiles ahead at most, never squeezing past a hunter to
    // get there, to whichever tile has the most room from them. The careful
    // player looks further, and also counts the ways out - a tile with three
    // neighbours beats a pocket with one. The other has no thought for whether
    // it is running into a dead end, which is how children get cornered.
    private int FleeGoal(int me)
    {
        int reach = _p.smartFlee ? 7 : 4;
        for (int i = 0; i < _nodes.Count; i++)
        {
            _prev[i] = -1;
            _depth[i] = -1;
        }
        _queue.Clear();
        _depth[me] = 0;
        _queue.Enqueue(me);
        int refuge = me;
        float best = RefugeScore(me);
        while (_queue.Count > 0)
        {
            int current = _queue.Dequeue();
            if (_depth[current] >= reach) continue;
            foreach (int next in _adj[current])
            {
                if (_depth[next] >= 0) continue;
                _depth[next] = _depth[current] + 1;
                if (_enemyDist[next] < 1f) continue;
                _prev[next] = current;
                _queue.Enqueue(next);
                float score = RefugeScore(next);
                if (score <= best) continue;
                best = score;
                refuge = next;
            }
        }
        _escapeRoom = _enemyDist[refuge];
        return refuge;
    }

    // Even a child sees a pocket with one way in, a little: the ways out count
    // for something either way, only much more for the careful player.
    private float RefugeScore(int node) =>
        Mathf.Min(_enemyDist[node], 10f)
        + (_p.smartFlee ? .6f * _adj[node].Length - .1f * _depth[node] : .3f * _adj[node].Length);

    // ---- abilities ----------------------------------------------------------

    private void UseAbilities(int me, float near, int crowd, Vector3 nearAt)
    {
        if (Time.time < _abilityReadyAt) return;
        if (near >= Far)
        {
            _inPanic = false;
            return;
        }

        if (!_p.proactiveAbilities)
        {
            // Only when something is right on top of them, once per scare, and
            // not every time: sometimes the button is simply forgotten.
            if (near > _p.panicDistance + 1f) _inPanic = false;
            if (near <= _p.panicDistance && !_inPanic)
            {
                _inPanic = true;
                if (Random.value >= _p.forgetChance && UseAny(Shuffled())) Spent();
                return;
            }
            // Now and then a trap goes down behind them while something follows.
            if (near < 3f && Behind(nearAt) && Random.value < .03f && Use(AbilityBarUI.Ability.Trap)) Spent();
            return;
        }

        if (_player.ShieldActive) return;

        bool cornered = _fleeing && _escapeRoom <= 2f && near <= 2.2f;
        if (cornered || near <= _p.panicDistance)
        {
            if (Random.value < _p.forgetChance)
            {
                Spent();
                return;
            }
            bool used = cornered
                ? UseAny(AbilityBarUI.Ability.Teleport, AbilityBarUI.Ability.Freeze, AbilityBarUI.Ability.Shield)
                : UseAny(AbilityBarUI.Ability.Shield, AbilityBarUI.Ability.Freeze, AbilityBarUI.Ability.Teleport);
            if (used)
            {
                Spent();
                return;
            }
        }

        if (crowd >= 2 && near <= 3f && Use(AbilityBarUI.Ability.Freeze))
        {
            Spent();
            return;
        }

        // A corridor with something coming up behind is what a trap is for.
        if (near <= 3.2f && Behind(nearAt) && _adj[me].Length <= 2 && Use(AbilityBarUI.Ability.Trap)) Spent();
    }

    private void Spent() => _abilityReadyAt = Time.time + _p.abilityCooldown;

    private bool Behind(Vector3 hunter)
    {
        if (_steer.sqrMagnitude < .01f) return false;
        Vector3 toHunter = hunter - _player.transform.position;
        toHunter.y = 0f;
        return toHunter.sqrMagnitude > .0001f && Vector3.Dot(_steer.normalized, toHunter.normalized) < -.3f;
    }

    private static AbilityBarUI.Ability[] Shuffled()
    {
        var all = new[] { AbilityBarUI.Ability.Shield, AbilityBarUI.Ability.Freeze, AbilityBarUI.Ability.Teleport };
        for (int i = all.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        return all;
    }

    private bool UseAny(params AbilityBarUI.Ability[] abilities)
    {
        foreach (var ability in abilities)
            if (Use(ability)) return true;
        return false;
    }

    // Through the manager, exactly as the button would; whether it worked is
    // read off the stock rather than assumed, since every manager can refuse.
    private static bool Use(AbilityBarUI.Ability ability)
    {
        int before = Owned(ability);
        if (before <= 0) return false;
        switch (ability)
        {
            case AbilityBarUI.Ability.Trap: TrapManager.Instance.PlaceTrap(); break;
            case AbilityBarUI.Ability.Freeze: FreezeManager.Instance.UseFreeze(); break;
            case AbilityBarUI.Ability.Shield: ShieldManager.Instance.UseShield(); break;
            case AbilityBarUI.Ability.Teleport: TeleportManager.Instance.UseTeleport(); break;
        }
        return Owned(ability) < before;
    }

    private static int Owned(AbilityBarUI.Ability ability)
    {
        switch (ability)
        {
            case AbilityBarUI.Ability.Trap: return TrapManager.Instance != null ? TrapManager.Instance.TrapsOwned : 0;
            case AbilityBarUI.Ability.Freeze: return FreezeManager.Instance != null ? FreezeManager.Instance.FreezesOwned : 0;
            case AbilityBarUI.Ability.Shield: return ShieldManager.Instance != null ? ShieldManager.Instance.ShieldsOwned : 0;
            case AbilityBarUI.Ability.Teleport: return TeleportManager.Instance != null ? TeleportManager.Instance.TeleportsOwned : 0;
        }
        return 0;
    }

    // ---- hands --------------------------------------------------------------

    private bool Distracted()
    {
        if (Time.time < _distractedUntil) return true;
        if (_p.distractionEvery <= 0f || Random.value >= Time.deltaTime / _p.distractionEvery) return false;
        _distractedUntil = Time.time + Random.Range(_p.distractionMin, _p.distractionMax);
        return true;
    }

    private void Steer()
    {
        Vector3 at = _player.transform.position;
        if (Unsticking(at)) return;
        if (_path.Count == 0)
        {
            _steer = Vector3.zero;
            return;
        }

        // On to the next tile once this one is close enough - or once it has
        // been passed along the line to the next, so the bot never doubles back
        // to touch a tile centre it has already walked through.
        while (_waypoint < _path.Count - 1 && Reached(at, _path[_waypoint], _path[_waypoint + 1])) _waypoint++;

        Vector3 to = _path[_waypoint] - at;
        to.y = 0f;
        if (to.magnitude < .06f)
        {
            _steer = Vector3.zero;
            return;
        }
        _steer = Quaternion.Euler(0f, Noise(), 0f) * to.normalized;
    }

    private bool Reached(Vector3 at, Vector3 waypoint, Vector3 next)
    {
        if (Planar(at, waypoint) <= _p.cornerTolerance) return true;
        Vector3 along = next - waypoint;
        along.y = 0f;
        Vector3 offset = at - waypoint;
        offset.y = 0f;
        float ahead = Vector3.Dot(offset, along.normalized);
        float aside = (offset - along.normalized * ahead).magnitude;
        return ahead > 0f && aside <= _p.cornerTolerance;
    }

    // The stick never points exactly where it is meant to: it drifts a few
    // degrees either side and settles somewhere new every few tenths of a second.
    private float Noise()
    {
        _noiseTimer -= Time.deltaTime;
        if (_noiseTimer <= 0f)
        {
            _noiseTimer = Random.Range(.2f, .5f);
            _noiseTarget = Random.Range(-_p.aimError, _p.aimError);
        }
        _noise = Mathf.MoveTowards(_noise, _noiseTarget, 60f * Time.deltaTime);
        return _noise;
    }

    // Pushing into a wall corner for a second and a half is not a strategy
    // anyone keeps up; it wiggles out towards a neighbouring tile and replans.
    private bool Unsticking(Vector3 at)
    {
        // Running back and forth from a hunter can look like being stuck, and a
        // random wiggle then is a step into it.
        if (_fleeing)
        {
            _unstickUntil = 0f;
            _stuckTimer = 0f;
            _stuckFrom = at;
            return false;
        }

        if (Time.time < _unstickUntil)
        {
            _steer = _unstickDir;
            return true;
        }

        if (_steer.sqrMagnitude < .01f)
        {
            _stuckTimer = 0f;
            _stuckFrom = at;
            return false;
        }

        _stuckTimer += Time.deltaTime;
        if (_stuckTimer < 1.5f) return false;
        bool stuck = Planar(at, _stuckFrom) < .25f;
        _stuckTimer = 0f;
        _stuckFrom = at;
        if (!stuck) return false;

        int me = NodeOf(at);
        if (me < 0 || _adj[me].Length == 0) return false;
        Vector3 to = _nodes[_adj[me][Random.Range(0, _adj[me].Length)]] - at;
        to.y = 0f;
        _unstickDir = to.normalized;
        _unstickUntil = Time.time + .4f;
        _steer = _unstickDir;
        return true;
    }

    // ---- small things -------------------------------------------------------

    private static float Planar(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static bool Contains(IReadOnlyList<Coin> coins, Coin coin)
    {
        for (int i = 0; i < coins.Count; i++)
            if (coins[i] == coin) return true;
        return false;
    }

    private void Push(float key, int node)
    {
        _heap.Add(new KeyValuePair<float, int>(key, node));
        int i = _heap.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (_heap[parent].Key <= _heap[i].Key) break;
            (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
            i = parent;
        }
    }

    private KeyValuePair<float, int> Pop()
    {
        var top = _heap[0];
        int last = _heap.Count - 1;
        _heap[0] = _heap[last];
        _heap.RemoveAt(last);
        int i = 0;
        while (true)
        {
            int left = i * 2 + 1;
            int right = left + 1;
            int smallest = i;
            if (left < _heap.Count && _heap[left].Key < _heap[smallest].Key) smallest = left;
            if (right < _heap.Count && _heap[right].Key < _heap[smallest].Key) smallest = right;
            if (smallest == i) break;
            (_heap[smallest], _heap[i]) = (_heap[i], _heap[smallest]);
            i = smallest;
        }
        return top;
    }
}
