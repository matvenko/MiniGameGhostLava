using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Drives the main-menu / loading screen: starts loading the gameplay scene
// in the background right away, animates a progress bar for it, and once
// loading is done (and a minimum display time has passed, so it doesn't
// just flash by) hands over to the difficulty choice - picking Easy or Hard
// is what activates the loaded scene.
public class LoadingScreenController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "LavaScene";
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject startPrompt;
    [SerializeField] private DifficultySelectController difficultySelect;
    [SerializeField] private LoadingScreenVisuals visuals;
    [SerializeField] private float minimumDisplayTime = 2.5f;

    private AsyncOperation _loadOp;
    private bool _readyToStart;

    void Start()
    {
        if (startPrompt != null) startPrompt.SetActive(false);
        StartCoroutine(LoadSequence());
    }

    private IEnumerator LoadSequence()
    {
        float startTime = Time.time;

        _loadOp = SceneManager.LoadSceneAsync(gameplaySceneName);
        _loadOp.allowSceneActivation = false;

        while (_loadOp.progress < 0.9f)
        {
            UpdateProgress(_loadOp.progress / 0.9f);
            yield return null;
        }

        // hold the bar at 100% for the rest of the minimum display time so
        // it reads as a real loading screen even on a level this small
        while (Time.time - startTime < minimumDisplayTime)
        {
            UpdateProgress(1f);
            yield return null;
        }

        UpdateProgress(1f);
        _readyToStart = true;

        // Hand the middle of the screen over to the choice. The visuals fade
        // the whole loader out together (ring, percentage and bar), so the
        // percentage is only switched off directly when there are no visuals
        // to do it - otherwise it would blink away mid-fade.
        if (visuals != null) visuals.HideLoader();
        else if (statusText != null) statusText.gameObject.SetActive(false);

        if (startPrompt != null) startPrompt.SetActive(true);
        if (difficultySelect != null) difficultySelect.Show(OnDifficultyChosen);
    }

    private void OnDifficultyChosen(Difficulty difficulty)
    {
        _readyToStart = false;
        _loadOp.allowSceneActivation = true;
    }

    // Only a fallback: with the difficulty cards wired up they own starting
    // the game, and tapping anywhere must not skip the choice. If the
    // selector is missing the menu would otherwise be a dead end, so the old
    // press-anything behaviour stands in and the game starts on whichever
    // mode was last played.
    void Update()
    {
        if (!_readyToStart || difficultySelect != null) return;

        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            _readyToStart = false;
            _loadOp.allowSceneActivation = true;
        }
    }

    // The percentage reads as the number inside the loader ring, so it is
    // just the figure - the word "loading" is a label the ring already wears.
    private void UpdateProgress(float t)
    {
        if (progressBar != null) progressBar.value = t;
        if (statusText != null) statusText.text = Mathf.RoundToInt(t * 100f) + "%";
        if (visuals != null) visuals.SetProgress(t);
    }
}
