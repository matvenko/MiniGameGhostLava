using System.Collections.Generic;
using UnityEngine;

public enum GameSound { Click, Ready, Start, Shield, Freeze, Teleport, TrapPlace, TrapSnap, Death, Victory, Reward }

// Original synthesized score and effects, shared by the menu and gameplay.
// No downloads, scene references, or per-event clip allocations are needed.
public static class GameAudioClips
{
    const int Rate = 22050;
    static readonly Dictionary<GameSound, AudioClip> effects = new Dictionary<GameSound, AudioClip>();
    static AudioClip score;
    public static AudioClip Music => score != null ? score : (score = Compose());

    public static AudioClip Get(GameSound sound)
    {
        if (effects.TryGetValue(sound, out var cached) && cached != null) return cached;
        float duration = sound == GameSound.Victory ? 1.6f : sound == GameSound.Death ? .85f : sound == GameSound.Click ? .09f : .55f;
        var data = new float[(int)(Rate * duration)];
        var noise = new System.Random(731 + (int)sound);
        float phase = 0;
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / Rate, p = t / duration;
            float hz = 440, grit = 0, harmonic = .15f;
            switch (sound)
            {
                case GameSound.Click: hz = 740 - 300 * p; break;
                case GameSound.Ready: hz = 523; break;
                case GameSound.Start: hz = p < .35f ? 523 : p < .65f ? 659 : 784; break;
                case GameSound.Shield: hz = 196 + 196 * p; harmonic = .4f; break;
                case GameSound.Freeze: hz = 1800 - 1100 * p; grit = .28f; break;
                case GameSound.Teleport: hz = 180 + 1200 * p * p; grit = .12f; break;
                case GameSound.TrapPlace: hz = 240 - 130 * p; grit = .18f; break;
                case GameSound.TrapSnap: hz = 850 - 620 * p; grit = .4f; break;
                case GameSound.Death: hz = 330 * Mathf.Pow(.22f, p); harmonic = .35f; break;
                case GameSound.Victory: hz = Midi(new[] {72, 76, 79, 84}[Mathf.Min(3, (int)(p * 5))]); break;
                case GameSound.Reward: hz = p < .3f ? 659 : p < .6f ? 784 : 1047; break;
            }
            phase += 2 * Mathf.PI * hz / Rate;
            float env = Mathf.Min(t / .012f, 1) * Mathf.Pow(1 - p, 1.7f);
            data[i] = .48f * env * ((1 - grit) * (Mathf.Sin(phase) + harmonic * Mathf.Sin(phase * 2)) + grit * (float)(noise.NextDouble() * 2 - 1));
        }
        var clip = AudioClip.Create(sound.ToString(), data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        effects[sound] = clip;
        return clip;
    }

    static float Midi(int note) => 440 * Mathf.Pow(2, (note - 69) / 12f);

    static AudioClip Compose()
    {
        // 16 bars, 100 BPM: soft marimba, warm bass, airy chords, brushed pulse.
        const float beat = .6f;
        var data = new float[(int)(64 * beat * Rate)];
        int[] roots = { 57, 53, 60, 55 };
        int[] melody = { 81, 0, 84, 88, 0, 84, 79, 0, 81, 84, 0, 76, 79, 0, 76, 0 };
        for (int bar = 0; bar < 16; bar++)
        {
            int root = roots[bar % 4];
            for (int chord = 0; chord < 3; chord++)
                Note(data, bar * 4 * beat, 4 * beat, root + (chord == 0 ? 12 : chord == 1 ? (bar % 4 == 0 ? 15 : 16) : 19), .045f, false);
            for (int b = 0; b < 4; b++)
            {
                Note(data, (bar * 4 + b) * beat, .48f, root - 12 + (b == 2 ? 7 : 0), .12f, true);
                int note = melody[(bar % 4) * 4 + b];
                if (note != 0) Note(data, (bar * 4 + b + (b % 2 == 1 ? .08f : 0)) * beat, 1.1f, note + (bar >= 8 ? -12 : 0), .14f, true);
                Note(data, (bar * 4 + b + .5f) * beat, .06f, 98, .016f, true);
            }
        }
        // Circular note tails make the loop continuous; keep generous headroom.
        for (int i = 0; i < data.Length; i++) data[i] = Mathf.Clamp(data[i], -.8f, .8f);
        var clip = AudioClip.Create("Moonlit Chase - original 100 BPM", data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void Note(float[] data, float start, float duration, int midi, float gain, bool pluck)
    {
        int offset = (int)(start * Rate), count = (int)(duration * Rate);
        float hz = Midi(midi);
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / Rate, p = (float)i / count;
            float env = pluck ? Mathf.Min(t * 100, 1) * Mathf.Exp(-6 * p) * Mathf.Clamp01((1 - p) * 15) : Mathf.Pow(Mathf.Sin(Mathf.PI * p), 2);
            float phase = 2 * Mathf.PI * hz * t;
            data[(offset + i) % data.Length] += gain * env * (Mathf.Sin(phase) + .18f * Mathf.Sin(phase * 2));
        }
    }
}
