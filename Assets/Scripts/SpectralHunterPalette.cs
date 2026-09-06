using System.Collections.Generic;
using UnityEngine;

// The colourway of one spectral hunter, editable in the inspector.
//
// The three hunters are the same model in three sets of materials - frost,
// ember and the original ivory - and the hue is the only thing that separates
// them, so it is the thing most worth reaching for. Reaching for it through the
// material assets meant hunting five files per hunter and editing them blind;
// this puts the same five colours on the enemy itself, where the change can be
// seen on the board while it is being made.
//
// The colours are pushed through a MaterialPropertyBlock rather than written to
// the materials: an edit in play mode is then a preview rather than a permanent
// change to the asset, and the three hunters can share a material set without
// bleeding into each other.
//
// A part is found by what its material is called, not by where it sits in the
// hierarchy, so the mapping survives the model being re-exported with its
// pieces renamed or reordered.
//
// It paints the whole hunter from wherever it is dropped: it climbs to the
// object carrying the EnemyChaser and works down from there. That is what lets
// it live on the mantle - the piece the mouse lands on in the scene view - and
// still colour the hood, the sleeves and the eyes.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SpectralHunterPalette : MonoBehaviour
{
    [System.Serializable]
    public class Part
    {
        [Tooltip("Base colour of this part.")]
        public Color colour = Color.white;

        [Tooltip("How hard the part glows. The glow takes the colour above, so the two never drift apart.")]
        [Range(0f, 4f)] public float glow;
    }

    [Header("Body")]
    [Tooltip("The porcelain shell: hood, sleeves and the lit side of the mantle.")]
    [SerializeField] private Part body = new Part();
    [Tooltip("The swept mantle and the wisp trailing behind it.")]
    [SerializeField] private Part mantle = new Part();

    [Header("Face")]
    [Tooltip("The deep mask the eyes sit in, and the mouth.")]
    [SerializeField] private Part face = new Part();
    [Tooltip("The bright rim around the mask.")]
    [SerializeField] private Part rim = new Part();
    [Tooltip("The eyes. This is the colour that says which hunter is coming, so it carries most of the glow.")]
    [SerializeField] private Part eyes = new Part();

    // Set once the five parts hold this hunter's own colours. Until then the
    // component has nothing to say and reads them off the materials instead, so
    // dropping it on a hunter never repaints it by surprise.
    [SerializeField, HideInInspector] private bool captured;

    private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColour = Shader.PropertyToID("_Color");
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _block;

    // Every palette currently on the board. EnemySpawnManager clones the scene's
    // authored hunter, so during a game the thing being looked at is a copy and
    // the thing being edited is the template it came from.
    private static readonly List<SpectralHunterPalette> Live = new List<SpectralHunterPalette>();

    void Reset() => CaptureFromMaterials();

    void OnEnable()
    {
        if (!Live.Contains(this)) Live.Add(this);
        if (!captured) CaptureFromMaterials();
        Apply();
    }

    void OnDisable() => Live.Remove(this);

    // No isActiveAndEnabled guard: a hunter parked inactive off the board is
    // exactly the one being dressed, and it should still take the colour.
    void OnValidate()
    {
        Apply();
        if (!Application.isPlaying) return;
        // An edit made while the game runs is meant for the hunters on the
        // board, not for the template alone. Copies are recognised by sharing
        // this one's materials - that is what makes them the same hunter.
        foreach (var other in Live)
        {
            if (other == null || other == this || !other.WearsTheSameSkinAs(this)) continue;
            other.CopyFrom(this);
            other.Apply();
        }
    }

    private void CopyFrom(SpectralHunterPalette source)
    {
        body = Copy(source.body);
        mantle = Copy(source.mantle);
        face = Copy(source.face);
        rim = Copy(source.rim);
        eyes = Copy(source.eyes);
        captured = source.captured;
    }

    private static Part Copy(Part part) => new Part { colour = part.colour, glow = part.glow };

    private bool WearsTheSameSkinAs(SpectralHunterPalette other)
    {
        var mine = FirstMaterial();
        return mine != null && mine == other.FirstMaterial();
    }

    // The hunter this palette belongs to, whichever of its pieces the component
    // was dropped on. The chaser is what makes an object one enemy rather than
    // one mesh, so the highest one above this component is the hunter; with none
    // above it - a loose model being dressed in isolation - it paints itself.
    private Transform HunterRoot()
    {
        Transform hunter = transform;
        for (Transform step = transform; step != null; step = step.parent)
            if (step.GetComponent<EnemyChaser>() != null) hunter = step;
        return hunter;
    }

    private Material FirstMaterial()
    {
        foreach (var renderer in HunterRoot().GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
                if (material != null) return material;
        return null;
    }

    // Repaints every renderer under this hunter from the five parts above.
    // Public so a script that recolours a hunter at runtime - a boss variant, a
    // level's own palette - can hand it new colours and ask for them straight away.
    [ContextMenu("Apply colours")]
    public void Apply()
    {
        if (!captured) return;
        _block ??= new MaterialPropertyBlock();
        foreach (var renderer in HunterRoot().GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                var part = PartFor(materials[slot]);
                if (part == null) continue;
                renderer.GetPropertyBlock(_block, slot);
                _block.SetColor(BaseColour, part.colour);
                _block.SetColor(LegacyColour, part.colour);
                _block.SetColor(Emission, part.colour * part.glow);
                renderer.SetPropertyBlock(_block, slot);
            }
        }
    }

    // Reads the colourway the materials already carry, so the inspector opens on
    // what is on the board rather than on white.
    [ContextMenu("Capture colours from materials")]
    public void CaptureFromMaterials()
    {
        foreach (var renderer in HunterRoot().GetComponentsInChildren<Renderer>(true))
        {
            var materials = renderer.sharedMaterials;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                // Drop any override first, or a colour set earlier would be read
                // back as if the material had always carried it.
                renderer.SetPropertyBlock(null, slot);
                var material = materials[slot];
                var part = PartFor(material);
                if (part == null || material == null) continue;
                Color colour = material.HasProperty(BaseColour) ? material.GetColor(BaseColour)
                    : material.HasProperty(LegacyColour) ? material.GetColor(LegacyColour) : Color.white;
                part.colour = colour;
                part.glow = 0f;
                if (!material.HasProperty(Emission)) continue;
                Color emission = material.GetColor(Emission);
                float lit = Mathf.Max(colour.r, Mathf.Max(colour.g, colour.b));
                if (lit > .001f)
                    part.glow = Mathf.Clamp(Mathf.Max(emission.r, Mathf.Max(emission.g, emission.b)) / lit, 0f, 4f);
            }
        }
        captured = true;
        Apply();
    }

    // The material names come from the model - "Frost Hostile gaze - coral",
    // "Tail - violet" - and every variant keeps the same words in them.
    private Part PartFor(Material material)
    {
        if (material == null) return null;
        string name = material.name.ToLowerInvariant();
        if (name.Contains("gaze") || name.Contains("eye")) return eyes;
        if (name.Contains("face") || name.Contains("mouth")) return face;
        if (name.Contains("edge") || name.Contains("rim")) return rim;
        if (name.Contains("porcelain")) return body;
        if (name.Contains("tail") || name.Contains("mantle") || name.Contains("wisp")) return mantle;
        return null;
    }
}
