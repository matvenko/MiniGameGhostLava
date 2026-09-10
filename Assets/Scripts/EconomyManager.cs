using UnityEngine;
using TMPro;

// Persistent coin wallet, separate from RewardSystem's per-level "X / Y
// collected" objective counter. Survives level transitions and individual
// deaths via RunProgress, which keeps one wallet per difficulty. Nothing takes
// coins back off the player any more: running out of lives costs a life and an
// ad to come back, not the wallet (see RunProgress.LeaveRun).
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI walletText;

    public int TotalCoins { get; private set; }

    void Awake()
    {
        Instance = this;
        TotalCoins = RunProgress.Coins;
        UpdateText();
    }

    public void AddCoins(int amount)
    {
        TotalCoins += amount;
        RunProgress.Coins = TotalCoins;
        UpdateText();
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
