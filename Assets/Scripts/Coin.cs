using System.Collections;
using UnityEngine;

public class Coin : MonoBehaviour
{
    private float rotationSpeed = 200f;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private float pickupDuration = 0.3f;
    [SerializeField] private int walletValue = 50;

    private bool _collected;

    void Update()
    {
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected || !other.CompareTag("Ghost")) return;
        _collected = true;

        GetComponent<Collider>().enabled = false;
        if (pickupSound != null && !AudioManager.SfxMuted)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        RewardSystem.CollectCoin();
        // walletValue is the hard-mode payout; easy mode pays a flat, much
        // larger amount per coin so younger players still reach the shop.
        if (EconomyManager.Instance != null) EconomyManager.Instance.AddCoins(DifficultySettings.CoinWalletValue(walletValue));
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
