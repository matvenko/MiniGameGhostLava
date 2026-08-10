using UnityEngine;

// Persists music/SFX mute state across sessions (PlayerPrefs) and applies
// the music mute to whichever AudioSource registers itself. SFX (currently
// just the coin pickup, played via PlayClipAtPoint with no persistent
// AudioSource) is muted by callers checking SfxMuted before playing.
public static class AudioManager
{
    private const string MusicMutedKey = "MusicMuted";
    private const string SfxMutedKey = "SfxMuted";

    private static AudioSource _musicSource;

    public static bool MusicMuted
    {
        get => PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(MusicMutedKey, value ? 1 : 0);
            if (_musicSource != null) _musicSource.mute = value;
        }
    }

    public static bool SfxMuted
    {
        get => PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;
        set => PlayerPrefs.SetInt(SfxMutedKey, value ? 1 : 0);
    }

    public static void RegisterMusicSource(AudioSource source)
    {
        _musicSource = source;
        if (_musicSource != null) _musicSource.mute = MusicMuted;
    }
}
