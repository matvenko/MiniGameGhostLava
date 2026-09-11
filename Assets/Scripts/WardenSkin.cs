using UnityEngine;

[CreateAssetMenu(menuName = "Maze Boo/Warden Skin")]
public sealed class WardenSkin : ScriptableObject
{
    [Tooltip("Stable ID for the future inventory/shop. Changing colors does not change ownership.")]
    public string skinId;
    public string displayName;
    public Color shell = new Color(1f, .5f, .18f);
    public Color shadow = new Color(.45f, .14f, .035f);
    public Color face = new Color(.012f, .022f, .05f);
    [ColorUsage(false, true)] public Color rim = new Color(.2f, 1.4f, 2f);
    [ColorUsage(false, true)] public Color eyes = new Color(.5f, 1.8f, 2.2f);
}
