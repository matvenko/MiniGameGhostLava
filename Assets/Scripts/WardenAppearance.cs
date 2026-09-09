using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public sealed class WardenAppearance : MonoBehaviour
{
    public WardenSkin skin;
    public WardenEyeStyle eyeStyle;
    public WardenSkin[] availableSkins;
    public WardenEyeStyle[] availableEyes;
    public Renderer[] shellRenderers;
    public Renderer faceRenderer;
    public Renderer rimRenderer;
    public Renderer leftEye;
    public Renderer rightEye;
    public Light glowLight;
    MaterialPropertyBlock block;
    readonly List<Material> portraitMaterials = new List<Material>();

    void OnEnable() => ApplyAppearance();
    void OnValidate() => ApplyAppearance();

    public void SetSkin(WardenSkin value)
    {
        if (value == null) return;
        skin = value;
        ApplyAppearance();
    }

    public void SetEyes(WardenEyeStyle value)
    {
        if (value == null || value.leftMesh == null || value.rightMesh == null) return;
        eyeStyle = value;
        ApplyAppearance();
    }

    public void ApplyAppearance()
    {
        if (skin == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        if (shellRenderers != null)
            foreach (var r in shellRenderers) Paint(r, skin.shell, skin.shadow, Color.black);
        Paint(faceRenderer, skin.face, skin.face * .55f, Color.black);
        Paint(rimRenderer, skin.rim * .3f, skin.rim * .15f, skin.rim);
        Paint(leftEye, skin.eyes * .25f, skin.eyes * .25f, skin.eyes);
        Paint(rightEye, skin.eyes * .25f, skin.eyes * .25f, skin.eyes);
        if (eyeStyle != null)
        {
            if (leftEye != null && eyeStyle.leftMesh != null && leftEye.TryGetComponent<MeshFilter>(out var left)) left.sharedMesh = eyeStyle.leftMesh;
            if (rightEye != null && eyeStyle.rightMesh != null && rightEye.TryGetComponent<MeshFilter>(out var right)) right.sharedMesh = eyeStyle.rightMesh;
        }
        // Visibility is owned by GhostScript; a skin switch must not revive its light.
        if (glowLight != null) glowLight.color = skin.rim / Mathf.Max(1, skin.rim.maxColorComponent);
    }

    void Paint(Renderer renderer, Color color, Color shadow, Color emission)
    {
        if (renderer == null) return;
        renderer.GetPropertyBlock(block); // Preserve teleport/death visibility.
        block.SetColor("_BaseColor", color);
        block.SetColor("_ShadeColor", shadow);
        block.SetColor("_EmissionColor", emission);
        renderer.SetPropertyBlock(block);
    }

    // The UI uses LDR. Bake the selected skin into owned material copies and
    // limit only their emission; gameplay materials and property blocks stay independent.
    public void PreparePortrait()
    {
        ApplyAppearance();
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            var originals = r.sharedMaterials;
            for (int i = 0; i < originals.Length; i++)
            {
                if (originals[i] == null) continue;
                var m = new Material(originals[i]);
                if (block.HasColor("_BaseColor")) m.SetColor("_BaseColor", block.GetColor("_BaseColor"));
                if (block.HasColor("_ShadeColor")) m.SetColor("_ShadeColor", block.GetColor("_ShadeColor"));
                if (block.HasColor("_EmissionColor"))
                {
                    Color em = block.GetColor("_EmissionColor");
                    m.SetColor("_EmissionColor", em / Mathf.Max(1, em.maxColorComponent));
                }
                m.SetFloat("_Dissolve", 1);
                originals[i] = m;
                portraitMaterials.Add(m);
            }
            r.sharedMaterials = originals;
            r.SetPropertyBlock(null);
        }
    }

    void OnDestroy()
    {
        foreach (var m in portraitMaterials)
            if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
    }
}
