using UnityEngine;

// Rotates the scene's sun (a Directional Light) through a full 360 degree
// arc on a repeating timer, fading its intensity/color and the ambient
// light to match: bright and white at solar noon, warm near the horizon
// (sunrise/sunset), and dim/cool once it's below the horizon at night.
[RequireComponent(typeof(Light))]
public class DayNightCycle : MonoBehaviour
{
    [SerializeField] private float cycleDurationSeconds = 120f;
    [SerializeField] private float maxSunIntensity = 1.2f;
    [SerializeField] private float nightAmbientIntensity = 0.15f;
    [SerializeField] private float daySkyboxExposure = 1f;

    [SerializeField] private Gradient sunColor = DefaultGradient();
    [SerializeField] private AnimationCurve intensityCurve = DefaultIntensityCurve();

    private Light _sun;
    private float _baseYaw;
    private float _baseRoll;

    void Awake()
    {
        _sun = GetComponent<Light>();
        Vector3 euler = transform.eulerAngles;
        _baseYaw = euler.y;
        _baseRoll = euler.z;
    }

    void Update()
    {
        float t = Mathf.Repeat(Time.time, cycleDurationSeconds) / cycleDurationSeconds;
        float elevationAngle = t * 360f;
        transform.rotation = Quaternion.Euler(elevationAngle, _baseYaw, _baseRoll);

        // 1 at solar noon (sun straight down), 0 at the horizon, negative
        // once it's below the horizon - this is what makes the arc read as
        // a real day/night cycle instead of just a spinning light.
        float sunHeight = Vector3.Dot(transform.forward, Vector3.down);
        float dayFactor = intensityCurve.Evaluate(Mathf.Clamp01(sunHeight));

        _sun.intensity = dayFactor * maxSunIntensity;
        _sun.color = sunColor.Evaluate(Mathf.Clamp01(sunHeight * 0.5f + 0.5f));

        RenderSettings.ambientIntensity = Mathf.Lerp(nightAmbientIntensity, 1f, dayFactor);
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
        {
            RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(nightAmbientIntensity, daySkyboxExposure, dayFactor));
        }
    }

    // Evaluated against (sunHeight * 0.5 + 0.5), which is symmetric around
    // the horizon - so this same curve naturally covers both sunrise and
    // sunset (same elevation = same color) without needing separate keys.
    private static Gradient DefaultGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.20f, 0.28f, 0.45f), 0f),   // midnight - cool blue
                new GradientColorKey(new Color(1.00f, 0.55f, 0.30f), 0.5f), // horizon - warm orange
                new GradientColorKey(new Color(1.00f, 1.00f, 1.00f), 1f)    // noon - neutral white
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return g;
    }

    private static AnimationCurve DefaultIntensityCurve()
    {
        // smooth fade to 0 as the sun approaches/crosses the horizon
        return new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.15f, 1f),
            new Keyframe(1f, 1f)
        );
    }
}
