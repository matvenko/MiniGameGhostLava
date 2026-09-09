#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Runs only in the isolated review scene, never attached to the shipping player.
public sealed class LumenPlayerIntegrationProbe : MonoBehaviour
{
    readonly List<string> checks = new List<string>();
    Sample.GhostScript player;
    Animator animator;
    SkinnedMeshRenderer[] renderers;
    Light glow;
    VirtualJoystick joystick;

    IEnumerator Start()
    {
        Application.runInBackground = true;
        yield return null;
        player = GetComponent<Sample.GhostScript>();
        animator = transform.Find("LumenGhost_Visual").GetComponent<Animator>();
        renderers = transform.Find("LumenGhost_Visual").GetComponentsInChildren<SkinnedMeshRenderer>();
        glow = transform.Find("LumenGhost_Visual").GetComponentInChildren<Light>();
        joystick = VirtualJoystick.Instance;
        var run = Run();
        string failure = null;
        while (true)
        {
            bool more;
            try { more = run.MoveNext(); }
            catch (Exception e) { failure = e.ToString(); break; }
            if (!more) break;
            yield return run.Current;
        }
        var report = new Report { passed = failure == null, checks = checks.ToArray(), failure = failure };
        File.WriteAllText("Art/LumenGhost/integration_validation.json", JsonUtility.ToJson(report, true));
        Debug.Log("LUMEN INTEGRATION " + (report.passed ? "PASSED" : failure));
        UnityEditor.EditorApplication.isPlaying = false;
    }

    IEnumerator Run()
    {
        Require(renderers.Length == 13 && GetComponent<Animator>().enabled == false, "New visual exclusively bound");
        foreach (string state in new[] { "idle", "move", "surprised", "attack_shift", "dissolve" })
            Require(animator.HasState(0, Animator.StringToHash("Base Layer." + state)), "Animator state " + state);
        yield return new WaitForSeconds(.15f);
        var start = transform.position;
        SetStick(Vector2.right);
        yield return new WaitForSeconds(.35f);
        Require(start.x - transform.position.x > .25f, "Joystick moves CharacterController");
        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("move"), "Movement selects move animation");
        Require(Vector3.Dot(transform.forward, Vector3.left) > .98f, "Visual follows movement facing");
        typeof(EnemySpawnManager).GetField("_freezeUntil", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, Time.time + .3f);
        var frozen = transform.position;
        yield return new WaitForSeconds(.2f);
        Require(Mathf.Abs(frozen.x - transform.position.x) < .02f, "Countdown freezes movement");
        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("idle"), "Countdown returns to idle");
        yield return new WaitForSeconds(.6f);
        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("move"), "Held joystick resumes after countdown");
        SetStick(Vector2.zero);
        yield return new WaitForSeconds(.5f);
        Require(animator.GetCurrentAnimatorStateInfo(0).IsName("idle"), "Joystick release returns to idle");

        var target = new Vector3(2, transform.position.y, 1);
        player.TeleportTo(target);
        bool faded = false;
        float until = Time.time + .7f;
        while (Time.time < until)
        {
            var value = Dissolve(renderers[0]);
            if (value < .8f) faded = true;
            Require(renderers.All(r => Mathf.Abs(Dissolve(r) - value) < .001f), null);
            if (value < .5f) Require(glow.intensity < .4f, null);
            yield return null;
        }
        Require(faded && Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(2, 1)) < .01f,
            "Teleport fades all surfaces and moves to destination");
        Require(Mathf.Abs(Dissolve(renderers[0]) - 1) < .001f && glow.intensity > .5f, "Teleport restores body and light");

        player.StartInvincibility(.3f);
        Require(renderers.All(r => !r.enabled) && glow.intensity == 0, "Grace blink hides face, body and light together");
        yield return new WaitForSeconds(.5f);
        Require(renderers.All(r => r.enabled) && glow.intensity > .5f, "Grace blink restores complete visual");
        player.StartInvincibility(2);
        player.RespawnAt(Vector3.zero);
        Require(renderers.All(r => r.enabled) && glow.intensity > .5f, "Respawn during blink restores all parts");

        player.ActivateShield(.3f);
        player.CaughtByEnemy();
        Require(player.ShieldActive && GetComponent<CharacterController>().enabled, "Shield protects new player from enemy catch");
        yield return new WaitForSeconds(.5f);
        Require(!player.ShieldActive, "Shield expires");
        player.FallIntoLava();
        Require(!GetComponent<CharacterController>().enabled, "Lava disables movement on death");
        yield return player.PlayDeathAnimation();
        Require(renderers.All(r => Dissolve(r) < .001f) && glow.intensity == 0, "Death dissolves all parts and extinguishes light");
        player.RespawnAt(Vector3.zero);
        yield return new WaitForSeconds(.2f);
        Require(GetComponent<CharacterController>().enabled && renderers.All(r => r.enabled && Dissolve(r) > .999f), "Respawn restores movement and complete visual");
        player.CaughtByEnemy();
        Require(!GetComponent<CharacterController>().enabled, "Enemy catch still kills unshielded player");
        player.RespawnAt(Vector3.zero);
    }

    void SetStick(Vector2 value) => typeof(VirtualJoystick).GetProperty("InputDirection").GetSetMethod(true).Invoke(joystick, new object[] { value });
    float Dissolve(Renderer renderer)
    {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        return block.HasFloat("_Dissolve") ? block.GetFloat("_Dissolve") : 1;
    }
    void Require(bool pass, string label)
    {
        if (!pass) throw new Exception(label ?? "Face/body/light synchronization failed");
        if (label != null) checks.Add(label);
    }
    [Serializable] class Report { public bool passed; public string[] checks; public string failure; }
}
#endif
