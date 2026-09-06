using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Plays a big centered "3... 2... 1..." countdown, timed to match the
// portal warning duration, so that warm-up period before enemies appear
// reads as a deliberate beat instead of a silent pause.
//
// It is the only thing on the screen that is mid-animation when a popup opens,
// so it is also the only thing that has to be told to get out of the way. The
// settings popup and the shop both say so on their way up and take it back on
// their way down: the beat itself is not cancelled, only taken off the screen,
// and since both of them stop time as well it is frozen where it was and carries
// on from there.
public class SpawnCountdownController : MonoBehaviour
{
    public static SpawnCountdownController Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI countdownText;
    // soft dark circle behind the number so its gold color still reads
    // against bright lava/floor tiles instead of blending into them
    [SerializeField] private Image countdownBackdrop;
    [SerializeField] private float backdropMaxAlpha = 0.55f;
    [SerializeField] private float punchInDuration = 0.15f;
    [SerializeField] private float punchOutDuration = 0.25f;

    // Whether a countdown is running, and whether something has the screen in
    // front of it. It shows only when both agree.
    private bool _counting;
    private bool _covered;

    void Awake()
    {
        Instance = this;
        Show();
    }

    public void PlayCountdown(float duration)
    {
        if (countdownText == null) return;
        StopAllCoroutines();
        StartCoroutine(CountdownRoutine(duration));
    }

    // called by GameOverManager the instant the player dies, so a
    // countdown caught mid-pulse doesn't keep animating over the death
    // sequence
    public void StopAndHide()
    {
        StopAllCoroutines();
        _counting = false;
        Show();
    }

    // Called by whatever is putting a popup over the board. The countdown keeps
    // its place: this only decides whether it can be seen.
    public void SetCovered(bool covered)
    {
        _covered = covered;
        Show();
    }

    private void Show()
    {
        bool visible = _counting && !_covered;
        if (countdownText != null) countdownText.gameObject.SetActive(visible);
        if (countdownBackdrop != null) countdownBackdrop.gameObject.SetActive(visible);
    }

    private IEnumerator CountdownRoutine(float duration)
    {
        int startNumber = Mathf.Max(1, Mathf.CeilToInt(duration));
        _counting = true;
        Show();

        for (int i = startNumber; i >= 1; i--)
        {
            countdownText.text = i.ToString();
            AudioManager.Play(GameSound.Ready);
            yield return StartCoroutine(PulseOnce());
        }

        _counting = false;
        Show();
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

        if (countdownBackdrop != null)
        {
            Color bc = countdownBackdrop.color;
            bc.a = a * backdropMaxAlpha;
            countdownBackdrop.color = bc;
        }
    }
}
