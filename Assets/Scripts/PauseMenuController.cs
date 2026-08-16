using UnityEngine;
using UnityEngine.UI;

// Escape toggles a pause popup: freezes gameplay via Time.timeScale, offers
// Resume/Quit, and lets the player mute music/SFX independently. Stays out
// of the way while the death/game-over sequence owns the screen.
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button musicToggleButton;
    [SerializeField] private Button sfxToggleButton;
    [SerializeField] private GameObject musicMuteSlash;
    [SerializeField] private GameObject sfxMuteSlash;
    [SerializeField] private AudioSource musicSource;

    private bool _isOpen;

    void Awake()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
        if (musicToggleButton != null) musicToggleButton.onClick.AddListener(ToggleMusic);
        if (sfxToggleButton != null) sfxToggleButton.onClick.AddListener(ToggleSfx);
    }

    void Start()
    {
        AudioManager.RegisterMusicSource(musicSource);
        RefreshIcons();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            bool gameOverActive = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive;
            bool levelCompleteActive = LevelManager.Instance != null && LevelManager.Instance.IsLevelCompleteActive;
            bool shopOpen = ShopUIController.Instance != null && ShopUIController.Instance.IsOpen;
            if (gameOverActive || levelCompleteActive || shopOpen) return;

            if (_isOpen) Close();
            else Open();
        }
    }

    private void Open()
    {
        _isOpen = true;
        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void Close()
    {
        _isOpen = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OnQuit()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ToggleMusic()
    {
        AudioManager.MusicMuted = !AudioManager.MusicMuted;
        RefreshIcons();
    }

    private void ToggleSfx()
    {
        AudioManager.SfxMuted = !AudioManager.SfxMuted;
        RefreshIcons();
    }

    private void RefreshIcons()
    {
        if (musicMuteSlash != null) musicMuteSlash.SetActive(AudioManager.MusicMuted);
        if (sfxMuteSlash != null) sfxMuteSlash.SetActive(AudioManager.SfxMuted);
    }
}
