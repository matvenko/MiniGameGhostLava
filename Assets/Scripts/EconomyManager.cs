using UnityEngine;
using TMPro;

// Persistent coin wallet, separate from RewardSystem's per-level "X / Y
// collected" objective counter. Survives level transitions and individual
// deaths via RunProgress, which keeps one wallet per difficulty. Nothing takes
// coins back off the player any more: running out of lives costs an ad to come
// back, not the wallet.
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI walletText;
    // The purple pill the moonshard balance is drawn on (see MoonshardHud).
    [SerializeField] private Sprite crystalBar;

    public int TotalCoins { get; private set; }
    public int TotalMoonshards { get; private set; }

    void Awake()
    {
        Instance = this;
        TotalCoins = RunProgress.Coins;
        TotalMoonshards = RunProgress.Moonshards;
        UpdateText();
        MoonshardHud.Create(this, crystalBar, walletText);
    }

    public void AddCoins(int amount)
    {
        TotalCoins += amount;
        RunProgress.Coins = TotalCoins;
        UpdateText();
    }

    public void AddMoonshard()
    {
        TotalMoonshards++;
        RunProgress.Moonshards = TotalMoonshards;
        MoonshardHud.Refresh(this);
    }

    // Returns false without spending anything if the wallet can't cover it.
    public bool SpendCoins(int amount)
    {
        if (amount > TotalCoins) return false;
        TotalCoins -= amount;
        RunProgress.Coins = TotalCoins;
        UpdateText();
        return true;
    }

    private void UpdateText()
    {
        if (walletText != null) walletText.text = TotalCoins.ToString();
    }
}
