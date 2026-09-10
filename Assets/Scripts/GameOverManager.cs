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
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private GhostScript _ghost;
    private TextMeshProUGUI _runSummary;

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

        // Nothing is taken away for quitting here - the run keeps its level, its
        // coins and its abilities either way. The difference is only when the ad
        // is watched: now, to carry straight on, or from the menu later to come
        // back to the same board.
        if (subText != null) subText.text = "Watch now to carry on, or later to come back";

        ShowRunSummary();

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    // What the run was worth, and where that puts it. Built here rather than
    // authored into the scene: it is one line of text that always sits in the
    // same gap between the coin warning and the buttons, and building it means
    // the panel needs no rewiring to gain it.
    //
    // The run is not necessarily over at this point - the ad button can hand a
    // life back and carry on - so this reads the standing the run has right now,
    // which is exactly what the Leaderboard has already been told (see
    // RunStats). Play on and the same screen will say something better later.
    private void ShowRunSummary()
    {
        if (gameOverPanel == null || subText == null) return;

        if (_runSummary == null)
        {
            var holder = new GameObject("Run summary", typeof(RectTransform));
            var rect = holder.GetComponent<RectTransform>();
            rect.SetParent(gameOverPanel.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0, 14);
            rect.sizeDelta = new Vector2(760, 80);
            _runSummary = holder.AddComponent<TextMeshProUGUI>();
            _runSummary.font = subText.font;
            _runSummary.fontSize = 26;
            _runSummary.alignment = TextAlignmentOptions.Center;
            _runSummary.color = new Color(.86f, .81f, 1f);
            _runSummary.raycastTarget = false;
        }

        string mode = RunStats.Mode == Difficulty.Normal ? "NORMAL" : "HARD";
        string tally = "Level " + RunStats.Level + "   ·   " + RunStats.Coins + " coins   ·   "
                       + RunRecord.Clock(RunStats.Seconds);

        int rank = Leaderboard.RankOf(RunStats.Mode, RunStats.RunId);
        RunRecord toBeat = Leaderboard.BestExcluding(RunStats.Mode, RunStats.RunId);

        string standing;
        if (rank == 0)
            // Nothing was collected and no level was cleared, so there is no
            // place in the table to report and no point inventing one.
            standing = "";
        else if (rank == 1 && toBeat == null)
            standing = "<color=#FFD24A>FIRST RUN ON THE " + mode + " BOARD!</color>";
        else if (rank == 1)
            standing = "<color=#FFD24A>NEW " + mode + " RECORD!</color>   Beaten: level " + toBeat.level;
        else
            standing = "#" + rank + " on " + mode + "   ·   best is level " + toBeat.level;

        _runSummary.text = standing.Length > 0 ? tally + "\n" + standing : tally;
    }

    // The ad hands back exactly one life and play resumes like any other
    // respawn. Whether there is really a video behind it belongs to RewardedAds,
    // not here.
    private void OnWatchAdClicked()
    {
        watchAdButton.interactable = false;
        RewardedAds.Show(this, CarryOn, () => watchAdButton.interactable = true);
    }

    private void CarryOn()
    {
        watchAdButton.interactable = true;
        IsGameOverActive = false;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (LivesManager.Instance != null) LivesManager.Instance.GrantExtraLife();

        RespawnPlayerAndEnemies();
    }

    // Giving up keeps everything the run earned - the level, the wallet and the
    // abilities all stay in the save, waiting for a continue. What it costs is a
    // life, the same as walking out through the pause menu, and by the time this
    // screen is up the last one is already spent: coming back will cost an ad
    // (see RunProgress.LeaveRun).
    private void OnMainMenuClicked()
    {
        RunProgress.LeaveRun();
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
