using UnityEngine;

// A consumable top-up: buying "Extra Life" restores one life immediately
// (capped at LivesManager.HardCap), priced by how far the player has
// progressed - placeholder tiers, to be tuned later. Not a permanent
// unlock, so it stays useful all game and can be repurchased any time
// after losing lives mid-run.
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    private const int EarlyLevelThreshold = 5;
    private const int EarlyLevelCost = 1000;
    private const int LateLevelCost = 2000;

    void Awake()
    {
        Instance = this;
    }

    public int GetExtraLifeCost()
    {
        int level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : 1;
        return level <= EarlyLevelThreshold ? EarlyLevelCost : LateLevelCost;
    }

    public bool IsExtraLifeMaxed()
    {
        return LivesManager.Instance != null && LivesManager.Instance.CurrentLives >= LivesManager.HardCap;
    }

    public bool CanBuyExtraLife()
    {
        if (IsExtraLifeMaxed()) return false;
        return EconomyManager.Instance != null && EconomyManager.Instance.TotalCoins >= GetExtraLifeCost();
    }

    // Returns true if the purchase succeeded.
    public bool BuyExtraLife()
    {
        if (!CanBuyExtraLife()) return false;
        if (!EconomyManager.Instance.SpendCoins(GetExtraLifeCost())) return false;

        return LivesManager.Instance != null && LivesManager.Instance.AddLife();
    }
}
