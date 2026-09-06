using UnityEngine;
using UnityEngine.UI;

// Tracks the player's remaining ghost-icon lives for the current run and
// drives the HUD row. Starts at 3 each run, or 4 in normal mode (see
// DifficultySettings); the shop can top lives back up to HardCap (6) as a
// consumable purchase (see AddLife) - never touches EconomyManager's
// persistent coin wallet.
public class LivesManager : MonoBehaviour
{
    public static LivesManager Instance { get; private set; }
    private const int AuthoredStartingLives = 3;
    private static int StartingLives => DifficultySettings.StartingLives(AuthoredStartingLives);
    public const int HardCap = 6;

    [SerializeField] private Image[] ghostIcons; // HardCap slots; only the living ones are shown
    [SerializeField] private Sprite ghostFullSprite;
    // resized to fit the visible icons so the pill never leaves dead space;
    // its pivot is on the right edge, so it shrinks leftwards and stays put
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private float panelPadding = 12f;
    [SerializeField] private float iconSize = 32f;
    [SerializeField] private float iconSpacing = 6.8f;
    [SerializeField] private float lostLifePunchScale = 1.4f;
    [SerializeField] private float lostLifePunchDuration = 0.25f;

    public int CurrentLives { get; private set; }

    void Awake()
    {
        Instance = this;
        CurrentLives = StartingLives;
        UpdateIcons(-1);
    }

    // Removes one life and refreshes the icons. Returns true when this was
    // the last life - the caller should trigger the full Game Over screen
    // instead of a quick respawn.
    public bool LoseLife()
    {
        CurrentLives = Mathf.Max(0, CurrentLives - 1);
        // punch the icon that's now last in the row - the one that vanished
        // is already gone, so the feedback lands on what's still there
        UpdateIcons(CurrentLives - 1);
        return CurrentLives <= 0;
    }

    // Called after a rewarded-ad continue: grants exactly one life so play
    // resumes, rather than refilling all the way back to full.
    public void GrantExtraLife()
    {
        CurrentLives = 1;
        UpdateIcons(-1);
    }

    // Shop hook: a consumable top-up, +1 life up to HardCap. Repurchasable
    // any time you're below the cap, including mid-run after losing lives.
    // Returns false if already at the cap (nothing to buy).
    public bool AddLife()
    {
        if (CurrentLives >= HardCap) return false;
        CurrentLives++;
        UpdateIcons(-1);
        return true;
    }

    public void ResetLives()
    {
        CurrentLives = StartingLives;
        UpdateIcons(-1);
    }

    // Only as many icons as lives remaining are shown - spent slots are
    // hidden outright rather than left as empty outlines.
    private void UpdateIcons(int punchIndex)
    {
        if (ghostIcons == null) return;
        for (int i = 0; i < ghostIcons.Length; i++)
        {
            if (ghostIcons[i] == null) continue;

            bool alive = i < CurrentLives;
            ghostIcons[i].gameObject.SetActive(alive);
            if (!alive) continue;

            ghostIcons[i].sprite = ghostFullSprite;
            if (i == punchIndex) StartCoroutine(PunchScale(ghostIcons[i].transform));
        }

        ResizePanel();
    }

    private void ResizePanel()
    {
        if (panelRect == null) return;
        int shown = Mathf.Max(1, CurrentLives);
        float width = panelPadding * 2f + shown * iconSize + (shown - 1) * iconSpacing;
        panelRect.sizeDelta = new Vector2(width, panelRect.sizeDelta.y);
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
