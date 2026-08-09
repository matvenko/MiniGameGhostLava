using UnityEngine;
using TMPro;

public class RewardSystem : MonoBehaviour
{
    public static RewardSystem Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI coinsAmountText;

    private int totalCoins;
    private int collectedCoins;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        totalCoins = FindObjectsByType<Coin>(FindObjectsSortMode.None).Length;
        collectedCoins = 0;
        UpdateText();
    }

    public static void CollectCoin()
    {
        if (Instance == null) return;
        Instance.collectedCoins++;
        Instance.UpdateText();
        if (Instance.collectedCoins >= Instance.totalCoins)
        {
            Debug.Log("Level Complete!");
        }
    }

    private void UpdateText()
    {
        if (coinsAmountText != null)
            coinsAmountText.text = collectedCoins + " / " + totalCoins;
    }
}
