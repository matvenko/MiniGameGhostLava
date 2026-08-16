using UnityEngine;
using TMPro;

// Persistent coin wallet, separate from RewardSystem's per-level "X / Y
// collected" objective counter. Survives level transitions, deaths, and
// full game restarts via PlayerPrefs - nothing in the game currently
// resets it, by design (see game design plan: losing should never wipe
// shop currency).
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

    private void UpdateText()
    {
        if (walletText != null) walletText.text = TotalCoins.ToString();
    }
}
