using System.Collections.Generic;
using System.Reflection;
using Sample;
using UnityEditor;
using UnityEngine;

// The guide book's words, and the numbers in them.
//
// Every number the book quotes is read off the game rather than typed in here:
// speeds and spawn tables off the scene, prices off ShopManager, durations off
// the managers and the trap prefab. Retune any of them and rebuilding the book
// is all it takes for the book to agree. What is typed here is what cannot be
// read - the names players call things by, the advice, how dangerous something
// feels - and GuideBookBuilder decides where all of it goes on the page.
internal static class GuideBookPages
{
    internal sealed class Chapter
    {
        public string Title, Intro, IconPath;
        public float IconPad;
        public readonly List<Page> Pages = new List<Page>();
    }

    internal sealed class Page
    {
        public string Name, Role, Tagline, Body, Tip;
        public Color RoleColour, Glow;
        public readonly List<KeyValuePair<string, int>> Meters = new List<KeyValuePair<string, int>>();
        public readonly List<Fact> Facts = new List<Fact>();

        // The picture on the left: a model filmed in 3D, or a drawn icon.
        public GameObject Model;           // a scene object or a prefab asset, copied onto the stage
        public Transform ModelFrame;       // the gameplay root the model faces forward from
        public Quaternion ModelTilt = Quaternion.identity;
        public Material DioramaLava, DioramaGround;
        public float Elevation = 20f, Zoom = 1f;
        public string IconPath;
        public float IconWidth = 190f;

        public bool Is3D => Model != null || DioramaLava != null;
    }

    internal struct Fact
    {
        public string Text;
        public bool Price;

        public Fact(string text, bool price = false)
        {
            Text = text;
            Price = price;
        }
    }

    // ---- the colours the roles are written in -------------------------------

    private static readonly Color You = Hex(0x22E5F0);
    private static readonly Color Hunter = Hex(0xFF5D73);
    private static readonly Color Wanderer = Hex(0xB58CFF);
    private static readonly Color Boss = Hex(0xFFB23E);
    private static readonly Color Friend = Hex(0x46E8A6);
    private static readonly Color Ability = Hex(0x6FB0FF);
    private static readonly Color Board = Hex(0xFFC93A);

    // ---- the enemies, by the scene object each kind is cloned from ----------

    private struct Foe
    {
        public string Template, Name, Role, Tagline, Body, Tip;
        public Color RoleColour, Glow;
        public int Danger;
    }

    // In the order the book introduces them. A kind in the spawn table with no
    // entry here is left out of the book, and the builder says so.
    private static readonly Foe[] Foes =
    {
        new Foe
        {
            Template = "EnemySpectralFrost", Name = "Frost Hunter", Role = "HUNTER",
            RoleColour = Hunter, Glow = Hex(0x59C8FF), Danger = 3,
            Tagline = "Cold, patient, and it always knows the shortest way to you.",
            Body = "The Frost Hunter works out the quickest route across the board and follows it " +
                   "without a single wrong turn. It is slow, but it never gets lost.",
            Tip = "Keep moving and stay out of dead ends. Given time, it will always find you."
        },
        new Foe
        {
            Template = "EnemySpectralHunter", Name = "Violet Wanderer", Role = "WANDERER",
            RoleColour = Wanderer, Glow = Hex(0xA27BFF), Danger = 2,
            Tagline = "It isn't hunting you, but it is still deadly to touch.",
            Body = "The Violet Wanderer drifts about the board on errands of its own, picking a spot " +
                   "and gliding there. It never aims for you, but bumping into it costs a life all the same.",
            Tip = "Watch which way it is heading and step aside. It is the one enemy you can walk around on purpose."
        },
        new Foe
        {
            Template = "EnemySpectralEmber", Name = "Ember Hunter", Role = "HUNTER",
            RoleColour = Hunter, Glow = Hex(0xFF6A3D), Danger = 3,
            Tagline = "Hot-headed and quick, it charges straight at you.",
            Body = "The Ember Hunter is faster than the Frost Hunter but never thinks ahead. At every turn " +
                   "it simply steps toward you, so it can charge into dead ends or get stuck behind lava.",
            Tip = "Put lava between you and it. It will press up against the far side instead of going round."
        },
        new Foe
        {
            Template = "EnemyGhoul", Name = "Apex Ghoul", Role = "BOSS",
            RoleColour = Boss, Glow = Hex(0xFFB23E), Danger = 5,
            Tagline = "The top of the food chain: fast and clever.",
            Body = "The Apex Ghoul is as clever as the Frost Hunter and faster than any other enemy. " +
                   "It takes the shortest path to you and closes the gap quickly.",
            Tip = "Save a Freeze or a Teleport for the moment it corners you."
        },
    };

    // How clever each way of moving is, out of five.
    private static int Smarts(EnemyChaser.PathingStrategy strategy)
    {
        switch (strategy)
        {
            case EnemyChaser.PathingStrategy.Optimal: return 5;
            case EnemyChaser.PathingStrategy.Greedy: return 2;
            default: return 1;
        }
    }

    // ---- composing the book ---------------------------------------------------

    public static List<Chapter> Compose()
    {
        var ghost = Object.FindAnyObjectByType<GhostScript>(FindObjectsInactive.Include);
        var level = Object.FindAnyObjectByType<LevelManager>(FindObjectsInactive.Include);
        var spawner = Object.FindAnyObjectByType<EnemySpawnManager>(FindObjectsInactive.Include);
        if (ghost == null || level == null || spawner == null)
        {
            Debug.LogError("[GuideBook] The open scene has no player, LevelManager or EnemySpawnManager to read " +
                           "the book's numbers off. Open the game scene first.");
            return null;
        }

        var levelData = new SerializedObject(level);
        float playerSpeed = Float(new SerializedObject(ghost), "Speed", 4f);
        int[] coinsByLevel = Ints(levelData, "coinsByLevel");
        Vector2Int[] boards = Boards(levelData);
        float lavaShare = Float(levelData, "lavaDensity", 0.27f);
        var friendly = levelData.FindProperty("friendlyGhost").objectReferenceValue as GameObject;
        int friendlyFrom = Int(levelData, "friendlyGhostFromLevel", 3);
        var coinPrefab = levelData.FindProperty("coinPrefab").objectReferenceValue as GameObject;
        var lava = levelData.FindProperty("lavaMaterial").objectReferenceValue as Material;
        var ground = levelData.FindProperty("blockMaterial").objectReferenceValue as Material;

        var spawnData = new SerializedObject(spawner);
        var portal = spawnData.FindProperty("portalPrefab").objectReferenceValue as GameObject;

        var foes = ReadFoes(spawnData);
        float fastestFoe = 0f;
        foreach (var foe in foes) fastestFoe = Mathf.Max(fastestFoe, foe.Speed);

        var flee = friendly != null ? friendly.GetComponent<FriendlyGhostFlee>() : null;
        var fleeData = flee != null ? new SerializedObject(flee) : null;
        float friendlySpeed = Float(fleeData, "speed", 2f);

        // Speeds are shown against everything on the board, slowest to fastest.
        float slowest = Mathf.Min(playerSpeed, friendlySpeed), fastest = Mathf.Max(playerSpeed, friendlySpeed);
        foreach (var foe in foes)
        {
            slowest = Mathf.Min(slowest, foe.Speed);
            fastest = Mathf.Max(fastest, foe.Speed);
        }
        int Pips(float speed) => SpeedPips(speed, slowest, fastest);

        int startLives = DifficultySettings.AuthoredStartingLives;
        int normalLives = DifficultySettings.NormalStartingLives;
        int maxLives = LivesManager.HardCap;
        int normalSpeed = Mathf.RoundToInt(DifficultySettings.NormalEnemySpeedMultiplier * 100f);
        int ghoulFrom = 0;

        var book = new List<Chapter>();

        // ---- characters ----

        var characters = new Chapter
        {
            Title = "CHARACTERS",
            Intro = "Who's who on the board. On Normal, every enemy moves at " + normalSpeed +
                    "% of the speed shown here.",
            IconPath = "Assets/UI/Icons/Settings/icon_ghost.png",
            IconPad = 6f
        };
        book.Add(characters);

        var warden = new Page
        {
            Name = "Lantern Warden", Role = "YOU", RoleColour = You, Glow = Hex(0xFF9A3C),
            Tagline = "A little lantern spirit on a coin-collecting mission.",
            Body = "Steer with the joystick and collect every coin on the board to clear the level. " +
                   "Keep off the lava and out of the enemies' reach: touching either one costs a life.",
            Tip = playerSpeed > fastestFoe
                ? "You are faster than every enemy. When one gets close, lead it the long way round a pool of lava."
                : "When an enemy gets close, lead it the long way round a pool of lava.",
            Model = WardenVisual(ghost), ModelFrame = ghost.transform, Elevation = 18f
        };
        warden.Meters.Add(new KeyValuePair<string, int>("SPEED", Pips(playerSpeed)));
        if (playerSpeed > fastestFoe) warden.Facts.Add(new Fact("Fastest on the board"));
        warden.Facts.Add(new Fact("Starts with " + startLives + " lives"));
        warden.Facts.Add(new Fact(normalLives + " lives on Normal"));
        warden.Facts.Add(new Fact("Holds up to " + maxLives));
        characters.Pages.Add(warden);

        foreach (Foe words in Foes)
        {
            FoeData data = foes.Find(f => f.Template != null && f.Template.name == words.Template);
            if (data.Template == null) continue;

            var page = new Page
            {
                Name = words.Name, Role = words.Role, RoleColour = words.RoleColour, Glow = words.Glow,
                Tagline = words.Tagline, Body = words.Body, Tip = words.Tip,
                Model = VisualOf(data.Template), ModelFrame = data.Template.transform
            };
            page.Meters.Add(new KeyValuePair<string, int>("SPEED", Pips(data.Speed)));
            page.Meters.Add(new KeyValuePair<string, int>("SMARTS", Smarts(data.Strategy)));
            page.Meters.Add(new KeyValuePair<string, int>("DANGER", words.Danger));

            int from = FirstLevel(data.Counts);
            int most = Max(data.Counts);
            page.Facts.Add(new Fact(from <= 1 ? "On every level" : "From level " + from));
            page.Facts.Add(new Fact(most <= 1 ? "1 at a time"
                                    : Min(data.Counts, from) == most ? most + " at a time"
                                    : "Up to " + most + " at once"));
            characters.Pages.Add(page);

            if (words.Template == "EnemyGhoul") ghoulFrom = from;
        }

        if (friendly != null)
        {
            int reward = Int(fleeData, "catchReward", 1000);
            var page = new Page
            {
                Name = "Friendly Spectral", Role = "FRIEND", RoleColour = Friend, Glow = Friend,
                Tagline = "A shy, smiling ghost worth a small fortune.",
                Body = "From level " + friendlyFrom + " a Friendly Spectral joins the board, once a level. " +
                       "It is harmless and scared of you, and always runs for the tile furthest away. " +
                       "Catch it for " + reward + " coins, the biggest prize on the board.",
                Tip = "Herd it into a corner or a dead end, where it has nowhere left to run.",
                Model = VisualOf(friendly), ModelFrame = friendly.transform
            };
            page.Meters.Add(new KeyValuePair<string, int>("SPEED", Pips(friendlySpeed)));
            page.Facts.Add(new Fact("+" + reward, true));
            page.Facts.Add(new Fact("From level " + friendlyFrom));
            page.Facts.Add(new Fact("Once per level"));
            page.Facts.Add(new Fact("Harmless"));
            characters.Pages.Add(page);
        }

        // ---- abilities ----

        var abilities = new Chapter
        {
            Title = "ABILITIES",
            Intro = "Buy charges in the Shop with the coins in your wallet, then tap them on the ability bar " +
                    "while you play. Charges you don't use carry over to your next run.",
            IconPath = "Assets/UI/Icons/Shop/tile_trap.png"
        };
        book.Add(abilities);

        var trapManager = Object.FindAnyObjectByType<TrapManager>(FindObjectsInactive.Include);
        var trapPrefab = trapManager != null
            ? new SerializedObject(trapManager).FindProperty("trapPrefab").objectReferenceValue as GameObject
            : null;
        var trap = trapPrefab != null ? trapPrefab.GetComponent<Trap>() : null;
        float stun = Float(trap != null ? new SerializedObject(trap) : null, "stunDuration", 4f);
        float freeze = Float(Data<FreezeManager>(), "freezeDuration", 5f);
        float shield = Float(Data<ShieldManager>(), "shieldDuration", 5f);
        float jump = Float(Data<TeleportManager>(), "minJumpDistance", 5f);

        abilities.Pages.Add(AbilityPage("Trap", "tile_trap", Hex(0xB06CFF), Shop("TrapCost", 400),
            "A snare for whoever is right behind you.",
            "Drops a trap on the tile you are standing on. The first enemy to step on it is stuck there for " +
            Seconds(stun) + ", and the trap is used up. Traps still waiting when the level ends are lost.",
            "Lay one in a narrow corridor while something is chasing you.",
            "Stops 1 enemy", Seconds(stun)));

        abilities.Pages.Add(AbilityPage("Freeze", "tile_freeze", Hex(0x59D8FF), Shop("FreezeCost", 1500),
            "Stop the whole board in its tracks.",
            "Encases every enemy on the board in ice for " + Seconds(freeze) + ". During the countdown at the " +
            "start of a level there is nothing to freeze yet, so the button waits and keeps your charge.",
            "Use it to grab a coin that a hunter is sitting right next to.",
            "Every enemy", Seconds(freeze)));

        abilities.Pages.Add(AbilityPage("Teleport", "tile_teleport", Hex(0xC77DFF), Shop("TeleportCost", 2500),
            "Your emergency exit.",
            "Lifts you off the board and sets you down on a random safe tile: never lava, at least " +
            Mathf.RoundToInt(jump) + " tiles from where you were, and as far from the enemies as the board allows.",
            "It is the priciest charge, so keep one for the Apex Ghoul.",
            "Safe landing", null));

        abilities.Pages.Add(AbilityPage("Shield", "tile_shield", Hex(0x4FE08A), Shop("ShieldCost", 1000),
            "A few seconds when nothing can touch you.",
            "Wraps you in a bubble for " + Seconds(shield) + ". Enemies can't catch you, and stepping into lava " +
            "just puts you back on the edge instead of costing a life. The bubble pulses faster just before it runs out.",
            "Walk straight past a hunter that is blocking the only way to a coin.",
            Seconds(shield), null));

        int lifeEarly = Shop("EarlyLevelCost", 1000), lifeLate = Shop("LateLevelCost", 2000);
        int lifeUntil = Shop("EarlyLevelThreshold", 5);
        var life = AbilityPage("Extra Life", "tile_extralife", Hex(0xE59BFF), lifeEarly,
            "One more try, straight away.",
            "Gives you back one life right now, up to " + maxLives + ". It costs " + lifeEarly + " coins up to level " +
            lifeUntil + " and " + lifeLate + " after that.",
            "Buy it before a tough level, not after the last life has gone.",
            lifeLate + " after level " + lifeUntil, "Up to " + maxLives + " lives");
        life.Role = "SHOP";
        abilities.Pages.Add(life);

        // ---- the board ----

        var board = new Chapter
        {
            Title = "THE BOARD",
            Intro = "How a level works, and what it is made of.",
            IconPath = "Assets/UI/Icons/coin_icon.png"
        };
        book.Add(board);

        int[] values = CoinValues(coinPrefab);
        var coins = new Page
        {
            Name = "Coins", Role = "GOAL", RoleColour = Board, Glow = Board,
            Tagline = "Collect them all to clear the level.",
            Body = "Every coin on the board has to be collected to finish the level. Each one adds " +
                   Min(values, 0) + " to " + Max(values) + " coins to your wallet (" +
                   DifficultySettings.NormalCoinWalletValue + " every time on Normal). When only one is left, " +
                   "a golden arrow points the way to it.",
            Tip = "Pick up the coins in dead ends while the way out is clear, not last.",
            // Drawn lying face up, for the camera straight over the board.
            Model = coinPrefab, Elevation = 58f, Zoom = 1.45f
        };
        if (coinsByLevel.Length > 0)
        {
            coins.Facts.Add(new Fact(coinsByLevel[0] + " on level 1"));
            if (Max(coinsByLevel) > coinsByLevel[0]) coins.Facts.Add(new Fact("Up to " + Max(coinsByLevel)));
        }
        board.Pages.Add(coins);

        var lavaPage = new Page
        {
            Name = "Lava", Role = "HAZARD", RoleColour = Hex(0xFF6A2B), Glow = Hex(0xFF6A2B),
            Tagline = "The floor you can't stand on.",
            Body = "About " + Share(lavaShare) + " of the board is lava, and it moves every level. Step in and you " +
                   "lose a life. Enemies can't cross it either, so a pool of lava is a wall between you and them.",
            Tip = "Circle a pool of lava to shake off whatever is following you.",
            DioramaLava = lava, DioramaGround = ground, Elevation = 48f, Zoom = 1.05f
        };
        lavaPage.Facts.Add(new Fact("Moves every level"));
        board.Pages.Add(lavaPage);

        var portals = new Page
        {
            Name = "Portals", Role = "START", RoleColour = Wanderer, Glow = Hex(0x9B6BFF),
            Tagline = "Where the enemies come from.",
            Body = "At the start of every level a portal opens wherever an enemy is about to appear. Nobody can " +
                   "move until the countdown ends, so use those seconds to plan your first steps.",
            Tip = "Find the portals nearest to you and head the other way first.",
            Model = portal, ModelTilt = Quaternion.Euler(90f, 0f, 0f), Elevation = 72f, Zoom = 1.4f
        };
        portals.Facts.Add(new Fact("Every level start"));
        board.Pages.Add(portals);

        var lives = new Page
        {
            Name = "Lives", Role = "LIVES", RoleColour = Hex(0xFF8FC8), Glow = Hex(0xFF8FC8),
            Tagline = "How many chances you have left.",
            Body = "You start with " + startLives + " lives (" + normalLives + " on Normal) and can hold up to " +
                   maxLives + ". Lava and enemies each take one. Run out and you can watch an ad to carry on " +
                   "with one life, or go back to the menu: your level, coins and abilities are saved either way.",
            Tip = "Top up with an Extra Life from the Shop before the harder levels.",
            IconPath = "Assets/UI/Icons/life_ghost.png", IconWidth = 170f
        };
        lives.Facts.Add(new Fact("Start with " + startLives));
        lives.Facts.Add(new Fact("Up to " + maxLives));
        board.Pages.Add(lives);

        string growth = boards.Length > 0
            ? "makes the board bigger, from " + Size(boards[0]) + " tiles up to " + Size(boards[boards.Length - 1]) + ", "
            : "";
        var levels = new Page
        {
            Name = "Levels", Role = "PROGRESS", RoleColour = Ability, Glow = Hex(0x4E8BE0),
            Tagline = "The maze grows with you.",
            Body = "Each new level " + growth + "lays the lava out fresh and brings in more enemies. From level " +
                   friendlyFrom + " the Friendly Spectral appears" +
                   (ghoulFrom > 1 ? ", and from level " + ghoulFrom + " the Apex Ghoul joins the hunt." : "."),
            Tip = "Your run is saved as you go: Continue on the main menu picks up exactly where you left off.",
            IconPath = "Assets/UI/Icons/level_badge.png", IconWidth = 280f
        };
        if (boards.Length > 0) levels.Facts.Add(new Fact(Size(boards[0]) + " to " + Size(boards[boards.Length - 1])));
        board.Pages.Add(levels);

        return book;
    }

    private static Page AbilityPage(string name, string tile, Color glow, int cost, string tagline, string body,
                                    string tip, string fact1, string fact2)
    {
        var page = new Page
        {
            Name = name, Role = "ABILITY", RoleColour = Ability, Glow = glow,
            Tagline = tagline, Body = body, Tip = tip,
            IconPath = "Assets/UI/Icons/Shop/" + tile + ".png", IconWidth = 176f
        };
        page.Facts.Add(new Fact(cost.ToString(), true));
        if (!string.IsNullOrEmpty(fact1)) page.Facts.Add(new Fact(fact1));
        if (!string.IsNullOrEmpty(fact2)) page.Facts.Add(new Fact(fact2));
        return page;
    }

    // ---- where the models come from -----------------------------------------

    // The player's character is whichever visual under the Ghost carries the
    // Warden's appearance - the root itself holds the gameplay, not the look.
    private static GameObject WardenVisual(GhostScript ghost)
    {
        foreach (var appearance in ghost.GetComponentsInChildren<WardenAppearance>(true))
        {
            Transform step = appearance.transform;
            while (step.parent != null && step.parent != ghost.transform) step = step.parent;
            if (step.gameObject.activeSelf) return step.gameObject;
        }
        return VisualOf(ghost.gameObject);
    }

    // A gameplay root either carries its own Animator, in which case the whole
    // thing is the look, or it wears the look as a child that does - the ghoul
    // and the friendly ghost, whose earlier visuals are kept switched off beside
    // the current one.
    private static GameObject VisualOf(GameObject root)
    {
        var own = root.GetComponent<Animator>();
        if (own != null && own.enabled) return root;
        foreach (Transform child in root.transform)
        {
            var animator = child.GetComponent<Animator>();
            if (child.gameObject.activeSelf && animator != null && animator.enabled) return child.gameObject;
        }
        return root;
    }

    // ---- reading the numbers ---------------------------------------------------

    private struct FoeData
    {
        public GameObject Template;
        public EnemyChaser.PathingStrategy Strategy;
        public float Speed;
        public int[] Counts;
    }

    private static List<FoeData> ReadFoes(SerializedObject spawner)
    {
        var found = new List<FoeData>();
        SerializedProperty kinds = spawner.FindProperty("enemyKinds");
        for (int i = 0; kinds != null && i < kinds.arraySize; i++)
        {
            SerializedProperty kind = kinds.GetArrayElementAtIndex(i);
            var template = kind.FindPropertyRelative("template").objectReferenceValue as GameObject;
            if (template == null) continue;
            var chaser = template.GetComponent<EnemyChaser>();
            var data = chaser != null ? new SerializedObject(chaser) : null;

            var counts = new List<int>();
            SerializedProperty table = kind.FindPropertyRelative("countByLevel");
            for (int n = 0; n < table.arraySize; n++) counts.Add(table.GetArrayElementAtIndex(n).intValue);

            found.Add(new FoeData
            {
                Template = template,
                Strategy = data != null
                    ? (EnemyChaser.PathingStrategy)data.FindProperty("strategy").enumValueIndex
                    : EnemyChaser.PathingStrategy.Optimal,
                Speed = Float(data, "speed", 2f),
                Counts = counts.ToArray()
            });

            if (System.Array.FindIndex(Foes, f => f.Template == template.name) < 0)
                Debug.LogWarning("[GuideBook] The spawn table fields " + template.name + ", which the book has no " +
                                 "page for. Give it one in GuideBookPages.Foes.");
        }
        return found;
    }

    private static int Shop(string constant, int fallback)
    {
        FieldInfo field = typeof(ShopManager).GetField(constant, BindingFlags.Static | BindingFlags.NonPublic |
                                                                 BindingFlags.Public);
        if (field != null && field.IsLiteral) return (int)field.GetRawConstantValue();
        Debug.LogWarning("[GuideBook] ShopManager has no " + constant + "; the book quotes " + fallback + ".");
        return fallback;
    }

    private static SerializedObject Data<T>() where T : Object
    {
        var found = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        return found != null ? new SerializedObject(found) : null;
    }

    private static int[] CoinValues(GameObject coinPrefab)
    {
        var coin = coinPrefab != null ? coinPrefab.GetComponent<Coin>() : null;
        int[] values = Ints(coin != null ? new SerializedObject(coin) : null, "walletValues");
        return values.Length > 0 ? values : new[] { 50, 100, 150, 200 };
    }

    private static Vector2Int[] Boards(SerializedObject level)
    {
        SerializedProperty table = level.FindProperty("boardSizeByLevel");
        var sizes = new Vector2Int[table != null ? table.arraySize : 0];
        for (int i = 0; i < sizes.Length; i++) sizes[i] = table.GetArrayElementAtIndex(i).vector2IntValue;
        return sizes;
    }

    private static float Float(SerializedObject data, string name, float fallback)
    {
        SerializedProperty p = data?.FindProperty(name);
        return p != null ? p.floatValue : fallback;
    }

    private static int Int(SerializedObject data, string name, int fallback)
    {
        SerializedProperty p = data?.FindProperty(name);
        return p != null ? p.intValue : fallback;
    }

    private static int[] Ints(SerializedObject data, string name)
    {
        SerializedProperty p = data?.FindProperty(name);
        if (p == null || !p.isArray) return new int[0];
        var values = new int[p.arraySize];
        for (int i = 0; i < values.Length; i++) values[i] = p.GetArrayElementAtIndex(i).intValue;
        return values;
    }

    // ---- saying the numbers ----------------------------------------------------

    // Speed as five beads, from the slowest thing on the board to the fastest.
    // The speeds sit close together - one and a half to three tiles a second -
    // and measured up from nothing they would all fill the same beads.
    private static int SpeedPips(float speed, float slowest, float fastest)
    {
        if (fastest - slowest < 0.01f) return 3;
        return Mathf.Clamp(1 + Mathf.RoundToInt((speed - slowest) / (fastest - slowest) * 4f), 1, 5);
    }

    private static int FirstLevel(int[] counts)
    {
        for (int i = 0; i < counts.Length; i++)
            if (counts[i] > 0) return i + 1;
        return 1;
    }

    private static int Max(int[] values)
    {
        int most = 0;
        foreach (int v in values) most = Mathf.Max(most, v);
        return most;
    }

    // The smallest value from a given index on - for a spawn table, the fewest
    // of a kind there are once it has started turning up at all.
    private static int Min(int[] values, int fromLevel)
    {
        int least = int.MaxValue;
        for (int i = Mathf.Max(0, fromLevel - 1); i < values.Length; i++) least = Mathf.Min(least, values[i]);
        return least == int.MaxValue ? 0 : least;
    }

    private static string Seconds(float s) => (Mathf.Approximately(s, Mathf.Round(s)) ? Mathf.RoundToInt(s).ToString()
                                                                                        : s.ToString("0.#")) + " seconds";

    private static string Size(Vector2Int size) => size.x + " x " + size.y;

    private static string Share(float share)
    {
        if (share > 0.2f && share < 0.3f) return "a quarter";
        if (share > 0.3f && share < 0.37f) return "a third";
        return Mathf.RoundToInt(share * 100f) + "%";
    }

    private static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
