using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Drives the main-menu / loading screen: starts loading the gameplay scene
// in the background right away, animates a progress bar for it, and once
// loading is done (and a minimum display time has passed, so it doesn't
// just flash by) shows a "press to start" prompt before activating.
public class LoadingScreenController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "LavaScene";
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject startPrompt;
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
        // hand off to the prompt entirely so the two texts don't overlap
        if (statusText != null) statusText.gameObject.SetActive(false);
        if (startPrompt != null) startPrompt.SetActive(true);
    }

    void Update()
    {
        if (!_readyToStart) return;

        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0)
        {
            _readyToStart = false;
            _loadOp.allowSceneActivation = true;
        }
    }

    private void UpdateProgress(float t)
    {
        if (progressBar != null) progressBar.value = t;
        if (statusText != null) statusText.text = "Loading... " + Mathf.RoundToInt(t * 100f) + "%";
    }
}
