using Defender.Core.Audio;

namespace Defender.Infrastructure.Audio;

public interface ISoundEngine : IDisposable
{
    bool IsAvailable { get; }
    string Status { get; }
    float EffectsVolume { get; set; }
    float AmbienceVolume { get; set; }
    bool Muted { get; set; }
    void Play(SoundId id);
    /// <summary>Starts or stops a looping sound (e.g. thrust). Idempotent.</summary>
    void SetLooping(SoundId id, bool on);
    void StopAll();
}

public sealed class NullSoundEngine(string status = "Audio disabled") : ISoundEngine
{
    public bool IsAvailable => false;
    public string Status { get; } = status;
    public float EffectsVolume { get; set; } = 1;
    public float AmbienceVolume { get; set; } = 1;
    public bool Muted { get; set; }
    public void Play(SoundId id) { }
    public void SetLooping(SoundId id, bool on) { }
    public void StopAll() { }
    public void Dispose() { }
}
