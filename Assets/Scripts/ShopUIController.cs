using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Drives the Shop popup: refreshes the wallet balance and each item's
// price/owned/buy-state - extra life, traps, freeze, teleport and shield
// charges - and applies a purchase through ShopManager when that item's Buy
// is clicked.
//
// There are two ways in and closing has to undo whichever it was. From the pause
// menu the game is already stopped and the pause card is only hidden, so closing
// puts that card back and leaves time stopped. From the button on the HUD the
// game is still running, so opening stops it and closing starts it again -
// otherwise the ghosts keep hunting a player who is reading prices.
public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Instance { get; private set; }
    public bool IsOpen => shopPanel != null && shopPanel.activeSelf;

    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject pausePanel; // hidden while the shop is open so its dim backdrop doesn't stack/bleed through
    [SerializeField] private GameObject[] hudElementsToHide; // top HUD (coins/wallet/hearts) - hidden outright rather than trusted to the backdrop alone
    [SerializeField] private Button openButton;      // on the pause card
    [SerializeField] private Button hudOpenButton;   // in the corner of the HUD, during play
    [SerializeField] private Button closeButton;
    [SerializeField] private Button buyExtraLifeButton;
    [SerializeField] private TextMeshProUGUI buyExtraLifeButtonText;
    [SerializeField] private TextMeshProUGUI extraLifeStatusText;
    [SerializeField] private Button buyTrapButton;
    [SerializeField] private TextMeshProUGUI buyTrapButtonText;
    [SerializeField] private TextMeshProUGUI trapStatusText;
    [SerializeField] private Button buyFreezeButton;
    [SerializeField] private TextMeshProUGUI buyFreezeButtonText;
    [SerializeField] private TextMeshProUGUI freezeStatusText;
    [SerializeField] private Button buyTeleportButton;
    [SerializeField] private TextMeshProUGUI buyTeleportButtonText;
    [SerializeField] private TextMeshProUGUI teleportStatusText;
    [SerializeField] private Button buyShieldButton;
    [SerializeField] private TextMeshProUGUI buyShieldButtonText;
    [SerializeField] private TextMeshProUGUI shieldStatusText;
    [SerializeField] private TextMeshProUGUI walletText;

    void Awake()
    {
        Instance = this;
        if (shopPanel != null) shopPanel.SetActive(false);
        if (openButton != null) openButton.onClick.AddListener(OpenFromPause);
        if (hudOpenButton != null) hudOpenButton.onClick.AddListener(OpenFromHud);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (buyExtraLifeButton != null) buyExtraLifeButton.onClick.AddListener(OnBuyExtraLifeClicked);
        if (buyTrapButton != null) buyTrapButton.onClick.AddListener(OnBuyTrapClicked);
        if (buyFreezeButton != null) buyFreezeButton.onClick.AddListener(OnBuyFreezeClicked);
        if (buyTeleportButton != null) buyTeleportButton.onClick.AddListener(OnBuyTeleportClicked);
        if (buyShieldButton != null) buyShieldButton.onClick.AddListener(OnBuyShieldClicked);
    }

    private bool _openedFromPause;

    private void OpenFromPause()
    {
        _openedFromPause = true;
        if (pausePanel != null) pausePanel.SetActive(false);
        Open();
    }

    private void OpenFromHud()
    {
        // Nothing else is stopping the game on this route, so the shop does.
        // Anything that already owns the screen has its own popup over the HUD
        // button, so there is no state to check first.
        _openedFromPause = false;
        Time.timeScale = 0f;
        Open();
    }

    private void Open()
    {
        if (shopPanel != null) shopPanel.SetActive(true);
        SetHudVisible(false);
        SetCountdownCovered(true);
        Refresh();
    }

    private void Close()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (_openedFromPause)
        {
            if (pausePanel != null) pausePanel.SetActive(true);
        }
        else
        {
            Time.timeScale = 1f;
        }
        SetHudVisible(true);
        // Closing back into the pause menu is not being back on the board: the
        // countdown stays off until that closes too.
        SetCountdownCovered(_openedFromPause);
    }

    // Opened during the warm-up beat at the start of a level, the shop would have
    // the countdown sitting frozen behind it.
    private static void SetCountdownCovered(bool covered)
    {
        if (SpawnCountdownController.Instance != null) SpawnCountdownController.Instance.SetCovered(covered);
    }

    private void SetHudVisible(bool visible)
    {
        if (hudElementsToHide == null) return;
        foreach (var go in hudElementsToHide)
        {
            if (go != null) go.SetActive(visible);
        }
    }

    private void OnBuyExtraLifeClicked()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.BuyExtraLife();
        Refresh();
    }

    private void OnBuyTrapClicked()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.BuyTrap();
        Refresh();
    }

    private void OnBuyFreezeClicked()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.BuyFreeze();
        Refresh();
    }

    private void OnBuyTeleportClicked()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.BuyTeleport();
        Refresh();
    }

    private void OnBuyShieldClicked()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.BuyShield();
        Refresh();
    }

    // The shop card puts the number in gold and the word in front of it in the
    // panel's quiet blue, so the count reads at a glance down the column.
    private static string Count(object value) => "<color=#FFD34A>" + value + "</color>";

    // Public so anything that changes the wallet behind the shop's back - the
    // test bar paying coins in while it is open - can have the prices and the
    // buy buttons say so without the shop being closed and reopened.
    public void Refresh()
    {
        if (walletText != null && EconomyManager.Instance != null)
            walletText.text = EconomyManager.Instance.TotalCoins.ToString();

        if (ShopManager.Instance == null) return;

        bool maxed = ShopManager.Instance.IsExtraLifeMaxed();
        if (extraLifeStatusText != null && LivesManager.Instance != null)
            extraLifeStatusText.text = "Lives: " + Count(LivesManager.Instance.CurrentLives + "/" + LivesManager.HardCap);

        if (buyExtraLifeButtonText != null)
            buyExtraLifeButtonText.text = maxed ? "MAXED" : ShopManager.Instance.GetExtraLifeCost().ToString();

        if (buyExtraLifeButton != null)
            buyExtraLifeButton.interactable = !maxed && ShopManager.Instance.CanBuyExtraLife();

        if (trapStatusText != null && TrapManager.Instance != null)
            trapStatusText.text = "Owned: " + Count(TrapManager.Instance.TrapsOwned);

        if (buyTrapButtonText != null)
            buyTrapButtonText.text = ShopManager.Instance.GetTrapCost().ToString();

        if (buyTrapButton != null)
            buyTrapButton.interactable = ShopManager.Instance.CanBuyTrap();

        if (freezeStatusText != null && FreezeManager.Instance != null)
            freezeStatusText.text = "Owned: " + Count(FreezeManager.Instance.FreezesOwned);

        if (buyFreezeButtonText != null)
            buyFreezeButtonText.text = ShopManager.Instance.GetFreezeCost().ToString();

        if (buyFreezeButton != null)
            buyFreezeButton.interactable = ShopManager.Instance.CanBuyFreeze();

        if (teleportStatusText != null && TeleportManager.Instance != null)
            teleportStatusText.text = "Owned: " + Count(TeleportManager.Instance.TeleportsOwned);

        if (buyTeleportButtonText != null)
            buyTeleportButtonText.text = ShopManager.Instance.GetTeleportCost().ToString();

        if (buyTeleportButton != null)
            buyTeleportButton.interactable = ShopManager.Instance.CanBuyTeleport();

        if (shieldStatusText != null && ShieldManager.Instance != null)
            shieldStatusText.text = "Owned: " + Count(ShieldManager.Instance.ShieldsOwned);

        if (buyShieldButtonText != null)
            buyShieldButtonText.text = ShopManager.Instance.GetShieldCost().ToString();

        if (buyShieldButton != null)
            buyShieldButton.interactable = ShopManager.Instance.CanBuyShield();
    }
}
