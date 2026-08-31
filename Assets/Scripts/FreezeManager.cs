using UnityEngine;
using UnityEngine.UI;

// Owns the player's stock of freeze charges (persisted like the trap stock and
// the coin wallet, so charges bought in one run survive into the next) and
// spends one to stop every enemy on the board at once.
//
// A freeze is the same stun a trap applies, only to all of them and without
// having to be walked onto - so an enemy about to corner the player can be
// stopped where it stands. What it looks like belongs to the stun rather than
// to this: every stunned enemy holds its pose and is encased in ice by
// FreezeVisual, whether a trap or this ability stopped it.
//
// A charge is only spent when there is something to spend it on. During the
// portal warm-up the enemies are still inactive, so pressing the button then
// does nothing rather than burning a charge on an empty board.
public class FreezeManager : MonoBehaviour
{
    public static FreezeManager Instance { get; private set; }

    private const string FreezesOwnedKey = "freezes_owned";

    [SerializeField] private Button useButton;
    [Tooltip("How long every enemy stays stopped, in seconds.")]
    [SerializeField] private float freezeDuration = 5f;

    public int FreezesOwned { get; private set; }

    void Awake()
    {
        Instance = this;
        FreezesOwned = PlayerPrefs.GetInt(FreezesOwnedKey, 0);
        if (useButton != null) useButton.onClick.AddListener(UseFreeze);
    }

    void Start()
    {
        Refresh();
    }

    public void AddFreezes(int amount)
    {
        FreezesOwned += amount;
        Save();
        Refresh();
    }

    public void UseFreeze()
    {
        if (FreezesOwned <= 0) return;
        if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive) return;

        if (EnemyChaser.StunAll(freezeDuration) == 0) return;

        FreezesOwned--;
        Save();
        Refresh();
    }

    private void Save()
    {
        PlayerPrefs.SetInt(FreezesOwnedKey, FreezesOwned);
        PlayerPrefs.Save();
    }

    public void Refresh()
    {
        // The bar owns the badge on the freeze button, so the count goes to it
        // rather than into a label of our own.
        if (AbilityBarUI.Instance != null)
            AbilityBarUI.Instance.SetCount(AbilityBarUI.Ability.Freeze, FreezesOwned);
        if (useButton != null) useButton.interactable = FreezesOwned > 0;
    }
}
