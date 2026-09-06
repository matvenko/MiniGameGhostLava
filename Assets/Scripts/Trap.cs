using System.Collections;
using UnityEngine;

// A placed trap: sits armed on its tile pulsing gently, and the first enemy
// to step on it is frozen. The trap then stays visible as an expanded
// "snare" for the stun duration - the tile the catch happened on, under the
// ice the enemy itself is now wearing (see FreezeVisual) - before fading out
// and destroying itself.
public class Trap : MonoBehaviour
{
    [SerializeField] private float stunDuration = 4f;
    [SerializeField] private float armedPulseScale = 1.15f;
    [SerializeField] private float armedPulseSpeed = 2.5f;
    [SerializeField] private float snareScale = 1.5f;
    [SerializeField] private float fadeDuration = 0.5f;

    private bool _triggered;
    private Vector3 _baseScale;
    private Renderer _renderer;

    void Awake()
    {
        _baseScale = transform.localScale;
        _renderer = GetComponentInChildren<Renderer>();
    }

    void Update()
    {
        if (_triggered) return;
        float t = (Mathf.Sin(Time.time * armedPulseSpeed) + 1f) * 0.5f;
        transform.localScale = _baseScale * Mathf.Lerp(1f, armedPulseScale, t);
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
        // snap out to the wider snare footprint so the catch reads instantly
        transform.localScale = _baseScale * snareScale;

        float hold = Mathf.Max(0f, stunDuration - fadeDuration);
        yield return new WaitForSeconds(hold);

        if (_renderer != null)
        {
            var mat = _renderer.material;
            bool canFade = mat.HasProperty("_BaseColor") || mat.HasProperty("_Color");
            if (canFade)
            {
                string prop = mat.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                Color start = mat.GetColor(prop);
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    Color c = start;
                    c.a = Mathf.Lerp(start.a, 0f, t / fadeDuration);
                    mat.SetColor(prop, c);
                    yield return null;
                }
            }
            else
            {
                // no fadeable color on this shader - shrink away instead
                Vector3 from = transform.localScale;
                float t = 0f;
                while (t < fadeDuration)
                {
                    t += Time.deltaTime;
                    transform.localScale = Vector3.Lerp(from, Vector3.zero, t / fadeDuration);
                    yield return null;
                }
            }
        }

        Destroy(gameObject);
    }
}
