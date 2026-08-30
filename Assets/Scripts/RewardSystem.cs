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
        if (Instance.collectedCoins >= Instance.totalCoins && LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelComplete();
        }
    }

    // Takes the exact count LevelManager just spawned rather than re-scanning
    // the scene: a just-collected coin's pickup animation can still be alive
    // (frozen mid-shrink while Time.timeScale is 0 for the level-complete
    // popup) when this runs, so FindObjectsByType would over-count it until
    // its deferred Destroy() actually lands.
    public void ResetForNewLevel(int newTotalCoins)
    {
        totalCoins = newTotalCoins;
        collectedCoins = 0;
        UpdateText();
    }

    // One number: what is still out on the board, counting down to zero. The
    // counter used to read "collected/total", which takes a second of arithmetic
    // to answer the only question being asked of it - how much is left.
    private void UpdateText()
    {
        if (coinsAmountText != null)
            coinsAmountText.text = Mathf.Max(0, totalCoins - collectedCoins).ToString();
    }
}
