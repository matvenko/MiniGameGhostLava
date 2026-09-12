using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// The existing MainMenu scene owns this controller. Artwork is built at runtime
// so the intro needs no external textures, tween packages or audio downloads.
public class LoadingScreenController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "LavaScene";
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject startPrompt;
    [SerializeField] private float minimumDisplayTime = 2.5f;
    [SerializeField] private GameObject introCharacterPrefab;

    private readonly Color ink = new Color(.035f, .035f, .075f);
    private readonly Color orange = new Color(1f, .36f, .16f);
    private readonly Color mint = new Color(.63f, 1f, .86f);
    private AsyncOperation load;
    private RectTransform safeRoot, hero, orbit, fill, playRect, progressWell, playShadow;
    private CanvasGroup entrance;
    private Image curtain;
    private TextMeshProUGUI soundLabel, playLabel, playNote;
    private Button play, fresh;
    private AudioSource music;
    private AudioClip readySound, startSound;
    private RectTransform[] embers;
    private float age, shownProgress;
    private bool ready, leaving;
    private TMP_FontAsset font;
    private Rect lastSafeArea;
    private int lastWidth, lastHeight;
    private RectTransform bestPill, recordsOverlay, recordsCard;
    private TextMeshProUGUI bestLabel, recordsTitle, recordsEmpty;
    private TextMeshProUGUI[] recordPlace, recordTally;
    private RenderTexture characterTexture;
    private Transform characterPivot, portraitStage, portraitFraming;
    private Camera characterCamera;
    private Renderer[] portraitRenderers;
    private bool portraitFramed;
    private bool portraitLooksUp;

    // Everything one of the two difficulty cards is made of. Held together so
    // the pair is dressed by a single call and cannot drift apart (see Dress).
    private class ModeChoice
    {
        public Button button;
        public Image edge;
        public IntroShape ghost;
        public RectTransform badge;
        public TextMeshProUGUI title, blurb;
        public Color accent;
    }

    private ModeChoice normalChoice, hardChoice;
    private RectTransform modeCard, startOverOverlay;
    private TextMeshProUGUI startOverBody;
    // Whether the gold button goes back to a saved run or starts a new one.
    // Read off RunProgress every time the mode changes, because the two modes
    // keep their saves apart and pressing HARD can change the answer.
    private bool resuming;
    // A rewarded video is playing, so nothing else may start the game under it.
    private bool watching;

    void Start()
    {
        font = statusText != null ? statusText.font : TMP_Settings.defaultFontAsset;
        // Preserve the scene's serialized references, but replace its old artwork.
        if (statusText != null)
        {
            Canvas old = statusText.GetComponentInParent<Canvas>();
            if (old != null) old.gameObject.SetActive(false);
        }
        BuildIntro();
        BuildAudio();
        StartCoroutine(LoadSequence());
    }

    private RectTransform design, titleBlock, heroBlock, controls;
    private Sprite roundSprite;
    private Texture2D roundTexture;
    private readonly System.Collections.Generic.List<Material> portraitMaterials = new System.Collections.Generic.List<Material>();
    [SerializeField] private TMP_FontAsset introFont;
    [SerializeField] private Shader portraitShader;

    private void BuildIntro()
    {
        if (introFont != null) font = introFont;
        BuildRoundSprite();
        var root = new GameObject("Maze Boo Intro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 1000);
        scaler.matchWidthOrHeight = .5f;
        var sky = Shape(root.transform, "Blueberry sky", Vector2.zero, Vector2.zero, Color.white, 3);
        Stretch(sky.rectTransform);
        // Large soft silhouettes extend beyond the safe area, filling wide displays.
        for (int i = 0; i < 9; i++)
        {
            var cloud = Rounded(root.transform, "Cloud bank", new Vector2((i-4)*290, -550 + (i%3)*35), new Vector2(600, 420), new Color(.23f,.19f,.49f));
            cloud.rectTransform.anchorMin = cloud.rectTransform.anchorMax = new Vector2(.5f, 0);
            cloud.rectTransform.anchoredPosition = new Vector2((i-4)*290, -60 + (i%3)*35);
        }
        embers = new RectTransform[28];
        for (int i = 0; i < embers.Length; i++)
        {
            embers[i] = Shape(root.transform, "Twinkling star", Vector2.zero, Vector2.one * (8+i%4*4), new Color(.86f,.78f,1,.45f), 4).rectTransform;
        }
        safeRoot = Rect(root.transform, "Safe area", Vector2.zero, Vector2.zero);
        Stretch(safeRoot);
        UpdateSafeArea();
        design = Rect(safeRoot, "Responsive design", Vector2.zero, new Vector2(1600,1000));
        entrance = design.gameObject.AddComponent<CanvasGroup>();
        entrance.alpha = 0;

        titleBlock = Rect(design, "Logo", new Vector2(0,346), new Vector2(1100,250));
        var tag = Rounded(titleBlock, "Adventure badge", new Vector2(0,112), new Vector2(350,40), new Color(.25f,.18f,.49f));
        Label(tag.transform,"A LITTLE BOO. A BIG ADVENTURE!",Vector2.zero,new Vector2(340,38),18,new Color(.88f,.81f,1));
        LogoWord(titleBlock,"MAZE",new Vector2(-152,6),new Vector2(440,164),new Color(1,.83f,.24f), -4);
        LogoWord(titleBlock,"BOO",new Vector2(162,-2),new Vector2(330,164),new Color(.98f,.92f,1), 3);
        Flicks(titleBlock,-470,1);
        Flicks(titleBlock,470,-1);
        Label(titleBlock,"Ready, set... BOO!",new Vector2(0,-80),new Vector2(700,50),29,new Color(.86f,.81f,1));

        heroBlock = Rect(design,"Your ghost's little world",new Vector2(0,5),new Vector2(1000,510));
        Rounded(heroBlock,"Halo",new Vector2(0,18),new Vector2(460,440),new Color(.58f,.40f,.88f,.18f));
        Rounded(heroBlock,"Halo inner",new Vector2(0,18),new Vector2(355,350),new Color(.69f,.49f,1,.15f));
        orbit = Rect(heroBlock,"Magic sparkles",Vector2.zero,new Vector2(400,400));
        for (int i=0;i<7;i++)
        {
            float a=i*Mathf.PI*2/7;
            Shape(orbit,"Magic",new Vector2(Mathf.Cos(a)*240,Mathf.Sin(a)*160),Vector2.one*(i%2==0?26:14),new Color(1,.86f,.45f),4);
        }
        // An isometric toy island, with individual mint tiles and candy-purple sides.
        Rounded(heroBlock,"Island shadow",new Vector2(0,-189),new Vector2(530,70),new Color(.12f,.08f,.3f,.4f));
        for(int diagonal=4;diagonal>=-4;diagonal--)
        for(int x=-2;x<=2;x++)
        {
            int z=diagonal-x;
            if(z < -2 || z > 2) continue;
            if(Mathf.Abs(x)+Mathf.Abs(z)>3) continue;
            Vector2 p=new Vector2((x-z)*65,-143+(x+z)*25);
            Shape(heroBlock,"Floating tile",p,new Vector2(130,78),((x+z)%2==0)?new Color(.52f,.87f,.65f):new Color(.66f,.95f,.70f),5);
        }
        Coin(heroBlock,new Vector2(-268,-21),68);
        Coin(heroBlock,new Vector2(247,57),76);
        Coin(heroBlock,new Vector2(325,-92),49);
        Coin(heroBlock,new Vector2(-334,88),44);
        var bubble=Rounded(heroBlock,"Hello bubble",new Vector2(262,104),new Vector2(135,70),new Color(1,.96f,.85f));
        Label(bubble.transform,"Boo!",Vector2.zero,new Vector2(125,65),32,new Color(.40f,.20f,.6f));
        bubble.rectTransform.localRotation=Quaternion.Euler(0,0,9);
        hero = Rect(heroBlock, "Main character portrait", new Vector2(0,12), new Vector2(350,350));
        BuildCharacterPortrait();
        var leftGhost=Shape(heroBlock,"Little blue friend",new Vector2(-430,-84),new Vector2(85,105),new Color(.46f,.89f,1),2);
        leftGhost.rectTransform.localRotation=Quaternion.Euler(0,0,-13);
        var rightGhost=Shape(heroBlock,"Little peach friend",new Vector2(423,-51),new Vector2(70,90),new Color(1,.63f,.68f),2);
        rightGhost.rectTransform.localRotation=Quaternion.Euler(0,0,16);

        controls=Rect(design,"Play area",new Vector2(0,-300),new Vector2(840,420));
        BuildModeCard();
        BuildStartButtons();
        var sound=MakeButton(design,"Sound",Vector2.zero,new Vector2(300,52),new Color(.25f,.17f,.45f),ToggleSound);
        sound.GetComponent<Image>().sprite=roundSprite; sound.GetComponent<Image>().type=Image.Type.Sliced;
        sound.name="Sound control";
        Shape(sound.transform,"Speaker",new Vector2(-107,1),new Vector2(36,32),new Color(.92f,.86f,1),11);
        soundLabel=Label(sound.transform,"",new Vector2(20,0),new Vector2(220,40),18,new Color(.92f,.86f,1));
        RefreshSoundLabel();
        BuildRecords();
        // Both cards are built after the record pill so they cover it: a card
        // that can be pressed through is not really a question.
        BuildStartOver();
        // The controller's way round the menu (see GamepadMenus): it starts on the
        // gold button once there is something to start, and on the chosen
        // difficulty until then.
        GamepadMenus.Register(design.gameObject, 10,
            () => play.interactable ? play : (DifficultySettings.IsNormal ? normalChoice.button : hardChoice.button));
        curtain=Box(root.transform,"Transition",Vector2.zero,Vector2.zero,new Color(ink.r,ink.g,ink.b,0));
        Stretch(curtain.rectTransform); curtain.raycastTarget=false;
        LayoutIntro();
    }

    private void LayoutIntro()
    {
        bool portrait=safeRoot.rect.height>safeRoot.rect.width;
        Vector2 size=portrait?new Vector2(900,1440):new Vector2(1600,1000);
        design.sizeDelta=size;
        design.localScale=Vector3.one*Mathf.Min(safeRoot.rect.width/size.x,safeRoot.rect.height/size.y);
        titleBlock.anchoredPosition=new Vector2(0,portrait?500:346);
        titleBlock.localScale=Vector3.one*(portrait?.95f:1);
        // The island is sized to stop just above the difficulty card rather than
        // to fill whatever is left, so the two never grow into each other.
        heroBlock.anchoredPosition=new Vector2(0,portrait?46:122);
        heroBlock.localScale=Vector3.one*(portrait?.92f:.86f);
        controls.anchoredPosition=new Vector2(0,portrait?-424:-300);
        // The record and the sound switch are the two things that are not part
        // of starting a run, so they sit out of the way in the top corners.
        design.Find("Sound control").GetComponent<RectTransform>().anchoredPosition=portrait?new Vector2(220,664):new Vector2(625,432);
        bestPill.anchoredPosition=portrait?new Vector2(-220,664):new Vector2(-625,432);
    }

    // The mode is picked before the run starts and nowhere else, so it sits on
    // the way to the play button rather than behind a settings screen.
    //
    // It is also the switch between two separate saved games: each mode keeps
    // its own level, wallet and abilities (see RunProgress), so pressing one of
    // these changes what the gold button underneath is offering to do.
    private void BuildModeCard()
    {
        modeCard=Rounded(controls,"Difficulty card",new Vector2(0,112),new Vector2(800,200),new Color(.19f,.13f,.38f,.92f)).rectTransform;
        Box(modeCard,"Header rule left",new Vector2(-152,70),new Vector2(86,3),new Color(.47f,.39f,.72f));
        Box(modeCard,"Header rule right",new Vector2(152,70),new Vector2(86,3),new Color(.47f,.39f,.72f));
        Label(modeCard,"CHOOSE DIFFICULTY",new Vector2(0,70),new Vector2(280,34),19,new Color(.76f,.70f,.95f));
        normalChoice=ModeCard("NORMAL","Easygoing adventure",-192,Difficulty.Normal,new Color(.42f,.87f,.56f));
        hardChoice=ModeCard("HARD","Bigger challenge",192,Difficulty.Hard,new Color(.95f,.35f,.40f));
    }

    // One of the two cards. Both are built from the same call so they can never
    // drift apart in size or wording, and which one looks chosen is redrawn from
    // what DifficultySettings says rather than from which was last pressed.
    private ModeChoice ModeCard(string word,string blurb,float x,Difficulty mode,Color accent)
    {
        var choice=new ModeChoice{accent=accent};
        choice.edge=Rounded(modeCard,word+" edge",new Vector2(x,-28),new Vector2(374,122),accent);
        choice.button=MakeButton(modeCard,word,new Vector2(x,-28),new Vector2(364,112),Color.white,()=>ChooseMode(mode));
        var image=choice.button.GetComponent<Image>(); image.sprite=roundSprite; image.type=Image.Type.Sliced;
        choice.ghost=Shape(choice.button.transform,"Mode ghost",new Vector2(-128,2),new Vector2(64,78),Color.white,mode==Difficulty.Normal?2:6);
        choice.title=Label(choice.button.transform,word,new Vector2(22,18),new Vector2(210,46),30,Color.white);
        choice.title.alignment=TextAlignmentOptions.Left;
        choice.blurb=Label(choice.button.transform,blurb,new Vector2(22,-20),new Vector2(230,34),18,Color.white);
        choice.blurb.alignment=TextAlignmentOptions.Left;
        choice.badge=Rect(choice.button.transform,"Chosen badge",new Vector2(142,6),new Vector2(50,50));
        Shape(choice.badge,"Badge disc",Vector2.zero,new Vector2(50,50),Color.white,0);
        Shape(choice.badge,"Badge tick",new Vector2(0,1),new Vector2(30,30),accent,7);
        return choice;
    }

    private void ChooseMode(Difficulty mode)
    {
        DifficultySettings.Current=mode;
        RefreshModePills();
        PlayEffect(readySound);
    }

    private void RefreshModePills()
    {
        if (normalChoice==null||hardChoice==null) return;
        bool normal=DifficultySettings.IsNormal;
        Dress(normalChoice,normal);
        Dress(hardChoice,!normal);
        RefreshStartButtons();
        RefreshRecords();
    }

    private void Dress(ModeChoice choice,bool picked)
    {
        var image=choice.button.GetComponent<Image>();
        image.color=picked?choice.accent:new Color(.25f,.17f,.45f);
        // Unchosen, the card goes dark with the colour of that mode drawn round
        // the outside: still there, but plainly not the one about to be played.
        choice.edge.color=picked?Color.Lerp(choice.accent,Color.white,.4f):new Color(choice.accent.r,choice.accent.g,choice.accent.b,.6f);
        choice.title.color=picked?new Color(.09f,.19f,.15f):new Color(.90f,.86f,1f);
        choice.title.fontStyle=FontStyles.Bold;
        choice.blurb.color=picked?new Color(.13f,.25f,.19f):new Color(.74f,.68f,.93f);
        choice.ghost.color=picked?Color.white:choice.accent;
        choice.badge.gameObject.SetActive(picked);
    }

    // The two ways into the board.
    //
    // Which is which depends on what the chosen mode has saved: a run to go back
    // to puts CONTINUE on the gold button and NEW GAME underneath it, and
    // nothing saved leaves only one thing worth pressing, so the gold button
    // starts the new run and the second button is not offered at all.
    private void BuildStartButtons()
    {
        playShadow=Rounded(controls,"Button shadow",new Vector2(0,-105),new Vector2(470,100),new Color(.55f,.24f,.09f)).rectTransform;
        play=MakeButton(controls,"Play",new Vector2(0,-95),new Vector2(470,100),new Color(1,.76f,.21f),OnPlayPressed);
        var buttonImage=play.GetComponent<Image>(); buttonImage.sprite=roundSprite; buttonImage.type=Image.Type.Sliced;
        Rounded(play.transform,"Button shine",new Vector2(0,33),new Vector2(398,9),new Color(1,.91f,.48f));
        playRect=play.GetComponent<RectTransform>();
        Shape(play.transform,"Play arrow",new Vector2(-174,4),new Vector2(38,42),new Color(.35f,.20f,.15f),8);
        playLabel=Label(play.transform,"LOADING...",new Vector2(20,12),new Vector2(350,52),35,new Color(.35f,.20f,.15f));
        playNote=Label(play.transform,"",new Vector2(20,-22),new Vector2(370,32),18,new Color(.47f,.29f,.12f));
        // The board is still loading for a second or two after the menu is up,
        // so the gold button carries its own progress along the base rather than
        // the layout keeping room for a bar that is gone almost immediately.
        var well=Rounded(play.transform,"Progress well",new Vector2(0,-40),new Vector2(390,10),new Color(.70f,.47f,.09f,.6f));
        progressWell=well.rectTransform;
        fill=Rounded(well.transform,"Progress",Vector2.zero,Vector2.zero,new Color(1,.97f,.72f)).rectTransform;
        fill.anchorMin=Vector2.zero; fill.anchorMax=new Vector2(0,1); fill.offsetMin=fill.offsetMax=Vector2.zero;
        play.interactable=false;

        fresh=MakeButton(controls,"New game",new Vector2(236,-95),new Vector2(260,100),new Color(.47f,.35f,.85f),OnNewGamePressed);
        var freshImage=fresh.GetComponent<Image>(); freshImage.sprite=roundSprite; freshImage.type=Image.Type.Sliced;
        Shape(fresh.transform,"Restart arrow",new Vector2(-92,1),new Vector2(32,32),new Color(.96f,.93f,1),9);
        Label(fresh.transform,"NEW GAME",new Vector2(20,0),new Vector2(190,50),23,new Color(.98f,.96f,1));
        RefreshModePills();
    }

    // Redrawn rather than remembered: switching mode, wiping a save, spending
    // the last life and the end of loading all change what these two buttons
    // should say, and all of them come through here.
    private void RefreshStartButtons()
    {
        if (play==null) return;
        Difficulty mode=DifficultySettings.Current;
        resuming=RunProgress.Exists(mode);
        if (ready)
        {
            playLabel.text=resuming?"CONTINUE":"NEW GAME";
            string where="Level "+RunProgress.LevelOf(mode)+"  ·  "+(mode==Difficulty.Normal?"Normal":"Hard");
            // Out of lives, the button still continues the same run - it just
            // says out loud what it is about to cost.
            playNote.text=resuming && RunProgress.LivesOf(mode)<=0
                ? where+(RewardedAds.Live?"  ·  ad for 1 life":"  ·  back with 1 life")
                : where;
        }
        else playLabel.text="GETTING READY";
        bool showFresh=resuming && ready;
        fresh.gameObject.SetActive(showFresh);
        // Keep the action row centered whether it contains one or two buttons.
        float playX=showFresh?-145f:0f;
        playRect.anchoredPosition=new Vector2(playX,-95);
        playShadow.anchoredPosition=new Vector2(playX,-105);
    }

    // The two ways a record reaches the menu: a pill that always says how far the
    // chosen mode has ever got, and the full ten behind it.
    //
    // The mode pills above already say which board is about to be played, so the
    // table simply follows them rather than growing tabs of its own - press HARD
    // and the hint, the pill and the card all change under it. Nothing here is
    // shared between the two modes; a normal run and a hard one are not the same
    // achievement (see Leaderboard).
    private void BuildRecords()
    {
        bestPill=MakeButton(design,"Best runs",Vector2.zero,new Vector2(352,52),new Color(.25f,.17f,.45f),OpenRecords).GetComponent<RectTransform>();
        var pillImage=bestPill.GetComponent<Image>(); pillImage.sprite=roundSprite; pillImage.type=Image.Type.Sliced;
        Shape(bestPill,"Trophy",new Vector2(-136,1),new Vector2(30,32),new Color(1,.83f,.24f),10);
        bestLabel=Label(bestPill,"",new Vector2(14,0),new Vector2(300,46),19,new Color(.92f,.86f,1));

        recordsOverlay=Rect(design,"Records",Vector2.zero,Vector2.zero);
        // Far wider than the design block so it still covers the screen on any
        // shape of display: nothing behind the card can be pressed by accident,
        // and pressing beside it is how the card is put away.
        var dim=MakeButton(recordsOverlay,"Dim",Vector2.zero,new Vector2(3200,2400),new Color(.02f,.02f,.06f,.72f),CloseRecords);
        dim.transition=Selectable.Transition.None;
        // A controller closes the card with B, so the ring never lands on the
        // backdrop.
        dim.navigation=new Navigation{mode=Navigation.Mode.None};
        var card=Rounded(recordsOverlay,"Records card",Vector2.zero,new Vector2(580,680),new Color(.16f,.11f,.33f));
        card.raycastTarget=true;
        recordsCard=card.rectTransform;
        recordsTitle=Label(recordsCard,"",new Vector2(0,286),new Vector2(540,50),28,new Color(1,.86f,.38f));
        Label(recordsCard,"DEEPEST RUNS FIRST",new Vector2(0,246),new Vector2(540,30),16,new Color(.72f,.66f,.92f));
        recordPlace=new TextMeshProUGUI[Leaderboard.Capacity];
        recordTally=new TextMeshProUGUI[Leaderboard.Capacity];
        for(int i=0;i<Leaderboard.Capacity;i++)
        {
            float y=196-i*44;
            if(i%2==0) Rounded(recordsCard,"Row",new Vector2(0,y),new Vector2(524,40),new Color(.21f,.15f,.40f));
            recordPlace[i]=Label(recordsCard,"",new Vector2(-131,y),new Vector2(250,38),21,Color.white);
            recordPlace[i].alignment=TextAlignmentOptions.Left;
            recordTally[i]=Label(recordsCard,"",new Vector2(131,y),new Vector2(250,38),21,Color.white);
            recordTally[i].alignment=TextAlignmentOptions.Right;
        }
        recordsEmpty=Label(recordsCard,"",new Vector2(0,60),new Vector2(500,150),22,new Color(.86f,.81f,1));
        var close=MakeButton(recordsCard,"Close",new Vector2(0,-282),new Vector2(240,58),new Color(.55f,.96f,.72f),CloseRecords);
        var closeImage=close.GetComponent<Image>(); closeImage.sprite=roundSprite; closeImage.type=Image.Type.Sliced;
        Label(close.transform,"CLOSE",new Vector2(0,-1),new Vector2(230,52),24,new Color(.10f,.24f,.18f));
        GamepadMenus.Register(recordsOverlay.gameObject,20,()=>close,CloseRecords);
        recordsOverlay.gameObject.SetActive(false);
        // The mode pills were dressed before this existed, so the first fill is
        // done here rather than waiting for the player to press one.
        RefreshRecords();
    }

    // Redrawn from the table rather than remembered, so switching mode, or
    // coming back to the menu having just beaten a record, always shows what is
    // actually stored.
    private void RefreshRecords()
    {
        if(bestLabel==null) return;
        string mode=DifficultySettings.IsNormal?"NORMAL":"HARD";
        var runs=Leaderboard.Top(DifficultySettings.Current);
        bestLabel.text=runs.Count>0?"BEST RUN  ·  LEVEL "+runs[0].level:"BEST RUN  ·  NONE YET";
        recordsTitle.text=mode+" RECORDS";
        recordsEmpty.text=runs.Count>0?"":"Nothing on this board yet.\nThe first run to pick up a coin lands here.";
        for(int i=0;i<recordPlace.Length;i++)
        {
            bool filled=i<runs.Count;
            recordPlace[i].text=filled?(i+1)+".   LEVEL "+runs[i].level:"";
            recordTally[i].text=filled?runs[i].coins+" coins   "+RunRecord.Clock(runs[i].seconds):"";
            // The run at the top of its board is the one to beat, so it is
            // lettered in the same gold as the coins.
            var tint=i==0?new Color(1,.86f,.38f):new Color(.88f,.83f,1);
            recordPlace[i].color=tint;
            recordTally[i].color=tint;
        }
    }

    private void OpenRecords()
    {
        RefreshRecords();
        recordsOverlay.gameObject.SetActive(true);
        PlayEffect(readySound);
    }

    private void CloseRecords() => recordsOverlay.gameObject.SetActive(false);

    private void LogoWord(Transform parent,string word,Vector2 position,Vector2 size,Color color,float angle)
    {
        var block=Rect(parent,word,position,size);
        block.localRotation=Quaternion.Euler(0,0,angle);
        Label(block,word,new Vector2(0,-10),size,119,new Color(.20f,.10f,.39f));
        var front=Label(block,word,Vector2.zero,size,119,color);
        front.outlineColor=new Color(.28f,.13f,.46f); front.outlineWidth=.14f;
    }
    private void Coin(Transform parent,Vector2 position,float size)
    {
        var outer=Rounded(parent,"Gold coin",position,Vector2.one*size,new Color(1,.60f,.08f));
        Rounded(outer.transform,"Coin rim",new Vector2(-2,3),Vector2.one*(size*.84f),new Color(1,.88f,.29f));
        Rounded(outer.transform,"Coin center",new Vector2(-2,3),Vector2.one*(size*.62f),new Color(1,.72f,.11f));
        Label(outer.transform,"G",new Vector2(-2,3),Vector2.one*size,size*.48f,new Color(1,.95f,.59f));
        outer.rectTransform.localRotation=Quaternion.Euler(0,0,-15);
    }
    private void BuildRoundSprite()
    {
        const int n=64;
        roundTexture=new Texture2D(n,n,TextureFormat.RGBA32,false);
        var pixels=new Color[n*n];
        for(int y=0;y<n;y++) for(int x=0;x<n;x++)
        {
            float d=new Vector2(x-31.5f,y-31.5f).magnitude;
            pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01(32-d));
        }
        roundTexture.SetPixels(pixels); roundTexture.Apply();
        roundSprite=Sprite.Create(roundTexture,new Rect(0,0,n,n),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(31,31,31,31));
    }
    private Image Rounded(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        var img=Box(parent,name,position,size,color);
        img.sprite=roundSprite; img.type=Image.Type.Sliced;
        if (name.StartsWith("Halo") || name == "Cloud bank" || name == "Island shadow" || (size.x>0 && Mathf.Approximately(size.x,size.y)))
            img.type=Image.Type.Simple;
        return img;
    }

    private void BuildCharacterPortrait()
    {
        if (introCharacterPrefab == null)
        {
            Debug.LogError("Assign the gameplay Ghost prefab to the intro character field.", this);
            return;
        }

        // Configure the inactive clone before gameplay scripts can start updating.
        // The preview keeps the original model, materials and idle animator.
        var stage = new GameObject("Character portrait studio");
        stage.transform.SetParent(transform, false);
        stage.transform.position = new Vector3(0, 0, -100);
        stage.SetActive(false);
        characterPivot = new GameObject("Character turntable").transform;
        characterPivot.SetParent(stage.transform, false);
        var framing = new GameObject("Unanimated framing offset").transform;
        framing.SetParent(characterPivot, false);
        var character = Instantiate(introCharacterPrefab, framing, false);
        character.transform.localPosition = Vector3.zero;
        character.transform.localRotation = Quaternion.identity;
        foreach (var appearance in character.GetComponentsInChildren<WardenAppearance>(true))
            appearance.PreparePortrait();
        foreach (var script in character.GetComponentsInChildren<MonoBehaviour>(true))
            script.enabled = false;
        foreach (var collider in character.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (var source in character.GetComponentsInChildren<AudioSource>(true))
            source.enabled = false;
        foreach (var camera in character.GetComponentsInChildren<Camera>(true))
            camera.enabled = false;
        foreach (var listener in character.GetComponentsInChildren<AudioListener>(true))
            listener.enabled = false;
        foreach (var body in character.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;
        foreach (var part in character.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = 31;
            part.gameObject.tag = "Untagged";
        }
        foreach (var animator in character.GetComponentsInChildren<Animator>(true))
        {
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // The gameplay shader dissolves by world position. A portrait studio is
        // outside the board, so use a dedicated opaque shader with the SAME skin.
        foreach (var renderer in character.GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || portraitShader == null) continue;
                var original = materials[i];
                if (original.shader.name == "MiniGame/WardenSpirit")
                {
                    portraitLooksUp = true;
                    continue;
                }
                // The UI portrait is LDR: keep Lumen's surface shading without
                // clipping its gameplay HDR emission to a flat white silhouette.
                if (original.shader.name == "MiniGame/LumenSpirit")
                {
                    portraitLooksUp = true;
                    var portraitMaterial = new Material(original);
                    portraitMaterial.SetFloat("_Dissolve", 1);
                    if (original.name.StartsWith("Lumen_Pearl"))
                    {
                        portraitMaterial.SetFloat("_Emission", .04f);
                        portraitMaterial.SetColor("_BaseColor", new Color(.84f, .90f, .95f));
                        portraitMaterial.SetColor("_ShadeColor", new Color(.35f, .52f, .64f));
                        portraitMaterial.SetColor("_GlowColor", new Color(.05f, .2f, .3f));
                    }
                    materials[i] = portraitMaterial;
                    portraitMaterials.Add(portraitMaterial);
                    continue;
                }
                var material = new Material(portraitShader);
                Texture skin = original.HasProperty("_MainTexture") ? original.GetTexture("_MainTexture") : original.mainTexture;
                material.SetTexture("_MainTex", skin);
                // Over-bright, so the grey character skin reads as a warm peach
                // ghost against the blueberry sky instead of dusty mauve.
                material.SetColor("_Tint", new Color(1.5f,.95f,1.05f,1));
                materials[i] = material;
                portraitMaterials.Add(material);
            }
            renderer.sharedMaterials = materials;
        }
        stage.SetActive(true);
        foreach (var animator in character.GetComponentsInChildren<Animator>(true))
        {
            animator.Rebind();
            animator.Update(0);
        }
        // Live bounds, so the portrait camera can frame the posed character.
        foreach (var skinned in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            skinned.updateWhenOffscreen = true;

        portraitRenderers = character.GetComponentsInChildren<Renderer>(true);
        if (portraitRenderers.Length == 0) return;
        portraitStage = stage.transform;
        portraitFraming = framing;

        characterTexture = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32);
        characterTexture.name = "Maze Boo character portrait";
        characterTexture.Create();
        var portrait = hero.gameObject.AddComponent<RawImage>();
        portrait.texture = characterTexture;
        portrait.raycastTarget = false;

        characterCamera = new GameObject("Portrait camera").AddComponent<Camera>();
        characterCamera.transform.SetParent(stage.transform, false);
        characterCamera.orthographic = true;
        characterCamera.nearClipPlane = .01f;
        characterCamera.clearFlags = CameraClearFlags.SolidColor;
        characterCamera.backgroundColor = Color.clear;
        characterCamera.cullingMask = 1 << 31;
        characterCamera.allowHDR = false;
        characterCamera.targetTexture = characterTexture;
        var key = new GameObject("Portrait light").AddComponent<Light>();
        key.transform.SetParent(stage.transform, false);
        key.transform.localRotation = Quaternion.Euler(25, 155, 0);
        key.type = LightType.Directional;
        key.color = new Color(.85f, .93f, 1);
        key.intensity = 1.4f;
        key.cullingMask = 1 << 31;
        stage.SetActive(true);
        FrameCharacter();
    }

    // Centre the portrait camera on whatever the character actually occupies.
    // Skinned meshes only report honest bounds once they have been posed, so the
    // framing is measured again on the first full frame (see Update).
    private void FrameCharacter()
    {
        if (characterCamera == null || portraitRenderers == null) return;
        Quaternion spin = characterPivot.localRotation;
        characterPivot.localRotation = Quaternion.identity;
        // Keep the turntable axis under the character so the idle spin stays put.
        portraitFraming.localPosition = Vector3.zero;
        if (!MeasureCharacter(out Bounds bounds)) { characterPivot.localRotation = spin; return; }
        Vector3 pivoted = portraitStage.InverseTransformPoint(bounds.center);
        portraitFraming.localPosition = new Vector3(-pivoted.x, 0, -pivoted.z);
        if (!MeasureCharacter(out bounds)) { characterPivot.localRotation = spin; return; }
        characterPivot.localRotation = spin;

        // A square render texture, so frame by the largest half-extent it must hold.
        float half = Mathf.Max(bounds.extents.y, Mathf.Max(bounds.extents.x, bounds.extents.z));
        half = Mathf.Max(half, .05f);
        float reach = Mathf.Max(bounds.extents.magnitude, .1f);
        Vector3 focus = bounds.center + new Vector3(0, half * .06f, 0);
        characterCamera.transform.position = focus + new Vector3(0, portraitLooksUp ? reach * 2.2f : half * .18f, reach * 4);
        characterCamera.transform.LookAt(focus);
        characterCamera.orthographicSize = half * 1.12f;
        characterCamera.farClipPlane = reach * 9;
    }

    private bool MeasureCharacter(out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (var renderer in portraitRenderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return found && bounds.extents.sqrMagnitude > 0;
    }

    private IEnumerator LoadSequence()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            playLabel.text = "SCENE UNAVAILABLE";
            playNote.text = "Add LavaScene to Build Settings";
            Debug.LogError("Intro cannot load scene: " + gameplaySceneName, this);
            yield break;
        }
        yield return null; // Present the intro before loading starts.
        load = SceneManager.LoadSceneAsync(gameplaySceneName);
        if (load == null) yield break;
        load.allowSceneActivation = false;
        while (shownProgress < .999f)
        {
            float target = Mathf.Min(load.progress / .9f, age / Mathf.Max(1, minimumDisplayTime));
            shownProgress = Mathf.MoveTowards(shownProgress, target, Time.unscaledDeltaTime * .65f);
            fill.anchorMax = new Vector2(shownProgress, 1);
            playNote.text = shownProgress < .35f ? "Packing the adventure..." : shownProgress < .75f ? "Counting shiny coins..." : "Waking your little ghost...";
            yield return null;
        }
        fill.anchorMax = Vector2.one;
        ready = true;
        play.interactable = true;
        // The bar has nothing left to say, and what it was drawn across is the
        // menu in the artwork - so it goes rather than sitting there full.
        progressWell.gameObject.SetActive(false);
        RefreshStartButtons();
        PlayEffect(readySound);
    }

    void Update()
    {
        if (safeRoot == null) return;
        age += Time.unscaledDeltaTime;
        UpdateSafeArea();
        LayoutIntro();
        entrance.alpha = Mathf.SmoothStep(0, 1, age / .85f);
        hero.anchoredPosition = new Vector2(Mathf.Sin(age * .7f) * 12, 12 + Mathf.Sin(age * 1.8f) * 12);
        hero.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 1.2f) * 5);
        if (characterPivot != null)
        {
            if (!portraitFramed) { FrameCharacter(); portraitFramed = true; }
            characterPivot.localRotation = Quaternion.Euler(0, Mathf.Sin(age * .65f) * 18, 0);
        }
        orbit.localRotation = Quaternion.Euler(0, 0, age * 9);
        if (ready) playRect.localScale = Vector3.one * (1 + Mathf.Sin(age * 3) * .012f);
        var canvasRect = (RectTransform)safeRoot.parent;
        float w = canvasRect.rect.width, h = canvasRect.rect.height;
        for (int i = 0; i < embers.Length; i++)
        {
            float p = Mathf.Repeat(i * .618034f + age * (.022f + i % 5 * .006f), 1);
            embers[i].anchoredPosition = new Vector2((Mathf.Repeat(i * .381966f, 1) - .5f) * w + Mathf.Sin(age + i) * 20, (p - .5f) * h);
            embers[i].localRotation = Quaternion.Euler(0, 0, -20 + Mathf.Sin(age + i) * 15);
        }
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        pressed = Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        pressed = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
        // Start on a controller is Enter.
        pressed |= Pad.Pressed(PadButton.Start);
        // Enter starts the run, but not from behind a card - the key press
        // belongs to whatever is actually in front of the player.
        bool covered = recordsOverlay.gameObject.activeSelf || startOverOverlay.gameObject.activeSelf;
        if (pressed && ready && !leaving && !watching && !covered) OnPlayPressed();
    }

    private void UpdateSafeArea()
    {
        Rect area = Screen.safeArea;
        if (area == lastSafeArea && lastWidth == Screen.width && lastHeight == Screen.height) return;
        lastSafeArea = area;
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        if (Screen.width == 0 || Screen.height == 0) return;
        safeRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
    }

    // The gold button.
    //
    // A saved run with lives left is simply picked up where it was put down, and
    // costs nothing to come back to. A run with no lives left cannot be played
    // at all until it has one, and a rewarded video is what buys it - one life,
    // exactly, with the level, the wallet and the abilities untouched.
    private void OnPlayPressed()
    {
        if (!ready || leaving || watching) return;
        if (!resuming || RunProgress.LivesOf(DifficultySettings.Current) > 0) { BeginGame(); return; }
        if (!RewardedAds.Live) { PaidContinue(); return; }
        watching = true;
        play.interactable = false;
        fresh.interactable = false;
        playLabel.text = "LOADING AD";
        playNote.text = "";
        RewardedAds.Show(this, PaidContinue, AdUnavailable);
    }

    private void PaidContinue()
    {
        watching = false;
        RunProgress.Lives = 1;
        PlayEffect(readySound);
        BeginGame();
    }

    // No ad to show, or one that was closed early. Nothing is granted and the
    // menu goes back to how it was, saying why.
    private void AdUnavailable()
    {
        watching = false;
        play.interactable = true;
        fresh.interactable = true;
        RefreshStartButtons();
        playNote.text = "No ad right now - try again in a moment";
    }

    // Starting over throws away a save that can be dozens of levels deep, so it
    // is worth a question - and the question says what is about to be lost
    // rather than asking whether the player is sure.
    private void OnNewGamePressed()
    {
        if (!ready || leaving || watching) return;
        Difficulty mode = DifficultySettings.Current;
        startOverBody.text = "Your " + (mode == Difficulty.Normal ? "NORMAL" : "HARD") + " game is saved on level "
            + RunProgress.LevelOf(mode) + ", with " + RunProgress.LivesOf(mode) + " lives, "
            + RunProgress.CoinsOf(mode) + " coins and " + RunProgress.AbilitiesOwned(mode) + " abilities."
            + "\n\nStarting a new game puts every bit of that back to the beginning.";
        startOverOverlay.gameObject.SetActive(true);
        PlayEffect(readySound);
    }

    private void ConfirmStartOver()
    {
        RunProgress.Reset(DifficultySettings.Current);
        CloseStartOver();
        RefreshModePills();
        BeginGame();
    }

    private void CloseStartOver() => startOverOverlay.gameObject.SetActive(false);

    private void BuildStartOver()
    {
        startOverOverlay = Rect(design, "Start over", Vector2.zero, Vector2.zero);
        var dim = MakeButton(startOverOverlay, "Dim", Vector2.zero, new Vector2(3200, 2400), new Color(.02f, .02f, .06f, .74f), CloseStartOver);
        dim.transition = Selectable.Transition.None;
        dim.navigation = new Navigation { mode = Navigation.Mode.None };
        var card = Rounded(startOverOverlay, "Start over card", Vector2.zero, new Vector2(700, 440), new Color(.16f, .11f, .33f));
        card.raycastTarget = true;
        Label(card.transform, "START A NEW GAME?", new Vector2(0, 152), new Vector2(640, 56), 34, new Color(1, .86f, .38f));
        startOverBody = Label(card.transform, "", new Vector2(0, 30), new Vector2(600, 200), 22, new Color(.88f, .83f, 1));
        var wipe = MakeButton(card.transform, "Wipe", new Vector2(-166, -152), new Vector2(300, 74), new Color(.90f, .31f, .36f), ConfirmStartOver);
        var wipeImage = wipe.GetComponent<Image>(); wipeImage.sprite = roundSprite; wipeImage.type = Image.Type.Sliced;
        Label(wipe.transform, "ERASE AND PLAY", new Vector2(0, -1), new Vector2(288, 66), 22, Color.white);
        var keep = MakeButton(card.transform, "Keep", new Vector2(166, -152), new Vector2(300, 74), new Color(.30f, .22f, .52f), CloseStartOver);
        var keepImage = keep.GetComponent<Image>(); keepImage.sprite = roundSprite; keepImage.type = Image.Type.Sliced;
        Label(keep.transform, "KEEP MY RUN", new Vector2(0, -1), new Vector2(288, 66), 22, new Color(.92f, .87f, 1));
        // A controller starts on keeping the run - wiping a deep save should take
        // a deliberate step across - and B keeps it too.
        GamepadMenus.Register(startOverOverlay.gameObject, 20, () => keep, CloseStartOver);
        startOverOverlay.gameObject.SetActive(false);
    }

    // The three gold flicks either side of the name.
    private void Flicks(Transform parent, float x, float direction)
    {
        for (int i = 0; i < 3; i++)
        {
            var flick = Rounded(parent, "Flick", new Vector2(x - direction * (i == 1 ? 22 : 0), 32 - i * 32), new Vector2(52, 12), new Color(1, .83f, .24f));
            flick.rectTransform.localRotation = Quaternion.Euler(0, 0, direction * (22 - i * 22));
        }
    }

    private void BeginGame()
    {
        if (!ready || leaving || load == null) return;
        leaving = true;
        ready = false;
        play.interactable = false;
        if (fresh != null) fresh.interactable = false;
        StartCoroutine(EnterGame());
    }

    private IEnumerator EnterGame()
    {
        PlayEffect(startSound);
        curtain.raycastTarget = true;
        for (float t = 0; t < .65f; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.SmoothStep(0, 1, t / .65f);
            curtain.color = new Color(ink.r, ink.g, ink.b, a);
            music.volume = (1 - a) * .22f;
            yield return null;
        }
        curtain.color = ink;
        load.allowSceneActivation = true;
    }

    private void ToggleSound()
    {
        bool muted = !(AudioManager.MusicMuted && AudioManager.SfxMuted);
        AudioManager.MusicMuted = muted;
        AudioManager.SfxMuted = muted;
        music.mute = muted;
        PlayerPrefs.Save();
        RefreshSoundLabel();
        if (!muted) PlayEffect(readySound);
    }

    private void RefreshSoundLabel() => soundLabel.text = AudioManager.MusicMuted && AudioManager.SfxMuted ? "SOUND: OFF" : "SOUND: ON";

    private void BuildAudio()
    {
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        readySound = GameAudioClips.Get(GameSound.Ready);
        startSound = GameAudioClips.Get(GameSound.Start);
        AudioManager.StartMusic(music);
    }

    private void PlayEffect(AudioClip clip)
    {
        AudioManager.Play(clip == startSound ? GameSound.Start : GameSound.Ready);
    }
    void OnDestroy()
    {
        foreach (var material in portraitMaterials) if (material != null) Destroy(material);
        if (roundSprite != null) Destroy(roundSprite);
        if (roundTexture != null) Destroy(roundTexture);
        if (characterCamera != null) characterCamera.targetTexture = null;
        if (characterTexture != null)
        {
            characterTexture.Release();
            Destroy(characterTexture);
        }
        if (music != null) music.Stop();

    }

    private static RectTransform Rect(Transform parent, string title, Vector2 position, Vector2 size)
    {
        var go = new GameObject(title, typeof(RectTransform));
        var r = go.GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
        r.anchoredPosition = position;
        r.sizeDelta = size;
        return r;
    }

    private static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    private static Image Box(Transform parent, string title, Vector2 position, Vector2 size, Color color)
    {
        var img = Rect(parent, title, position, size).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TextMeshProUGUI Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var label = Rect(parent, text, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private static Button MakeButton(Transform parent, string title, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction action)
    {
        var img = Box(parent, title, position, size, color);
        img.raycastTarget = true;
        var button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        // These tints multiply the colour the button already carries rather than
        // replacing it, so they stay neutral: a coloured highlight over a
        // coloured button comes out muddy, and over a dark one comes out black.
        var colors = button.colors;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.78f, .78f, .78f);
        colors.disabledColor = new Color(.55f, .55f, .55f, .8f);
        button.colors = colors;
        button.onClick.AddListener(action);
        return button;
    }

    private static IntroShape Shape(Transform parent, string title, Vector2 position, Vector2 size, Color color, int kind)
    {
        var shapeRect = Rect(parent, title, position, size);
        shapeRect.gameObject.AddComponent<CanvasRenderer>();
        var shape = shapeRect.gameObject.AddComponent<IntroShape>();
        shape.color = color;
        shape.kind = kind;
        shape.raycastTarget = false;
        return shape;
    }
}

internal class IntroShape : MaskableGraphic
{
    public int kind;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (kind == 3)
        {
            vh.AddVert(new Vector2(r.xMin,r.yMin),new Color(.28f,.16f,.48f),Vector2.zero);
            vh.AddVert(new Vector2(r.xMin,r.yMax),new Color(.21f,.18f,.48f),Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,r.yMax),new Color(.21f,.18f,.48f),Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,r.yMin),new Color(.28f,.16f,.48f),Vector2.zero);
            vh.AddTriangle(0,1,2); vh.AddTriangle(0,2,3);
        }
        else if (kind == 4)
        {
            for (int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4, b=(i+1)*Mathf.PI/4;
                int n=vh.currentVertCount;
                vh.AddVert(Vector2.zero,color,Vector2.zero);
                vh.AddVert(Point(a,i%2==0?.5f:.16f,r),color,Vector2.zero);
                vh.AddVert(Point(b,i%2==0?.16f:.5f,r),color,Vector2.zero);
                vh.AddTriangle(n,n+2,n+1);
            }
        }
        else if (kind == 5)
        {
            Vector2 left=new Vector2(-r.width*.48f,0), top=new Vector2(0,r.height*.30f), right=new Vector2(r.width*.48f,0), bottom=new Vector2(0,-r.height*.30f);
            Vector2 depth=new Vector2(0,-r.height*.36f);
            Quad(vh,left,bottom,bottom+depth,left+depth,new Color(.39f,.29f,.61f));
            Quad(vh,bottom,right,right+depth,bottom+depth,new Color(.28f,.19f,.47f));
            Quad(vh,left,top,right,bottom,color);
        }
        else if (kind == 1)
        {
            for (int i = 0; i < 96; i++)
            {
                if (i % 24 > 18) continue;
                float a = i * Mathf.PI * 2 / 96, b = (i + 1) * Mathf.PI * 2 / 96;
                Quad(vh, Point(a, .5f, r), Point(b, .5f, r), Point(b, .38f, r), Point(a, .38f, r), color);
            }
        }
        else if (kind == 2 || kind == 6)
        {
            // The little ghost, either wide-eyed or - on the hard card - scowling.
            GhostBody(vh, r, color);
            Color eye = new Color(.055f, .09f, .13f);
            float lift = kind == 6 ? .06f : .10f;
            Disk(vh, new Vector2(-r.width * .16f, r.height * lift), new Vector2(r.width * .055f, r.height * .08f), eye);
            Disk(vh, new Vector2(r.width * .16f, r.height * lift), new Vector2(r.width * .055f, r.height * .08f), eye);
            if (kind == 6)
            {
                Stroke(vh, new Vector2(-r.width * .30f, r.height * .30f), new Vector2(-r.width * .06f, r.height * .17f), r.height * .07f, eye);
                Stroke(vh, new Vector2(r.width * .30f, r.height * .30f), new Vector2(r.width * .06f, r.height * .17f), r.height * .07f, eye);
            }
        }
        else if (kind == 7)
        {
            // Tick, for the difficulty card being played.
            Stroke(vh, new Vector2(-r.width * .28f, r.height * .02f), new Vector2(-r.width * .07f, -r.height * .23f), r.height * .16f, color);
            Stroke(vh, new Vector2(-r.width * .11f, -r.height * .21f), new Vector2(r.width * .30f, r.height * .25f), r.height * .16f, color);
        }
        else if (kind == 8)
        {
            int n = vh.currentVertCount;
            vh.AddVert(new Vector2(-r.width * .32f, r.height * .45f), color, Vector2.zero);
            vh.AddVert(new Vector2(r.width * .40f, 0), color, Vector2.zero);
            vh.AddVert(new Vector2(-r.width * .32f, -r.height * .45f), color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
        }
        else if (kind == 9)
        {
            // Circling arrow, for starting the whole thing again.
            const float from = Mathf.PI * .38f, to = Mathf.PI * 1.98f;
            Arc(vh, from, to, .355f, .11f, r, color);
            int n = vh.currentVertCount;
            vh.AddVert(Point(from - .80f, .355f, r), color, Vector2.zero);
            vh.AddVert(Point(from + .04f, .58f, r), color, Vector2.zero);
            vh.AddVert(Point(from + .04f, .13f, r), color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
        }
        else if (kind == 10)
        {
            // A little cup, for the record the menu keeps in its top corner.
            Quad(vh, new Vector2(-r.width * .25f, r.height * .42f), new Vector2(r.width * .25f, r.height * .42f), new Vector2(r.width * .15f, -r.height * .04f), new Vector2(-r.width * .15f, -r.height * .04f), color);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 pivot = new Vector2(side * r.width * .27f, r.height * .26f);
                for (int i = 0; i < 12; i++)
                {
                    float a = -Mathf.PI * .5f + i * Mathf.PI / 12, b = -Mathf.PI * .5f + (i + 1) * Mathf.PI / 12;
                    Vector2 outerA = new Vector2(pivot.x + side * Mathf.Cos(a) * r.width * .17f, pivot.y + Mathf.Sin(a) * r.height * .17f);
                    Vector2 outerB = new Vector2(pivot.x + side * Mathf.Cos(b) * r.width * .17f, pivot.y + Mathf.Sin(b) * r.height * .17f);
                    Vector2 innerA = new Vector2(pivot.x + side * Mathf.Cos(a) * r.width * .09f, pivot.y + Mathf.Sin(a) * r.height * .09f);
                    Vector2 innerB = new Vector2(pivot.x + side * Mathf.Cos(b) * r.width * .09f, pivot.y + Mathf.Sin(b) * r.height * .09f);
                    Quad(vh, outerA, outerB, innerB, innerA, color);
                }
            }
            Quad(vh, new Vector2(-r.width * .07f, -r.height * .02f), new Vector2(r.width * .07f, -r.height * .02f), new Vector2(r.width * .07f, -r.height * .28f), new Vector2(-r.width * .07f, -r.height * .28f), color);
            Quad(vh, new Vector2(-r.width * .26f, -r.height * .28f), new Vector2(r.width * .26f, -r.height * .28f), new Vector2(r.width * .26f, -r.height * .44f), new Vector2(-r.width * .26f, -r.height * .44f), color);
        }
        else if (kind == 11)
        {
            // Speaker box, cone and two waves, for the sound switch.
            Quad(vh, new Vector2(-r.width * .44f, r.height * .17f), new Vector2(-r.width * .22f, r.height * .17f), new Vector2(-r.width * .22f, -r.height * .17f), new Vector2(-r.width * .44f, -r.height * .17f), color);
            int n = vh.currentVertCount;
            vh.AddVert(new Vector2(-r.width * .24f, r.height * .12f), color, Vector2.zero);
            vh.AddVert(new Vector2(-r.width * .02f, r.height * .45f), color, Vector2.zero);
            vh.AddVert(new Vector2(-r.width * .02f, -r.height * .45f), color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            n = vh.currentVertCount;
            vh.AddVert(new Vector2(-r.width * .24f, r.height * .12f), color, Vector2.zero);
            vh.AddVert(new Vector2(-r.width * .02f, -r.height * .45f), color, Vector2.zero);
            vh.AddVert(new Vector2(-r.width * .24f, -r.height * .12f), color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
            for (int wave = 0; wave < 2; wave++)
            {
                float radius = .18f + wave * .14f;
                Vector2 origin = new Vector2(r.width * .04f, 0);
                for (int i = 0; i < 16; i++)
                {
                    float a = -Mathf.PI * .32f + i * Mathf.PI * .64f / 16, b = -Mathf.PI * .32f + (i + 1) * Mathf.PI * .64f / 16;
                    Quad(vh, origin + Point(a, radius, r), origin + Point(b, radius, r), origin + Point(b, radius - .045f, r), origin + Point(a, radius - .045f, r), color);
                }
            }
        }
        else Disk(vh, Vector2.zero, r.size * .5f, color);
    }

    // The body every ghost on this screen is drawn from: rounded crown,
    // straight sides and a scalloped hem. What goes on its face is the caller.
    private static void GhostBody(VertexHelper vh, Rect r, Color color)
    {
        Disk(vh, new Vector2(0, r.height * .10f), new Vector2(r.width * .45f, r.height * .38f), color);
        Quad(vh, new Vector2(-r.width * .45f, -r.height * .28f), new Vector2(r.width * .45f, -r.height * .28f), new Vector2(r.width * .45f, r.height * .1f), new Vector2(-r.width * .45f, r.height * .1f), color);
        for (int i = 0; i < 3; i++) Disk(vh, new Vector2((i - 1) * r.width * .3f, -r.height * .27f), new Vector2(r.width * .15f, r.height * .14f), color);
    }

    // A band of the ellipse that fits the rect, from one angle to another.
    private static void Arc(VertexHelper vh, float from, float to, float radius, float thickness, Rect r, Color color)
    {
        const int steps = 44;
        for (int i = 0; i < steps; i++)
        {
            float a = from + (to - from) * i / steps, b = from + (to - from) * (i + 1) / steps;
            Quad(vh, Point(a, radius + thickness * .5f, r), Point(b, radius + thickness * .5f, r),
                     Point(b, radius - thickness * .5f, r), Point(a, radius - thickness * .5f, r), color);
        }
    }

    // A straight bar from a to b, which is all a tick or a scowling brow
    // needs to be at this size.
    private static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 along = (b - a).normalized;
        Vector2 across = new Vector2(-along.y, along.x) * (thickness * .5f);
        Quad(vh, a + across, b + across, b - across, a - across, color);
    }

    private static Vector2 Point(float a, float radius, Rect r) => new Vector2(Mathf.Cos(a) * r.width * radius, Mathf.Sin(a) * r.height * radius);
    private static void Disk(VertexHelper vh, Vector2 center, Vector2 radius, Color color)
    {
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2 / 64, b = (i + 1) * Mathf.PI * 2 / 64;
            int n = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            vh.AddVert(center + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y), color, Vector2.zero);
            vh.AddVert(center + new Vector2(Mathf.Cos(b) * radius.x, Mathf.Sin(b) * radius.y), color, Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2);
        }
    }
    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int n = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
    }
}
