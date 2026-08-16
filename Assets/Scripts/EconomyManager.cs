using UnityEngine;
using TMPro;

// Persistent coin wallet, separate from RewardSystem's per-level "X / Y
// collected" objective counter. Survives level transitions and individual
// deaths via PlayerPrefs; only running out of lives touches it, and even
// then it just halves (see HalveOnDefeat) rather than wiping.
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    private const string WalletKey = "wallet_coins";

    [SerializeField] private TextMeshProUGUI walletText;

    public int TotalCoins { get; private set; }

    void Awake()
    {
        Instance = this;
        TotalCoins = PlayerPrefs.GetInt(WalletKey, 0);
        UpdateText();
    }

    public void AddCoins(int amount)
    {
        TotalCoins += amount;
        PlayerPrefs.SetInt(WalletKey, TotalCoins);
        PlayerPrefs.Save();
        UpdateText();
    }

    // Returns false without spending anything if the wallet can't cover it.
    public bool SpendCoins(int amount)
    {
        if (amount > TotalCoins) return false;
        TotalCoins -= amount;
        PlayerPrefs.SetInt(WalletKey, TotalCoins);
        PlayerPrefs.Save();
        UpdateText();
        return true;
    }

    // What abandoning the run would cost, so the Game Over screen can warn
    // before the player commits to it.
    public int CoinsLostOnDefeat => TotalCoins - Mathf.CeilToInt(TotalCoins / 2f);

    // Abandoning the run costs half the wallet, rounded in the player's
    // favour. Returns how many coins were taken.
    public int HalveOnDefeat()
    {
        int kept = Mathf.CeilToInt(TotalCoins / 2f);
        int lost = TotalCoins - kept;
        TotalCoins = kept;
        PlayerPrefs.SetInt(WalletKey, TotalCoins);
        PlayerPrefs.Save();
        UpdateText();
        return lost;
    }

    private void UpdateText()
    {
        if (walletText != null) walletText.text = TotalCoins.ToString();
    }
}
