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

    private const int TrapCost = 400;
    private const int TrapsPerPurchase = 1;

    private const int FreezeCost = 1500;
    private const int FreezesPerPurchase = 1;

    private const int TeleportCost = 2500;
    private const int TeleportsPerPurchase = 1;

    private const int ShieldCost = 1000;
    private const int ShieldsPerPurchase = 1;

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

    // Traps have no cap - they're pure stock, and unspent ones carry over
    // into the next run.
    public int GetTrapCost()
    {
        return TrapCost;
    }

    public bool CanBuyTrap()
    {
        return EconomyManager.Instance != null && EconomyManager.Instance.TotalCoins >= TrapCost;
    }

    public bool BuyTrap()
    {
        if (!CanBuyTrap()) return false;
        if (TrapManager.Instance == null) return false;
        if (!EconomyManager.Instance.SpendCoins(TrapCost)) return false;

        TrapManager.Instance.AddTraps(TrapsPerPurchase);
        return true;
    }

    // Freeze charges are stock like traps - no cap, carried between runs, and
    // priced well above a trap since one stops the whole board rather than
    // whichever enemy happens to walk onto a tile.
    public int GetFreezeCost()
    {
        return FreezeCost;
    }

    public bool CanBuyFreeze()
    {
        return EconomyManager.Instance != null && EconomyManager.Instance.TotalCoins >= FreezeCost;
    }

    public bool BuyFreeze()
    {
        if (!CanBuyFreeze()) return false;
        if (FreezeManager.Instance == null) return false;
        if (!EconomyManager.Instance.SpendCoins(FreezeCost)) return false;

        FreezeManager.Instance.AddFreezes(FreezesPerPurchase);
        return true;
    }

    // Teleport charges are stock like the other two, and the dearest of them:
    // a freeze buys five seconds of standing still, a teleport takes the player
    // out of the corner it was about to be caught in entirely.
    public int GetTeleportCost()
    {
        return TeleportCost;
    }

    public bool CanBuyTeleport()
    {
        return EconomyManager.Instance != null && EconomyManager.Instance.TotalCoins >= TeleportCost;
    }

    public bool BuyTeleport()
    {
        if (!CanBuyTeleport()) return false;
        if (TeleportManager.Instance == null) return false;
        if (!EconomyManager.Instance.SpendCoins(TeleportCost)) return false;

        TeleportManager.Instance.AddTeleports(TeleportsPerPurchase);
        return true;
    }

    // The cheapest of the four charges, and the one that asks the most of the
    // player: it buys seconds rather than a way out, and those seconds are
    // only worth anything if they are spent walking somewhere.
    public int GetShieldCost()
    {
        return ShieldCost;
    }

    public bool CanBuyShield()
    {
        return EconomyManager.Instance != null && EconomyManager.Instance.TotalCoins >= ShieldCost;
    }

    public bool BuyShield()
    {
        if (!CanBuyShield()) return false;
        if (ShieldManager.Instance == null) return false;
        if (!EconomyManager.Instance.SpendCoins(ShieldCost)) return false;

        ShieldManager.Instance.AddShields(ShieldsPerPurchase);
        return true;
    }
}
