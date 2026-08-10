using System.Collections;
using UnityEngine;
using TMPro;

// Plays a big centered "3... 2... 1..." countdown, timed to match the
// portal warning duration, so that warm-up period before enemies appear
// reads as a deliberate beat instead of a silent pause.
public class SpawnCountdownController : MonoBehaviour
{
    public static SpawnCountdownController Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private float punchInDuration = 0.15f;
    [SerializeField] private float punchOutDuration = 0.25f;

    void Awake()
    {
        Instance = this;
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    public void PlayCountdown(float duration)
    {
        if (countdownText == null) return;
        StopAllCoroutines();
        StartCoroutine(CountdownRoutine(duration));
    }

    private IEnumerator CountdownRoutine(float duration)
    {
        int startNumber = Mathf.Max(1, Mathf.CeilToInt(duration));
        countdownText.gameObject.SetActive(true);

        for (int i = startNumber; i >= 1; i--)
        {
            countdownText.text = i.ToString();
            yield return StartCoroutine(PulseOnce());
        }

        countdownText.gameObject.SetActive(false);
    }

    // one "beat": pops in oversized+transparent, settles to full size and
    // opacity, holds, then pops out slightly bigger while fading - reads as
    // a pulse rather than a flat number swap.
    private IEnumerator PulseOnce()
    {
        float holdDuration = Mathf.Max(0f, 1f - punchInDuration - punchOutDuration);

        float t = 0f;
        while (t < punchInDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / punchInDuration);
            float ease = 1f - (1f - p) * (1f - p);
            countdownText.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, 1f, ease);
            SetAlpha(Mathf.Lerp(0f, 1f, ease));
            yield return null;
        }
        countdownText.transform.localScale = Vector3.one;
        SetAlpha(1f);

        yield return new WaitForSeconds(holdDuration);

        t = 0f;
        while (t < punchOutDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / punchOutDuration);
            countdownText.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.35f, p);
            SetAlpha(Mathf.Lerp(1f, 0f, p));
            yield return null;
        }
    }

    private void SetAlpha(float a)
    {
        Color c = countdownText.color;
        c.a = a;
        countdownText.color = c;
    }
}
