using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Renderer-free halo: shields and freeze shells must measure the character,
// not its aura. The draw is depth tested, so it does not reveal ghosts through walls.
[DisallowMultipleComponent]
public sealed class CharacterGlow : MonoBehaviour
{
    private Renderer[] bodies;
    private Material[][] materials;
    private Material aura;
    private Mesh quad;
    private Light fill;
    private WardenAppearance warden;
    private SpectralHunterPalette hunter;
    private MaterialPropertyBlock properties;
    private Color tint = new Color(0.35f, 0.8f, 1f);
    private float nextColorUpdate = float.NegativeInfinity;
    private float pulsePhase;
    private static readonly int Dissolve = Shader.PropertyToID("_Dissolve");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int Tint = Shader.PropertyToID("_Tint");
    private static readonly int Strength = Shader.PropertyToID("_Strength");
    private static readonly int FloorY = Shader.PropertyToID("_FloorY");
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    public static void Attach(GameObject character)
    {
        if (character.GetComponent<CharacterGlow>() == null) character.AddComponent<CharacterGlow>();
    }

    void Start()
    {
        properties = new MaterialPropertyBlock();
        pulsePhase = transform.position.x * 1.7f + transform.position.z * 2.3f;
        var shader = Resources.Load<Shader>("CharacterEffects/CharacterAura");
        if (shader == null) { enabled = false; return; }
        var found = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r is MeshRenderer || r is SkinnedMeshRenderer) found.Add(r);
        bodies = found.ToArray();
        materials = new Material[bodies.Length][];
        for (int i = 0; i < bodies.Length; i++) materials[i] = bodies[i].sharedMaterials;
        warden = GetComponentInChildren<WardenAppearance>(true);
        hunter = GetComponentInChildren<SpectralHunterPalette>(true);
        aura = new Material(shader) { name = "Character halo (runtime)" };
        quad = new Mesh { name = "Character halo quad" };
        quad.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
            new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
        quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        // Conservative bounds for the floor pool the shader spreads under the body.
        quad.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
        // The Warden already carries a light controlled by its death/teleport code.
        if (GetComponentInChildren<Light>(true) == null)
        {
            var go = new GameObject("Character soft light");
            go.transform.SetParent(transform, false);
            fill = go.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.shadows = LightShadows.None;
            fill.renderMode = LightRenderMode.Auto;
            fill.intensity = 0f;
        }
    }

    void LateUpdate()
    {
        if (aura == null) return;
        // Managed caches are lost when Unity reloads scripts during a live game.
        if (properties == null) properties = new MaterialPropertyBlock();
        if (bodies == null) bodies = GetComponentsInChildren<Renderer>(true);
        if (materials == null || materials.Length != bodies.Length)
        {
            materials = new Material[bodies.Length][];
            for (int i = 0; i < bodies.Length; i++)
                materials[i] = bodies[i] != null ? bodies[i].sharedMaterials : new Material[0];
            nextColorUpdate = float.NegativeInfinity;
        }
        bool any = false;
        var bounds = new Bounds();
        float visibility = 0f;
        bool updateColor = Time.time >= nextColorUpdate;
        float brightest = 0.01f;
        for (int i = 0; i < bodies.Length; i++)
        {
            var r = bodies[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r.forceRenderingOff) continue;
            if (any) bounds.Encapsulate(r.bounds); else { bounds = r.bounds; any = true; }
            r.GetPropertyBlock(properties);
            float visible = 1f;
            if (properties.HasFloat(Dissolve)) visible = properties.GetFloat(Dissolve);
            else if (materials[i].Length > 0 && materials[i][0] != null && materials[i][0].HasProperty(Dissolve))
                visible = materials[i][0].GetFloat(Dissolve);
            visibility = Mathf.Max(visibility, Mathf.Clamp01(visible));
            if (!updateColor) continue;
            foreach (var material in materials[i])
            {
                if (material == null) continue;
                Color color;
                if (properties.HasColor(BaseColor)) color = properties.GetColor(BaseColor);
                else if (material.HasProperty("_BaseColor")) color = material.GetColor("_BaseColor");
                else if (material.HasProperty("_MainColor")) color = material.GetColor("_MainColor");
                else if (material.HasProperty("_Color")) color = material.GetColor("_Color");
                else continue;
                // Prefer the main body over small eyes, trim, and dark face masks.
                float value = r.bounds.size.sqrMagnitude * color.maxColorComponent;
                if (value <= brightest) continue;
                brightest = value;
                tint = color;
                tint.a = 1f;
            }
        }
        if (updateColor)
        {
            if (hunter != null) tint = hunter.GlowColour;
            if (warden != null && warden.skin != null)
            {
                tint = warden.skin.shell;
                tint.a = 1f;
            }
            nextColorUpdate = Time.time + 0.25f;
        }
        if (!any || visibility <= 0.001f)
        {
            if (fill != null) fill.intensity = 0f;
            return;
        }
        float pulse = 1f + Mathf.Sin(Time.time * 2f + pulsePhase) * 0.055f;
        // Keep the body hue but at full brightness and saturation: pale pastels
        // otherwise light the floor a flat grey. Near-greys have no hue to push.
        Color.RGBToHSV(tint, out float hue, out float saturation, out _);
        if (saturation > 0.12f) saturation = Mathf.Max(saturation, 0.72f);
        Color brightTint = Color.HSVToRGB(hue, saturation, 1f);
        brightTint.a = 1f;
        aura.SetColor(Tint, brightTint);
        aura.SetFloat(Strength, visibility * pulse);
        // The glow lies flat on the ground under the body, lighting the floor
        // instead of fogging the character. Trailing wisps hang almost to the
        // floor, so measure the ground rather than trusting the body's lowest point.
        aura.SetFloat(FloorY, GroundBelow(bounds) + 0.015f);
        var matrix = Matrix4x4.TRS(bounds.center, Quaternion.identity, bounds.size);
        Graphics.DrawMesh(quad, matrix, aura, gameObject.layer, null, 0, null,
            ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        if (fill != null)
        {
            fill.transform.position = bounds.center;
            fill.color = brightTint;
            fill.range = Mathf.Clamp(bounds.size.magnitude * 0.77f, 0.77f, 1.71f);
            fill.intensity = 1.11f * visibility * pulse * Mathf.Clamp01(bounds.size.magnitude);
        }
    }

    private float GroundBelow(Bounds body)
    {
        int count = Physics.RaycastNonAlloc(body.center, Vector3.down, groundHits,
            body.extents.y + 1f, ~0, QueryTriggerInteraction.Ignore);
        float ground = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.transform.IsChildOf(transform)) continue;
            ground = Mathf.Max(ground, hit.point.y);
        }
        // Over a pit there is nothing to light; keep the glow just under the body.
        return float.IsNegativeInfinity(ground) ? body.min.y - 0.05f : ground;
    }

    void OnDisable() { if (fill != null) fill.intensity = 0f; }
    void OnDestroy()
    {
        Release(aura);
        Release(quad);
        if (fill != null) Release(fill.gameObject);
    }

    private static void Release(Object owned)
    {
        if (owned == null) return;
        if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned);
    }
}
