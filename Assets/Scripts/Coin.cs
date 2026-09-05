using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private int[] walletValues = { 50, 100, 150, 200 };

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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected || !other.CompareTag("Ghost")) return;
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
        if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(RollWalletValue());
        StartCoroutine(PickupAnimation());
    }

    // Pays nothing rather than throwing if the list is emptied in the Inspector,
    // so a mis-set field costs the player their reward but not the run.
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
