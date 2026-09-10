using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

// Escape toggles a pause popup: freezes gameplay via Time.timeScale, offers
// Resume/Main Menu, and lets the player mute music/SFX independently. Stays
// out of the way while the death/game-over sequence owns the screen.
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button openButton;   // the gear on the HUD, doing what Escape does
    [SerializeField] private Button resumeButton;
    [FormerlySerializedAs("quitButton")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private Button musicToggleButton;
    [SerializeField] private Button sfxToggleButton;
    [SerializeField] private GameObject musicMuteSlash;
    [SerializeField] private GameObject sfxMuteSlash;
    [SerializeField] private AudioSource musicSource;

    private bool _isOpen;

    void Awake()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        if (openButton != null) openButton.onClick.AddListener(Toggle);
        if (resumeButton != null) resumeButton.onClick.AddListener(Close);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
        if (musicToggleButton != null) musicToggleButton.onClick.AddListener(ToggleMusic);
        if (sfxToggleButton != null) sfxToggleButton.onClick.AddListener(ToggleSfx);
    }

    void Start()
    {
        AudioManager.StartMusic(musicSource);
        RefreshIcons();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    // Escape, and the gear on the HUD. Both are refused while something else owns
    // the screen: the game-over and level-complete popups have their own way out,
    // and the shop is opened over the top of this and closes back into it.
    public void Toggle()
    {
        // With the guide book open, Escape - which is also Android's back button -
        // goes back a page to this card rather than straight out to the board.
        if (GuideBookUI.Instance != null && GuideBookUI.Instance.IsOpen)
        {
            GuideBookUI.Instance.Close();
            return;
        }

        bool gameOverActive = GameOverManager.Instance != null && GameOverManager.Instance.IsGameOverActive;
        bool levelCompleteActive = LevelManager.Instance != null && LevelManager.Instance.IsLevelCompleteActive;
        bool shopOpen = ShopUIController.Instance != null && ShopUIController.Instance.IsOpen;
        if (gameOverActive || levelCompleteActive || shopOpen) return;

        if (_isOpen) Close();
        else Open();
    }

    private void Open()
    {
        AudioManager.Play(GameSound.Click);
        _isOpen = true;
        if (pausePanel != null) pausePanel.SetActive(true);
        Time.timeScale = 0f;
        Cover(true);
    }

    private void Close()
    {
        AudioManager.Play(GameSound.Click);
        _isOpen = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        GameSpeed.Resume();
        Cover(false);
    }

    // Opened during the warm-up beat at the start of a level, the popup would
    // have the countdown sitting frozen behind it. It goes away and comes back
    // where it was.
    private static void Cover(bool covered)
    {
        if (SpawnCountdownController.Instance != null) SpawnCountdownController.Instance.SetCovered(covered);
    }

    // Walking away costs nothing: the level, the lives, the wallet and the
    // abilities are all already written down, and CONTINUE picks the run up
    // exactly where it was put down (see RunProgress).
    private void OnMainMenu()
    {
        Time.timeScale = 1f;
        TestModeSession.LeaveBoard();
        SceneManager.LoadScene(mainMenuSceneName);
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
