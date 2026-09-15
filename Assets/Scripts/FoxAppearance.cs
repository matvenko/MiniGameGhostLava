using System.Collections.Generic;
using UnityEngine;

// Own material copies so the multi-material Fox keeps its cream muzzle,
// dark face and seams, while shell and illuminated accents change together.
public sealed class FoxAppearance : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    private static readonly Color[] Shells = { new Color(1.35f,.5f,.29f), new Color(.48f,.83f,1.15f), new Color(.79f,.5f,1.1f), new Color(.35f,1f,.7f) };
    private static readonly Color[] Accents = { new Color(.015f,2.8f,3.5f), new Color(1.9f,.85f,.2f), new Color(.3f,2.3f,3f), new Color(1.6f,.65f,2.8f) };
    public int ColorIndex { get; private set; }
    public void SetColor(int index, bool portrait = false)
    {
        ColorIndex = Mathf.Clamp(index, 0, 3);
        if (materials.Count == 0)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var copies = renderer.sharedMaterials;
                for (int i = 0; i < copies.Length; i++)
                {
                    if (copies[i] == null) continue;
                    copies[i] = new Material(copies[i]);
                    materials.Add(copies[i]);
                }
                renderer.sharedMaterials = copies;
            }
        }
        Color shell = Shells[ColorIndex], accent = Accents[ColorIndex];
        foreach (var material in materials)
        {
            if (material.name.StartsWith("Fox_Coral"))
            {
                material.SetColor("_BaseColor", shell);
                material.SetColor("_ShadeColor", shell * .45f);
            }
            else if (material.name.StartsWith("Fox_Cyan"))
            {
                material.SetColor("_BaseColor", accent * .2f);
                material.SetColor("_ShadeColor", accent * .1f);
                material.SetColor("_EmissionColor", portrait ? accent / Mathf.Max(1, accent.maxColorComponent) : accent);
            }
            else if (material.name.StartsWith("Fox_InnerEar"))
                material.SetColor("_BaseColor", accent * .06f);
        }
        foreach (var light in GetComponentsInChildren<Light>(true)) light.color = accent / Mathf.Max(1, accent.maxColorComponent);
    }
    void OnDestroy()
    {
        foreach (var material in materials) if (material != null) Destroy(material);
    }
}
