using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Sample;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }
    public bool IsGameOverActive { get; private set; }

    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI subText;
    [SerializeField] private Button watchAdButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private float cameraZoomDuration = 1f;
    [SerializeField] private Vector3 closeUpOffset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private float invincibilityDuration = 1.5f;
    [SerializeField] private float watchAdMockDuration = 2f;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private GhostScript _ghost;

    void Awake()
    {
        Instance = this;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (watchAdButton != null) watchAdButton.onClick.AddListener(OnWatchAdClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    // Called by GhostScript.Die() on every death. A life still remaining
    // means a quick, low-friction respawn; hitting zero triggers the full
    // dramatic sequence and the Game Over screen.
    public void TriggerGameOver(GhostScript ghost)
    {
        _ghost = ghost;
        if (SpawnCountdownController.Instance != null) SpawnCountdownController.Instance.StopAndHide();

        bool outOfLives = LivesManager.Instance == null || LivesManager.Instance.LoseLife();
        if (outOfLives)
        {
            IsGameOverActive = true;
            StartCoroutine(FinalGameOverSequence());
        }
        else
        {
            StartCoroutine(QuickDeathSequence());
        }
    }

    private IEnumerator QuickDeathSequence()
    {
        yield return _ghost.PlayDeathAnimation();
        RespawnPlayerAndEnemies();
    }

    private IEnumerator FinalGameOverSequence()
    {
        if (cameraFollow != null)
        {
            cameraFollow.SetControlEnabled(false);
            Vector3 startPos = cameraFollow.transform.position;
            Vector3 endPos = _ghost.transform.position + closeUpOffset;
            float t = 0f;
            while (t < cameraZoomDuration)
            {
                t += Time.deltaTime;
                cameraFollow.transform.position = Vector3.Lerp(startPos, endPos, t / cameraZoomDuration);
                yield return null;
            }
        }

        yield return _ghost.PlayDeathAnimation();

        // The wallet is only halved if the player actually gives up (see
        // OnMainMenuClicked) - continuing via the ad keeps it whole, so the
        // screen warns what quitting would cost rather than charging now.
        if (subText != null)
        {
            int atStake = EconomyManager.Instance != null ? EconomyManager.Instance.CoinsLostOnDefeat : 0;
            subText.text = atStake > 0
                ? "Quit now and lose " + atStake + " coins"
                : "Continue to keep playing";
        }

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // Placeholder for a real rewarded-ad SDK: mocks the "watched to
    // completion" callback after a short delay, then grants one life and
    // resumes exactly like a normal respawn. Swap WatchAdMockSequence's
    // body for the SDK's reward callback later - nothing else changes.
    private void OnWatchAdClicked()
    {
        StartCoroutine(WatchAdMockSequence());
    }

    private IEnumerator WatchAdMockSequence()
    {
        watchAdButton.interactable = false;
        yield return new WaitForSeconds(watchAdMockDuration);
        watchAdButton.interactable = true;

        IsGameOverActive = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (LivesManager.Instance != null) LivesManager.Instance.GrantExtraLife();

        RespawnPlayerAndEnemies();
    }

    // Giving up is what actually costs the coins - the run is over here, so
    // the wallet is halved on the way out.
    private void OnMainMenuClicked()
    {
        if (EconomyManager.Instance != null) EconomyManager.Instance.HalveOnDefeat();
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void RespawnPlayerAndEnemies()
    {
        var blocksParent = GameObject.Find("Blocks");
        var candidates = new List<Transform>();
        foreach (Transform b in blocksParent.transform) candidates.Add(b);
        var chosen = candidates[Random.Range(0, candidates.Count)];
        _ghost.RespawnAt(chosen.position);
        _ghost.StartInvincibility(invincibilityDuration);

        if (cameraFollow != null) cameraFollow.SetControlEnabled(true);

        if (EnemySpawnManager.Instance != null) EnemySpawnManager.Instance.RespawnEnemies();
    }
}
