using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Drives the Shop popup opened from the pause menu: refreshes the wallet
// balance and the Extra Life item's price/owned/buy-state, and applies a
// purchase through ShopManager when Buy is clicked.
public class ShopUIController : MonoBehaviour
{
    public static ShopUIController Instance { get; private set; }
    public bool IsOpen => shopPanel != null && shopPanel.activeSelf;

    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject pausePanel; // hidden while the shop is open so its dim backdrop doesn't stack/bleed through
    [SerializeField] private GameObject[] hudElementsToHide; // top HUD (coins/wallet/hearts) - hidden outright rather than trusted to the backdrop alone
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button buyExtraLifeButton;
    [SerializeField] private TextMeshProUGUI buyExtraLifeButtonText;
    [SerializeField] private TextMeshProUGUI extraLifeStatusText;
    [SerializeField] private Button buyTrapButton;
    [SerializeField] private TextMeshProUGUI buyTrapButtonText;
    [SerializeField] private TextMeshProUGUI trapStatusText;
    [SerializeField] private TextMeshProUGUI walletText;

    void Awake()
    {
        Instance = this;
        if (shopPanel != null) shopPanel.SetActive(false);
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (buyExtraLifeButton != null) buyExtraLifeButton.onClick.AddListener(OnBuyExtraLifeClicked);
        if (buyTrapButton != null) buyTrapButton.onClick.AddListener(OnBuyTrapClicked);
    }

    private void Open()
    {
        if (shopPanel != null) shopPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        SetHudVisible(false);
        Refresh();
    }

    private void Close()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);
        SetHudVisible(true);
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

    private void Refresh()
    {
        if (walletText != null && EconomyManager.Instance != null)
            walletText.text = EconomyManager.Instance.TotalCoins.ToString();

        if (ShopManager.Instance == null) return;

        bool maxed = ShopManager.Instance.IsExtraLifeMaxed();
        if (extraLifeStatusText != null && LivesManager.Instance != null)
            extraLifeStatusText.text = "Lives: " + LivesManager.Instance.CurrentLives + "/" + LivesManager.HardCap;

        if (buyExtraLifeButtonText != null)
            buyExtraLifeButtonText.text = maxed ? "MAXED" : ShopManager.Instance.GetExtraLifeCost().ToString();

        if (buyExtraLifeButton != null)
            buyExtraLifeButton.interactable = !maxed && ShopManager.Instance.CanBuyExtraLife();

        if (trapStatusText != null && TrapManager.Instance != null)
            trapStatusText.text = "Owned: " + TrapManager.Instance.TrapsOwned;

        if (buyTrapButtonText != null)
            buyTrapButtonText.text = ShopManager.Instance.GetTrapCost().ToString();

        if (buyTrapButton != null)
            buyTrapButton.interactable = ShopManager.Instance.CanBuyTrap();
    }
}
