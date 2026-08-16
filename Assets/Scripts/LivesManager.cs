using UnityEngine;
using UnityEngine.UI;

// Tracks the player's remaining ghost-icon lives for the current run and
// drives the HUD row. Starts at 3; the shop can raise the owned max up to
// HardCap (6) via IncreaseMaxLives - never touches EconomyManager's
// persistent coin wallet.
public class LivesManager : MonoBehaviour
{
    public static LivesManager Instance { get; private set; }
    private const int StartingLives = 3;
    private const int HardCap = 6;

    [SerializeField] private Image[] ghostIcons; // sized for HardCap slots; only the first _ownedMaxLives are shown
    [SerializeField] private Sprite ghostFullSprite;
    [SerializeField] private Sprite ghostEmptySprite;
    [SerializeField] private float lostLifePunchScale = 1.4f;
    [SerializeField] private float lostLifePunchDuration = 0.25f;

    private int _ownedMaxLives = StartingLives;

    public int CurrentLives { get; private set; }

    void Awake()
    {
        Instance = this;
        _ownedMaxLives = StartingLives;
        CurrentLives = _ownedMaxLives;
        UpdateIcons(-1);
    }

    // Removes one life and refreshes the icons. Returns true when this was
    // the last life - the caller should trigger the full Game Over screen
    // instead of a quick respawn.
    public bool LoseLife()
    {
        int lostIndex = Mathf.Max(0, CurrentLives - 1);
        CurrentLives = Mathf.Max(0, CurrentLives - 1);
        UpdateIcons(lostIndex);
        return CurrentLives <= 0;
    }

    // Called after a rewarded-ad continue: grants exactly one life so play
    // resumes, rather than refilling all the way back to full.
    public void GrantExtraLife()
    {
        CurrentLives = Mathf.Min(_ownedMaxLives, 1);
        UpdateIcons(-1);
    }

    // Shop hook: raises the owned max life count (up to HardCap) and grants
    // one immediately. Safe to call once the cap is already reached - it's
    // just a no-op then.
    public void IncreaseMaxLives(int amount = 1)
    {
        int newMax = Mathf.Min(HardCap, _ownedMaxLives + amount);
        int gained = newMax - _ownedMaxLives;
        _ownedMaxLives = newMax;
        CurrentLives = Mathf.Min(_ownedMaxLives, CurrentLives + gained);
        UpdateIcons(-1);
    }

    public void ResetLives()
    {
        _ownedMaxLives = StartingLives;
        CurrentLives = _ownedMaxLives;
        UpdateIcons(-1);
    }

    private void UpdateIcons(int punchIndex)
    {
        if (ghostIcons == null) return;
        for (int i = 0; i < ghostIcons.Length; i++)
        {
            if (ghostIcons[i] == null) continue;

            bool owned = i < _ownedMaxLives;
            ghostIcons[i].gameObject.SetActive(owned);
            if (!owned) continue;

            ghostIcons[i].sprite = i < CurrentLives ? ghostFullSprite : ghostEmptySprite;
            if (i == punchIndex) StartCoroutine(PunchScale(ghostIcons[i].transform));
        }
    }

    private System.Collections.IEnumerator PunchScale(Transform t)
    {
        float half = lostLifePunchDuration / 2f;
        float time = 0f;
        while (time < half)
        {
            time += Time.deltaTime;
            t.localScale = Vector3.one * Mathf.Lerp(1f, lostLifePunchScale, time / half);
            yield return null;
        }
        time = 0f;
        while (time < half)
        {
            time += Time.deltaTime;
            t.localScale = Vector3.one * Mathf.Lerp(lostLifePunchScale, 1f, time / half);
            yield return null;
        }
        t.localScale = Vector3.one;
    }
}
