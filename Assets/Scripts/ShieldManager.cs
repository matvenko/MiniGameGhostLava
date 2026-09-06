using UnityEngine;
using UnityEngine.UI;
using Sample;

// Owns the player's stock of shield charges (persisted like the trap, freeze
// and teleport stocks, so charges bought in one run survive into the next) and
// spends one to make the character untouchable for a few seconds.
//
// Untouchable means everything that kills: an enemy walking straight into the
// player is shrugged off, and so is the lava, so a shield is also a way
// through a board that has closed up. What it looks like belongs to the
// character rather than to this - ShieldBubble puts the bubble on, and pulses
// it faster as the time runs out so the player can feel the end coming.
//
// A charge can only be spent on a shield that would do something: pressing the
// button while one is already up does nothing rather than restarting a timer
// the player has most of anyway.
public class ShieldManager : MonoBehaviour
{
    public static ShieldManager Instance { get; private set; }

    private const string ShieldsOwnedKey = "shields_owned";

    [SerializeField] private Button useButton;
    [SerializeField] private GhostScript player;
    [Tooltip("How long the character stays untouchable, in seconds.")]
    [SerializeField] private float shieldDuration = 5f;

    public int ShieldsOwned { get; private set; }

    void Awake()
    {
        Instance = this;
        ShieldsOwned = PlayerPrefs.GetInt(ShieldsOwnedKey, 0);
        if (useButton != null) useButton.onClick.AddListener(UseShield);
    }

    void Start()
    {
        Refresh();
    }

    public void AddShields(int amount)
    {
        ShieldsOwned += amount;
        Save();
        Refresh();
    }

    public void UseShield()
    {
        if (ShieldsOwned <= 0 || player == null) return;
        if (player.ShieldActive) return;
        if (GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive) return;
        if (LevelManager.Instance != null && LevelManager.Instance.IsLevelCompleteActive) return;

        player.ActivateShield(shieldDuration);
        AudioManager.Play(GameSound.Shield);

        ShieldsOwned--;
        Save();
        Refresh();
    }

    private void Save()
    {
        PlayerPrefs.SetInt(ShieldsOwnedKey, ShieldsOwned);
        PlayerPrefs.Save();
    }

    public void Refresh()
    {
        // The bar owns the badge on the shield button, so the count goes to it
        // rather than into a label of our own.
        if (AbilityBarUI.Instance != null)
            AbilityBarUI.Instance.SetCount(AbilityBarUI.Ability.Shield, ShieldsOwned);
        if (useButton != null) useButton.interactable = ShieldsOwned > 0;
    }
}
