using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// Visuals animate independently of the trigger, keeping the catch footprint fixed.
public class Trap : MonoBehaviour
{
    [SerializeField] private float stunDuration = 4f;
    [SerializeField] private float armedPulseScale = 1.15f;
    [SerializeField] private float armedPulseSpeed = 2.5f;
    [SerializeField] private float snareScale = 1.5f;
    [SerializeField] private float fadeDuration = 0.5f;

    private bool _triggered;
    private Transform _visual;
    private readonly Transform[] _teeth = new Transform[8];
    private LineRenderer _halo, _timer, _burst;
    private Material _metal, _ice, _glow;
    private Mesh _crystal;
    private float _age;
    private static readonly Color Cyan = new Color(0.18f, 0.88f, 1f);

    void Awake()
    {
        var source = GetComponentInChildren<Renderer>();
        if (source == null || source.sharedMaterial == null) return;
        _metal = new Material(source.sharedMaterial);
        _ice = new Material(source.sharedMaterial);
        _glow = new Material(source.sharedMaterial);
        Paint(_metal, new Color(0.055f, 0.12f, 0.19f), 0f);
        Paint(_ice, new Color(0.5f, 0.91f, 1f), 0.4f);
        Paint(_glow, Cyan, 1.8f);
        _visual = new GameObject("Frost snare visuals").transform;
        _visual.SetParent(transform, false);
        // Move only the authored meshes; never scale or move the collider.
        var meshes = GetComponentsInChildren<MeshRenderer>();
        _crystal = CreateCrystal();
        foreach (var mesh in meshes)
        {
            mesh.transform.SetParent(_visual, false);
            mesh.sharedMaterial = mesh.name == "Disc" ? _metal : _ice;
            if (mesh.name == "Disc")
                mesh.transform.localScale = new Vector3(0.76f, 0.035f, 0.76f);
            else if (mesh.name.StartsWith("Tooth") &&
                     int.TryParse(mesh.name.Substring(5), out int index) && index < 8 && index >= 0)
            {
                _teeth[index] = mesh.transform;
                mesh.GetComponent<MeshFilter>().sharedMesh = _crystal;
            }
        }
        _halo = Ring("Outer frost seal", 0.022f);
        _timer = Ring("Stun countdown", 0.032f);
        _burst = Ring("Catch shockwave", 0.028f);
        _burst.enabled = false;
        DrawRing(_halo, 0.39f, 0.045f, 1f);
        DrawRing(_timer, 0.26f, 0.055f, 1f);
        PoseTeeth(0f);
    }

    private static void Paint(Material material, Color color, float emission)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.65f);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * emission);
        }
    }

    private LineRenderer Ring(string label, float width)
    {
        var go = new GameObject(label);
        go.transform.SetParent(_visual, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = _glow;
        line.useWorldSpace = false;
        line.widthMultiplier = width;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.numCornerVertices = 2;
        return line;
    }

    private static void DrawRing(LineRenderer line, float radius, float height, float amount)
    {
        const int segments = 64;
        line.enabled = amount > 0.001f;
        line.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (90f - 360f * amount * i / segments) * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
        }
    }

    void Update()
    {
        if (_triggered || _visual == null) return;
        _age += Time.deltaTime;
        float pulse = (Mathf.Sin(_age * armedPulseSpeed) + 1f) * 0.5f;
        float arrival = Mathf.Clamp01(_age / 0.25f);
        float scale = 1f - Mathf.Pow(1f - arrival, 3f);
        _visual.localScale = Vector3.one * scale;
        _halo.transform.localScale = Vector3.one * Mathf.Lerp(1f, armedPulseScale, pulse);
        _timer.transform.localRotation = Quaternion.Euler(0f, _age * 25f, 0f);
        DrawRing(_timer, 0.26f, 0.055f, 0.82f);
    }

    private void PoseTeeth(float closure)
    {
        for (int i = 0; i < _teeth.Length; i++)
        {
            if (_teeth[i] == null) continue;
            float angle = i * 45f;
            Vector3 radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            _teeth[i].localPosition = radial * Mathf.Lerp(0.34f, 0.28f, closure) + Vector3.up * 0.03f;
            _teeth[i].localRotation = Quaternion.Euler(0f, angle, 0f) * Quaternion.Euler(Mathf.Lerp(-28f, 32f, closure), 0f, 0f);
            _teeth[i].localScale = new Vector3(0.12f, Mathf.Lerp(0.24f, 0.5f, closure), 0.13f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        var enemy = other.GetComponentInParent<EnemyChaser>();
        if (enemy == null) return;
        _triggered = true;
        AudioManager.Play(GameSound.TrapSnap);
        enemy.Stun(stunDuration);
        StartCoroutine(SnareThenExpire());
    }

    private IEnumerator SnareThenExpire()
    {
        float duration = Mathf.Max(0f, stunDuration);
        float fade = Mathf.Clamp(fadeDuration, 0f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (_visual != null)
            {
                float snap = Mathf.Clamp01(elapsed / 0.16f);
                PoseTeeth(1f - Mathf.Pow(1f - snap, 3f));
                float disappear = fade > 0f ? Mathf.Clamp01((duration - elapsed) / fade) : 1f;
                _visual.localScale = Vector3.one * disappear * (1f + 0.12f * Mathf.Sin(snap * Mathf.PI));
                _halo.transform.localScale = Vector3.one;
                _timer.transform.localRotation = Quaternion.identity;
                DrawRing(_timer, 0.29f, 0.065f, Mathf.Clamp01(1f - elapsed / duration));
                float wave = Mathf.Clamp01(elapsed / 0.4f);
                DrawRing(_burst, Mathf.Lerp(0.3f, 0.48f * snareScale, wave), 0.07f, 1f);
                _burst.widthMultiplier = 0.045f * (1f - wave);
                _burst.enabled = wave < 1f;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    private static Mesh CreateCrystal()
    {
        var mesh = new Mesh { name = "Frost snare crystal" };
        Vector3[] corners = { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
        var vertices = new Vector3[12];
        var indices = new int[12];
        for (int i = 0; i < 4; i++)
        {
            vertices[i * 3] = corners[i];
            vertices[i * 3 + 1] = Vector3.up;
            vertices[i * 3 + 2] = corners[(i + 1) % 4];
            for (int j = 0; j < 3; j++) indices[i * 3 + j] = i * 3 + j;
        }
        mesh.vertices = vertices;
        mesh.triangles = indices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDestroy()
    {
        if (_metal != null) Destroy(_metal);
        if (_ice != null) Destroy(_ice);
        if (_glow != null) Destroy(_glow);
        if (_crystal != null) Destroy(_crystal);
    }
}
