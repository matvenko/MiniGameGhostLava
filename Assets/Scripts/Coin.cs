using System.Collections;
using UnityEngine;

public class Coin : MonoBehaviour
{
    private float rotationSpeed = 200f;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float pickupDuration = 0.3f;
    [SerializeField] private int walletValue = 50;

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

        GetComponent<Collider>().enabled = false;
        if (pickupSound != null && !AudioManager.SfxMuted)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        RewardSystem.CollectCoin();
        if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(walletValue);
        StartCoroutine(PickupAnimation());
    }

    private IEnumerator PickupAnimation()
    {
        Transform root = transform.parent != null ? transform.parent : transform;
        Vector3 startScale = transform.localScale;
        Vector3 startPos = root.position;
        float t = 0f;
        while (t < pickupDuration)
        {
            t += Time.deltaTime;
            float p = t / pickupDuration;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
            root.position = startPos + Vector3.up * (p * 0.5f);
            yield return null;
        }
        Destroy(root.gameObject);
    }
}
