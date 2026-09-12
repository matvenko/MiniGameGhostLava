using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The guide book behind "Game Info" on the pause card: who is on the board, what
// the abilities do, and the rules of the maze.
//
// It is one long scrolling page cut into chapters. The rail down the left is its
// table of contents - a tab per chapter and, under whichever chapter is being
// read, a line per page. Tapping either scrolls the book there; scrolling the
// book by hand moves the highlight along with it, so the rail always says where
// you are.
//
// It opens over the pause menu the way the shop does: the pause card is hidden
// rather than stacked underneath, and comes back when the book closes. The pause
// menu has already stopped time, so nothing here touches Time.timeScale, and
// everything that moves runs on unscaled time.
//
// Everything on the pages is laid out by GuideBookBuilder, which reads the
// numbers off the scene and the code. The 3D windows look after themselves
// (GuideBookShowcase); all this does for them is switch the stage they are
// filmed on on and off with the book.
public class GuideBookUI : MonoBehaviour
{
    public static GuideBookUI Instance { get; private set; }
    public bool IsOpen => bookPanel != null && bookPanel.activeSelf;

    [Serializable]
    private class Chapter
    {
        public Button tab;
        public Image tabPanel;
        public TextMeshProUGUI tabLabel;
        public Image tabIcon;
        [Tooltip("The chapter's heading in the scrolling content - where a tap on the tab scrolls to.")]
        public RectTransform heading;
    }

    [Serializable]
    private class Page
    {
        public RectTransform card;
        public int chapter;
        [Tooltip("The page's line in the table of contents.")]
        public Button row;
        public Image rowHighlight;
        public Image rowMarker;
        public TextMeshProUGUI rowLabel;
    }

    [SerializeField] private GameObject bookPanel;
    [SerializeField] private GameObject pausePanel;
    [Tooltip("Where the 3D windows' models stand, far off the board. On only while the book is open.")]
    [SerializeField] private GameObject stage;
    [SerializeField] private Button openButton;   // "Game Info" on the pause card
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scroll;
    [Tooltip("Top HUD put away while the book is up - the same pieces the shop puts away, which the book's plate and close cross would otherwise sit across.")]
    [SerializeField] private GameObject[] hudElementsToHide = new GameObject[0];
    [SerializeField] private Chapter[] chapters = new Chapter[0];
    [SerializeField] private Page[] pages = new Page[0];

    [Header("Rail")]
    [SerializeField] private Sprite tabPicked;
    [SerializeField] private Sprite tabUnpicked;
    [SerializeField] private float tabHeight = 76f;
    [SerializeField] private float rowHeight = 50f;
    [SerializeField] private float tabGap = 8f;
    [SerializeField] private Color inkPicked = Color.white;
    [SerializeField] private Color inkUnpicked = new Color(0.58f, 0.66f, 0.81f);
    [SerializeField] private Color markerPicked = new Color(0.00f, 0.94f, 0.92f);
    [SerializeField] private Color markerUnpicked = new Color(0.30f, 0.40f, 0.62f, 0.8f);

    // A jump starts this far above its target, so the card it lands on is not
    // pressed up against the top edge of the window.
    private const float LandingGap = 18f;
    // How far down the window a page has to reach before the rail calls it the
    // one being read.
    private const float ReadingLine = 0.3f;
    // Canvas units a second with the right stick all the way over.
    private const float StickScrollSpeed = 1600f;

    private int _chapter = -1, _page = -1;
    private bool _jumping;
    private float _jumpTarget, _jumpVelocity;
    private int _jumpPage = -1;

    void Awake()
    {
        Instance = this;
        if (bookPanel != null) bookPanel.SetActive(false);
        if (stage != null) stage.SetActive(false);
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        for (int i = 0; i < chapters.Length; i++)
        {
            int chapter = i;
            if (chapters[i].tab != null) chapters[i].tab.onClick.AddListener(() => JumpToChapter(chapter));
        }
        for (int i = 0; i < pages.Length; i++)
        {
            int page = i;
            if (pages[i].row != null) pages[i].row.onClick.AddListener(() => JumpToPage(page));
        }

        // With a controller the book starts on the tab of the chapter being read,
        // and B goes back to the pause card the way the cross does.
        GamepadMenus.Register(bookPanel, 40,
            () => _chapter >= 0 && _chapter < chapters.Length && chapters[_chapter].tab != null ? chapters[_chapter].tab : closeButton,
            Close);
    }

    public void Open()
    {
        AudioManager.Play(GameSound.Click);
        if (pausePanel != null) pausePanel.SetActive(false);
        SetHudVisible(false);
        if (stage != null) stage.SetActive(true);
        if (bookPanel != null) bookPanel.SetActive(true);

        // Reopened, the book is where it was left: someone checking one thing
        // twice should not have to find it again.
        _jumping = false;
        Spy(true);
    }

    // Back to the pause card, not out to the board: the book was opened from
    // there, and Resume is the pause card's to offer.
    public void Close()
    {
        AudioManager.Play(GameSound.Click);
        if (bookPanel != null) bookPanel.SetActive(false);
        if (stage != null) stage.SetActive(false);
        SetHudVisible(true);
        if (pausePanel != null) pausePanel.SetActive(true);
    }

    private void SetHudVisible(bool visible)
    {
        foreach (var go in hudElementsToHide)
            if (go != null) go.SetActive(visible);
    }

    void Update()
    {
        if (!IsOpen || scroll == null) return;
        StickScroll();
        if (_jumping) Glide();
        Spy(false);
    }

    // The right stick reads down the page the way a thumb drags it, and takes over
    // from a jump the same way.
    private void StickScroll()
    {
        float stick = Pad.Scroll;
        if (Mathf.Abs(stick) < .2f) return;
        _jumping = false;
        scroll.velocity = Vector2.zero;
        RectTransform content = scroll.content;
        Vector2 at = content.anchoredPosition;
        at.y = Mathf.Clamp(at.y - stick * StickScrollSpeed * Time.unscaledDeltaTime, 0f, MaxScroll());
        content.anchoredPosition = at;
    }

    // ---- getting about the book -------------------------------------------

    private void JumpToChapter(int chapter)
    {
        if (chapter < 0 || chapter >= chapters.Length) return;
        int first = Array.FindIndex(pages, p => p.chapter == chapter);
        AudioManager.Play(GameSound.Click);
        StartJump(Offset(chapters[chapter].heading), first);
    }

    private void JumpToPage(int page)
    {
        if (page < 0 || page >= pages.Length) return;
        AudioManager.Play(GameSound.Click);
        StartJump(Offset(pages[page].card) - LandingGap, page);
    }

    // The rail shows where the jump is going straight away, rather than
    // flickering through every page it passes on the way.
    private void StartJump(float target, int page)
    {
        _jumpTarget = Mathf.Clamp(target, 0f, MaxScroll());
        _jumpVelocity = 0f;
        _jumpPage = page;
        _jumping = true;
        scroll.velocity = Vector2.zero;
        Spy(true);
    }

    private void Glide()
    {
        // A thumb on the page takes over from the jump.
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            _jumping = false;
            return;
        }

        RectTransform content = scroll.content;
        Vector2 at = content.anchoredPosition;
        at.y = Mathf.SmoothDamp(at.y, _jumpTarget, ref _jumpVelocity, 0.18f, Mathf.Infinity, Time.unscaledDeltaTime);
        if (Mathf.Abs(at.y - _jumpTarget) < 0.5f)
        {
            at.y = _jumpTarget;
            _jumping = false;
        }
        scroll.velocity = Vector2.zero;
        content.anchoredPosition = at;
    }

    private float MaxScroll()
    {
        float view = scroll.viewport != null ? scroll.viewport.rect.height : ((RectTransform)scroll.transform).rect.height;
        return Mathf.Max(0f, scroll.content.rect.height - view);
    }

    // How far down the content something starts. Every card and heading hangs
    // from the top of the content by its own top edge.
    private static float Offset(RectTransform item) => item != null ? -item.anchoredPosition.y : 0f;

    // ---- which page is being read -----------------------------------------

    private void Spy(bool force)
    {
        if (pages.Length == 0) return;

        int reading = _jumping && _jumpPage >= 0 ? _jumpPage : PageInView();
        if (reading == _page && !force) return;
        _page = reading;

        int chapter = pages[_page].chapter;
        bool newChapter = chapter != _chapter;
        _chapter = chapter;

        for (int i = 0; i < chapters.Length; i++)
        {
            bool picked = i == _chapter;
            if (chapters[i].tabPanel != null) chapters[i].tabPanel.sprite = picked ? tabPicked : tabUnpicked;
            if (chapters[i].tabLabel != null) chapters[i].tabLabel.color = picked ? inkPicked : inkUnpicked;
            if (chapters[i].tabIcon != null) chapters[i].tabIcon.color = new Color(1f, 1f, 1f, picked ? 1f : 0.55f);
        }

        for (int i = 0; i < pages.Length; i++)
        {
            bool reads = i == _page;
            if (pages[i].rowHighlight != null) pages[i].rowHighlight.enabled = reads;
            if (pages[i].rowMarker != null) pages[i].rowMarker.color = reads ? markerPicked : markerUnpicked;
            if (pages[i].rowLabel != null) pages[i].rowLabel.color = reads ? inkPicked : inkUnpicked;
        }

        if (newChapter || force) LayOutRail();
    }

    private int PageInView()
    {
        float scrolled = scroll.content.anchoredPosition.y;
        float view = scroll.viewport != null ? scroll.viewport.rect.height : 0f;

        // At the very bottom the last page may be too short ever to reach the
        // reading line, and it is plainly the one on screen.
        if (scrolled >= MaxScroll() - 2f) return pages.Length - 1;

        float line = scrolled + view * ReadingLine;
        int reading = 0;
        for (int i = 0; i < pages.Length; i++)
            if (Offset(pages[i].card) <= line) reading = i;
        return reading;
    }

    // Tabs stacked down the rail, with the pages of the chapter being read
    // opened out underneath its tab and every other chapter folded away.
    private void LayOutRail()
    {
        float y = 0f;
        for (int c = 0; c < chapters.Length; c++)
        {
            if (chapters[c].tab != null)
                ((RectTransform)chapters[c].tab.transform).anchoredPosition = new Vector2(0f, -y);
            y += tabHeight + tabGap;

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i].chapter != c || pages[i].row == null) continue;
                bool open = c == _chapter;
                pages[i].row.gameObject.SetActive(open);
                if (!open) continue;
                ((RectTransform)pages[i].row.transform).anchoredPosition = new Vector2(0f, -y);
                y += rowHeight;
            }
            if (c == _chapter) y += tabGap * 2f;
        }
    }
}
