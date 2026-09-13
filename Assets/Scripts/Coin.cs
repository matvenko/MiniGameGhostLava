using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sample;

public class Coin : MonoBehaviour
{
    private static readonly List<Coin> Uncollected = new List<Coin>();

    // Every coin still out on the board. A coin leaves this the instant it is
    // taken rather than when its pickup animation finishes destroying it, so
    // anything counting what is left - the last-coin indicator - never counts
    // one that is already shrinking away.
    public static IReadOnlyList<Coin> Active => Uncollected;

    void OnEnable()
    {
        if (!_collected) Uncollected.Add(this);
    }

    void OnDisable()
    {
        Uncollected.Remove(this);
    }

    private float rotationSpeed = 200f;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float pickupDuration = 0.3f;
    // What a coin is worth is drawn from this when it is taken, not when it is
    // spawned: nothing on the board shows the value, so rolling it at the moment
    // of pickup is the same thing to the player and leaves no state to carry
    // around. Flat odds - every entry is as likely as any other, so adding a
    // fifth number here changes the odds of all of them.
    [Tooltip("One of these is paid into the wallet, picked at random, each time a coin is taken.")]
    [SerializeField] private int[] walletValues = { 150, 200, 250, 300 };

    // Optional burst played where the coin was taken. It is spawned unparented:
    // the pickup animation destroys the coin's whole prefab root a moment later,
    // and a child effect would be torn down with it mid-flash.
    [SerializeField] private GameObject pickupEffect;
    [SerializeField] private float pickupEffectLifetime = 2f;

    // The blob on the ground under the coin. It hangs off the prefab root, not
    // off this disc, so that it stays flat while the disc tumbles - which also
    // means the pickup animation has to shrink it by hand.
    [SerializeField] private Transform shadow;

    private bool _collected;

    // How fast the coin turns about world Y, as a share of its flip speed about
    // world X. The two rates are deliberately unequal: matched, the disc would
    // retrace one short loop forever, while a ratio like this one keeps it
    // wandering through every angle between flat on and edge on.
    private const float SpinRatio = 0.62f;

    private Vector3 _tumble;

    // How the disc lies in the prefab - face up - before Start sets it tumbling.
    private Quaternion _restRotation;

    void Awake()
    {
        _restRotation = transform.localRotation;
    }

    // Laid back face up and turned about the vertical: the first-time tour's
    // close-up, taken while the board - and the tumble with it - is stopped, and
    // a coin caught edge on would be a line rather than a coin.
    public void Present(float degrees)
    {
        transform.localRotation = Quaternion.Euler(0f, degrees, 0f) * _restRotation;
    }

    // The board is seen from straight above, so a coin turning about a single
    // axis only ever repeats one silhouette. Turning about two at once tumbles
    // it instead - the face comes round flat, on edge, and at every tilt in
    // between - which is what puts both sides of the minted disc in front of the
    // player. Each coin gets its own starting pose and its own pace, so twenty
    // of them read as scattered coins rather than one animation played twenty
    // times.
    void Start()
    {
        transform.rotation = Random.rotationUniform;
        float speed = rotationSpeed * Random.Range(0.85f, 1.15f);
        _tumble = new Vector3(speed, speed * SpinRatio, 0f);
    }

    void Update()
    {
        transform.Rotate(_tumble * Time.deltaTime, Space.World);
        // Every stop in the game - tour, pause, shop, level complete - holds
        // time at zero, and a coin must not be taken from under a frozen board.
        if (!_collected && Time.timeScale > 0f && PlayerOnTile()) Collect();
    }

    // Taking a coin is decided by the tile it lies on, not by touching the
    // disc. The disc is small and tumbles, so its collider could slip past a
    // player who walked across the tile off-centre - which read as a coin that
    // refused to be picked up. Tiles are one unit and the coin sits in the
    // middle of its tile, so the tile is everything within half a unit of it.
    private const float TileHalf = 0.5f;

    private static CharacterController _player;
    private static GhostScript _playerGhost;
    private static float _nextPlayerSearch;

    private bool PlayerOnTile()
    {
        if (_player == null && !FindPlayer()) return false;
        if (_playerGhost != null && _playerGhost.IsDead) return false;
        // The middle of the body, not the transform: the capsule sits a little
        // behind it.
        Vector3 body = _player.transform.TransformPoint(_player.center);
        Vector3 here = transform.position;
        return Mathf.Abs(body.x - here.x) < TileHalf && Mathf.Abs(body.z - here.z) < TileHalf;
    }

    // Found the way the other scripts find the player - the character
    // controller named Ghost - so the friendly ghost never takes coins. Shared
    // by every coin, and searched for at most twice a second while missing.
    private static bool FindPlayer()
    {
        if (Time.unscaledTime < _nextPlayerSearch) return false;
        _nextPlayerSearch = Time.unscaledTime + 0.5f;
        foreach (var ctrl in FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude))
        {
            if (ctrl.gameObject.name != "Ghost") continue;
            _player = ctrl;
            _playerGhost = ctrl.GetComponentInParent<GhostScript>();
            return true;
        }
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ghost")) Collect();
    }

    private void Collect()
    {
        if (_collected) return;
        _collected = true;
        Uncollected.Remove(this);

        GetComponent<Collider>().enabled = false;
        if (pickupSound != null && !AudioManager.SfxMuted)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        if (pickupEffect != null)
        {
            var fx = Instantiate(pickupEffect, transform.position, pickupEffect.transform.rotation);
            Destroy(fx, pickupEffectLifetime);
        }
        RewardSystem.CollectCoin();
        RunStats.CoinCollected();
        int value = RollWalletValue();
        if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(value);
        PlaytestLog.CoinCollected(value);
        StartCoroutine(PickupAnimation());
    }

    // Pays nothing rather than throwing if the list is emptied in the Inspector,
    // so a mis-set field costs the player their reward but not the run. Both
    // modes roll the same list.
    private int RollWalletValue() =>
        walletValues == null || walletValues.Length == 0
            ? 0
            : walletValues[Random.Range(0, walletValues.Length)];

    private IEnumerator PickupAnimation()
    {
        Transform root = transform.parent != null ? transform.parent : transform;
        Vector3 startScale = transform.localScale;
        Vector3 startPos = root.position;
        Vector3 startShadow = shadow != null ? shadow.localScale : Vector3.zero;
        float t = 0f;
        while (t < pickupDuration)
        {
            t += Time.deltaTime;
            float p = t / pickupDuration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
            root.position = startPos + Vector3.up * (p * 0.5f);
            if (shadow != null) shadow.localScale = Vector3.Lerp(startShadow, Vector3.zero, p);
            yield return null;
        }
        Destroy(root.gameObject);
    }
}
