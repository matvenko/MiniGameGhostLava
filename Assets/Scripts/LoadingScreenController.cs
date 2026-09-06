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
    private RectTransform safeRoot, hero, orbit, fill, playRect;
    private CanvasGroup entrance;
    private Image curtain;
    private TextMeshProUGUI percent, caption, soundLabel, playLabel, modeHint;
    private Button play, normalPill, hardPill;
    private AudioSource music;
    private AudioClip readySound, startSound;
    private RectTransform[] embers;
    private float age, shownProgress;
    private bool ready, leaving;
    private TMP_FontAsset font;
    private Rect lastSafeArea;
    private int lastWidth, lastHeight;
    private RenderTexture characterTexture;
    private Transform characterPivot, portraitStage, portraitFraming;
    private Camera characterCamera;
    private Renderer[] portraitRenderers;
    private bool portraitFramed;

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
        var root = new GameObject("Pac Ghost Intro", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        titleBlock = Rect(design, "Logo", new Vector2(0,330), new Vector2(1000,220));
        var tag = Rounded(titleBlock, "Adventure badge", new Vector2(0,100), new Vector2(310,38), new Color(.25f,.18f,.49f));
        Label(tag.transform,"A LITTLE BOO. A BIG ADVENTURE!",Vector2.zero,new Vector2(300,36),17,new Color(.88f,.81f,1));
        LogoWord(titleBlock,"PAC",new Vector2(-195,9),new Vector2(350,160),new Color(1,.83f,.24f), -5);
        LogoWord(titleBlock,"GHOST",new Vector2(143,-3),new Vector2(540,160),new Color(.98f,.92f,1), 3);
        Label(titleBlock,"Ready, set... BOO!",new Vector2(0,-99),new Vector2(700,50),29,new Color(.86f,.81f,1));

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
        var bubble=Rounded(heroBlock,"Hello bubble",new Vector2(215,163),new Vector2(135,70),new Color(1,.96f,.85f));
        Label(bubble.transform,"Boo!",Vector2.zero,new Vector2(125,65),32,new Color(.40f,.20f,.6f));
        bubble.rectTransform.localRotation=Quaternion.Euler(0,0,9);
        hero = Rect(heroBlock, "Main character portrait", new Vector2(0,12), new Vector2(350,350));
        BuildCharacterPortrait();
        var leftGhost=Shape(heroBlock,"Little blue friend",new Vector2(-430,-84),new Vector2(85,105),new Color(.46f,.89f,1),2);
        leftGhost.rectTransform.localRotation=Quaternion.Euler(0,0,-13);
        var rightGhost=Shape(heroBlock,"Little peach friend",new Vector2(423,-51),new Vector2(70,90),new Color(1,.63f,.68f),2);
        rightGhost.rectTransform.localRotation=Quaternion.Euler(0,0,16);

        controls=Rect(design,"Play area",new Vector2(0,-320),new Vector2(620,260));
        caption=Label(controls,"Getting the fun ready...",new Vector2(-52,112),new Vector2(385,36),20,new Color(.89f,.85f,1));
        caption.alignment=TextAlignmentOptions.Left;
        percent=Label(controls,"0%",new Vector2(214,112),new Vector2(80,36),20,new Color(1,.86f,.38f));
        percent.alignment=TextAlignmentOptions.Right;
        var track=Rounded(controls,"Progress border",new Vector2(0,74),new Vector2(520,22),new Color(.22f,.13f,.40f));
        var inner=Rounded(track.transform,"Progress well",Vector2.zero,new Vector2(510,12),new Color(.31f,.20f,.49f));
        fill=Rounded(inner.transform,"Progress",Vector2.zero,Vector2.zero,new Color(.55f,.96f,.72f)).rectTransform;
        fill.anchorMin=Vector2.zero; fill.anchorMax=new Vector2(0,1); fill.offsetMin=fill.offsetMax=Vector2.zero;
        // The mode is picked before the run starts and nowhere else, so it sits
        // on the way to the play button rather than behind a settings screen.
        normalPill=ModePill(controls,"NORMAL",-108,Difficulty.Normal);
        hardPill=ModePill(controls,"HARD",108,Difficulty.Hard);
        Rounded(controls,"Button shadow",new Vector2(0,-71),new Vector2(420,92),new Color(.55f,.24f,.09f));
        play=MakeButton(controls,"Play",new Vector2(0,-60),new Vector2(420,94),new Color(1,.76f,.21f),BeginGame);
        var buttonImage=play.GetComponent<Image>(); buttonImage.sprite=roundSprite; buttonImage.type=Image.Type.Sliced;
        Rounded(play.transform,"Button shine",new Vector2(0,29),new Vector2(362,9),new Color(1,.91f,.48f));
        playRect=play.GetComponent<RectTransform>();
        playLabel=Label(play.transform,"LOADING...",new Vector2(0,-2),new Vector2(400,75),40,new Color(.35f,.20f,.15f));
        play.interactable=false;
        modeHint=Label(controls,"",new Vector2(0,-136),new Vector2(760,35),20,new Color(.83f,.77f,.95f));
        RefreshModePills();
        var sound=MakeButton(design,"Sound",Vector2.zero,new Vector2(160,46),new Color(.25f,.17f,.45f),ToggleSound);
        sound.GetComponent<Image>().sprite=roundSprite; sound.GetComponent<Image>().type=Image.Type.Sliced;
        sound.name="Sound control";
        soundLabel=Label(sound.transform,"",Vector2.zero,new Vector2(150,40),17,new Color(.92f,.86f,1));
        RefreshSoundLabel();
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
        titleBlock.anchoredPosition=new Vector2(0,portrait?490:330);
        titleBlock.localScale=Vector3.one*(portrait?.95f:1);
        heroBlock.anchoredPosition=new Vector2(0,portrait?40:5);
        heroBlock.localScale=Vector3.one*(portrait?.90f:1.1f);
        controls.anchoredPosition=new Vector2(0,portrait?-410:-320);
        design.Find("Sound control").GetComponent<RectTransform>().anchoredPosition=portrait?new Vector2(0,-620):new Vector2(665,418);
    }

    // One of the two mode pills. Both are built from the same call so they can
    // never drift apart in size or wording, and the pair is redrawn from what
    // DifficultySettings says rather than from which one was last pressed.
    private Button ModePill(Transform parent,string word,float x,Difficulty mode)
    {
        var pill=MakeButton(parent,word,new Vector2(x,22),new Vector2(200,54),Color.white,()=>ChooseMode(mode));
        var image=pill.GetComponent<Image>();
        image.sprite=roundSprite; image.type=Image.Type.Sliced;
        var label=Label(pill.transform,word,new Vector2(0,-1),new Vector2(190,50),24,Color.white);
        label.name="Mode word";
        return pill;
    }

    private void ChooseMode(Difficulty mode)
    {
        DifficultySettings.Current=mode;
        RefreshModePills();
        PlayEffect(readySound);
    }

    private void RefreshModePills()
    {
        if (normalPill==null||hardPill==null) return;
        bool normal=DifficultySettings.IsNormal;
        Dress(normalPill,normal);
        Dress(hardPill,!normal);
        modeHint.text=normal
            ? "Normal: slower ghosts, kinder coins and a spare life - just right for little players."
            : "Hard: full-speed ghosts and the coins as they fall. The grown-up chase!";
    }

    private void Dress(Button pill,bool picked)
    {
        var image=pill.GetComponent<Image>();
        image.color=picked?new Color(.55f,.96f,.72f):new Color(.25f,.17f,.45f);
        var colors=pill.colors;
        colors.highlightedColor=picked?new Color(.70f,1f,.82f):new Color(.34f,.24f,.58f);
        colors.pressedColor=new Color(.80f,.80f,.80f);
        pill.colors=colors;
        var label=pill.transform.Find("Mode word").GetComponent<TextMeshProUGUI>();
        label.color=picked?new Color(.10f,.24f,.18f):new Color(.86f,.81f,1f);
        label.fontStyle=picked?FontStyles.Bold:FontStyles.Normal;
    }

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
        characterTexture.name = "Pac Ghost character portrait";
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
        characterCamera.transform.position = focus + new Vector3(0, half * .18f, reach * 4);
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
            caption.text = "SCENE UNAVAILABLE";
            playLabel.text = "ADD LAVASCENE TO BUILD SETTINGS";
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
            percent.text = Mathf.RoundToInt(shownProgress * 100).ToString("00") + "%";
            caption.text = shownProgress < .35f ? "Packing the adventure..." : shownProgress < .75f ? "Counting shiny coins..." : "Waking your little ghost...";
            yield return null;
        }
        fill.anchorMax = Vector2.one;
        percent.text = "100%";
        caption.text = "Your adventure is ready!";
        playLabel.text = "LET'S PLAY!";
        ready = true;
        play.interactable = true;
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
        if (pressed && ready && !leaving) BeginGame();
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

    private void BeginGame()
    {
        if (!ready || leaving || load == null) return;
        leaving = true;
        ready = false;
        play.interactable = false;
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
        var colors = button.colors;
        colors.highlightedColor = new Color(1, .88f, .73f);
        colors.pressedColor = new Color(.75f, .75f, .75f);
        colors.disabledColor = new Color(.45f, .45f, .45f, .7f);
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
                Quad(vh, Point(a, .5f, r), Point(b, .5f, r), Point(b, .49f, r), Point(a, .49f, r), color);
            }
        }
        else if (kind == 2)
        {
            // Rounded crown, scalloped hem and dark oval eyes.
            Disk(vh, new Vector2(0, r.height * .10f), new Vector2(r.width * .45f, r.height * .38f), color);
            Quad(vh, new Vector2(-r.width * .45f, -r.height * .28f), new Vector2(r.width * .45f, -r.height * .28f), new Vector2(r.width * .45f, r.height * .1f), new Vector2(-r.width * .45f, r.height * .1f), color);
            for (int i = 0; i < 3; i++) Disk(vh, new Vector2((i - 1) * r.width * .3f, -r.height * .27f), new Vector2(r.width * .15f, r.height * .14f), color);
            Color eye = new Color(.055f, .09f, .13f);
            Disk(vh, new Vector2(-r.width * .16f, r.height * .1f), new Vector2(r.width * .055f, r.height * .08f), eye);
            Disk(vh, new Vector2(r.width * .16f, r.height * .1f), new Vector2(r.width * .055f, r.height * .08f), eye);
        }
        else Disk(vh, Vector2.zero, r.size * .5f, color);
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
