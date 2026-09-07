using UnityEngine;

// A scene-local effects bus. Music uses the existing menu/gameplay source.
public static class AudioManager
{
    const string MusicMutedKey = "MusicMuted", SfxMutedKey = "SfxMuted";
    static AudioSource musicSource, effectsSource;
    public static bool MusicMuted
    {
        get => PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
        set { PlayerPrefs.SetInt(MusicMutedKey, value ? 1 : 0); if (musicSource != null) musicSource.mute = value; }
    }
    public static bool SfxMuted
    {
        get => PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;
        set { PlayerPrefs.SetInt(SfxMutedKey, value ? 1 : 0); if (effectsSource != null) effectsSource.mute = value; }
    }
    public static void RegisterMusicSource(AudioSource source)
    {
        if (source == null) return;
        if (musicSource != null && musicSource != source) musicSource.Stop();
        musicSource = source;
        source.mute = MusicMuted;
    }
    // A scene that brings its own music keeps it, at the volume it was mixed
    // at; the written score is what plays where nobody has said otherwise - the
    // menu, and anything added later that has no track of its own.
    public static void StartMusic(AudioSource source)
    {
        if (source == null) source = new GameObject("Game Music").AddComponent<AudioSource>();
        RegisterMusicSource(source);
        if (source.clip == null)
        {
            source.clip = GameAudioClips.Music;
            source.volume = .32f;
        }
        source.loop = true;
        source.spatialBlend = 0;
        source.Play();
    }
    public static void Play(GameSound sound)
    {
        if (SfxMuted) return;
        if (effectsSource == null)
        {
            effectsSource = new GameObject("Game Effects").AddComponent<AudioSource>();
            effectsSource.playOnAwake = false;
            effectsSource.spatialBlend = 0;
            effectsSource.volume = .55f;
        }
        effectsSource.PlayOneShot(GameAudioClips.Get(sound));
    }
}
