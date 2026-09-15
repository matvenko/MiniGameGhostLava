using UnityEngine;

// Cosmetics share the permanent Boo Gem vault, across difficulties and new runs.
public static class CharacterCollection
{
    public static readonly string[] Ids = { "warden", "fox" };
    public static readonly string[] Names = { "LANTERN WARDEN", "FOX ROBOT" };
    public static readonly int[] Prices = { 0, 12 };
    public static readonly string[] WardenSkinIds = { "warden.apricot", "warden.ice", "warden.midnight", "warden.ivory" };
    public static readonly string[][] ColorNames = { new[] { "APRICOT", "FROST", "MIDNIGHT", "IVORY" }, new[] { "CORAL", "ARCTIC", "LAVENDER", "MINT" } };
    public static event System.Action Changed;
    public static bool Owned(int index) => index >= 0 && index < Ids.Length && (index == 0 || PlayerPrefs.GetInt("characters.owned." + Ids[index], 0) == 1);
    public static int Selected
    {
        get { int i = System.Array.IndexOf(Ids, PlayerPrefs.GetString("characters.selected", Ids[0])); return Owned(i) ? i : 0; }
    }
    // Old saves stored the Warden color as the selected character ID.
    public static int SelectedColor(int character)
    {
        if (character < 0 || character >= Ids.Length) return 0;
        string key = "characters.color." + Ids[character];
        int legacy = character == 0 ? System.Array.IndexOf(WardenSkinIds, PlayerPrefs.GetString("characters.selected", "")) : 0;
        return Mathf.Clamp(PlayerPrefs.GetInt(key, Mathf.Max(0, legacy)), 0, 3);
    }
    public static bool SelectColor(int character, int color)
    {
        if (!Owned(character) || color < 0 || color >= 4) return false;
        PlayerPrefs.SetInt("characters.color." + Ids[character], color);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }
    public static bool Unlock(int index)
    {
        if (index <= 0 || index >= Ids.Length || Owned(index)) return false;
        var wallet = EconomyManager.Instance;
        if (wallet == null || !wallet.SpendBooGems(Prices[index])) return false;
        PlayerPrefs.SetInt("characters.owned." + Ids[index], 1);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }
    public static bool Select(int index)
    {
        if (!Owned(index)) return false;
        // Preserve a legacy Warden color before replacing the old selection ID.
        PlayerPrefs.SetInt("characters.color.warden", SelectedColor(0));
        PlayerPrefs.SetString("characters.selected", Ids[index]);
        PlayerPrefs.Save();
        Changed?.Invoke();
        return true;
    }
    public static void Apply(WardenAppearance appearance)
    {
        if (appearance == null || appearance.availableSkins == null) return;
        foreach (var skin in appearance.availableSkins)
            if (skin != null && skin.skinId == WardenSkinIds[SelectedColor(0)]) { appearance.SetSkin(skin); return; }
    }
}
