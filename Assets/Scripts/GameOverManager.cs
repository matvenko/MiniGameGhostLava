using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sample;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private Button continueButton;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private float cameraZoomDuration = 1f;
    [SerializeField] private Vector3 closeUpOffset = new Vector3(0f, 2.5f, 0f);

    private GhostScript _ghost;

    void Awake()
    {
        Instance = this;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
    }

    public void TriggerGameOver(GhostScript ghost)
    {
        _ghost = ghost;
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
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

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }

    private void OnContinueClicked()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        var blocksParent = GameObject.Find("Blocks");
        var candidates = new List<Transform>();
        foreach (Transform b in blocksParent.transform) candidates.Add(b);
        var chosen = candidates[Random.Range(0, candidates.Count)];
        _ghost.RespawnAt(chosen.position);

        if (cameraFollow != null) cameraFollow.SetControlEnabled(true);
    }
}
