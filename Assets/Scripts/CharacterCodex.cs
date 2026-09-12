using UnityEngine;

// Who is who on the board, in the words players read about them.
//
// Two things introduce the characters - the guide book behind Game Info, and the
// first-time tour that stops the board the first time one of them turns up - and
// both say the same thing about each. The words live here, once, rather than in
// the book's editor code, where the running game could not reach them.
//
// Enemies are keyed by the name of the scene object their kind is cloned from
// (see EnemySpawnManager), which is what both readers have in hand. The numbers
// the book quotes beside these - speeds, levels, prices - are still read off the
// game by the book itself; nothing here is a number that could go stale.
public static class CharacterCodex
{
    public sealed class Entry
    {
        public string Key, Name, Role, Tagline, Body, Tip;
        public Color RoleColour, Glow;
        public int Danger;
    }

    // ---- the colours the roles are written in -------------------------------

    public static readonly Color HunterInk = Hex(0xFF5D73);
    public static readonly Color WandererInk = Hex(0xB58CFF);
    public static readonly Color BossInk = Hex(0xFFB23E);
    public static readonly Color FriendInk = Hex(0x46E8A6);

    // In the order the book introduces them, which is also the order the tour
    // walks the board in. A kind in the spawn table with no entry here is left
    // out of both, and the book's builder says so.
    public static readonly Entry[] Enemies =
    {
        new Entry
        {
            Key = "EnemySpectralFrost", Name = "Frost Hunter", Role = "HUNTER",
            RoleColour = HunterInk, Glow = Hex(0x59C8FF), Danger = 3,
            Tagline = "Cold, patient, and it always knows the shortest way to you.",
            Body = "The Frost Hunter works out the quickest route across the board and follows it " +
                   "without a single wrong turn. It is slow, but it never gets lost.",
            Tip = "Keep moving and stay out of dead ends. Given time, it will always find you."
        },
        new Entry
        {
            Key = "EnemySpectralHunter", Name = "Violet Wanderer", Role = "WANDERER",
            RoleColour = WandererInk, Glow = Hex(0xA27BFF), Danger = 2,
            Tagline = "It isn't hunting you, but it is still deadly to touch.",
            Body = "The Violet Wanderer drifts about the board on errands of its own, picking a spot " +
                   "and gliding there. It never aims for you, but bumping into it costs a life all the same.",
            Tip = "Watch which way it is heading and step aside. It is the one enemy you can walk around on purpose."
        },
        new Entry
        {
            Key = "EnemySpectralEmber", Name = "Ember Hunter", Role = "HUNTER",
            RoleColour = HunterInk, Glow = Hex(0xFF6A3D), Danger = 3,
            Tagline = "Hot-headed and quick, it charges straight at you.",
            Body = "The Ember Hunter is faster than the Frost Hunter but never thinks ahead. At every turn " +
                   "it simply steps toward you, so it can charge into dead ends or get stuck behind lava.",
            Tip = "Put lava between you and it. It will press up against the far side instead of going round."
        },
        new Entry
        {
            Key = "EnemyGhoul", Name = "Apex Ghoul", Role = "BOSS",
            RoleColour = BossInk, Glow = Hex(0xFFB23E), Danger = 5,
            Tagline = "The top of the food chain: fast and clever.",
            Body = "The Apex Ghoul is as clever as the Frost Hunter and faster than any other enemy. " +
                   "It takes the shortest path to you and closes the gap quickly.",
            Tip = "Save a Freeze or a Teleport for the moment it corners you."
        },
        new Entry
        {
            Key = "EnemyAshWarden", Name = "Ash Warden", Role = "HUNTER",
            RoleColour = HunterInk, Glow = Hex(0xFF8A4C), Danger = 4,
            Tagline = "A smouldering guard from the Burnt Lands.",
            Body = "The Ash Warden plans its route as carefully as the Frost Hunter, but moves as fast as " +
                   "the Apex Ghoul. It never takes a wrong turn, and it does not slow down.",
            Tip = "Keep a pool of lava between you, and a Freeze ready for when it closes in."
        },
        new Entry
        {
            Key = "EnemyWhiteApex", Name = "White Apex", Role = "BOSS",
            RoleColour = BossInk, Glow = Hex(0xDDE8FF), Danger = 5,
            Tagline = "Pale, silent, and the deadliest thing in the maze.",
            Body = "The White Apex hunts you down the shortest path at top speed. It is every bit as clever " +
                   "as the Apex Ghoul and just as quick.",
            Tip = "Keep a Teleport in reserve. When it corners you, that is your way out."
        },
    };

    public const string FriendlyKey = "FriendlyGhost";

    public static readonly Entry Friendly = new Entry
    {
        Key = FriendlyKey, Name = "Friendly Spectral", Role = "FRIEND",
        RoleColour = FriendInk, Glow = FriendInk,
        Tagline = "A shy, smiling ghost worth a small fortune.",
        Tip = "Herd it into a corner or a dead end, where it has nowhere left to run."
    };

    // Its description quotes the reward, which the scene decides.
    public static string FriendlyBody(int reward) =>
        "It is harmless and scared of you, and always runs for the tile furthest away. " +
        "Catch it for " + reward + " coins, the biggest prize on the board.";

    public static Entry Find(string key)
    {
        foreach (var entry in Enemies)
            if (entry.Key == key) return entry;
        return key == FriendlyKey ? Friendly : null;
    }

    private static Color Hex(int rgb) =>
        new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
}
