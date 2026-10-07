using Defender.Core.Audio;
using Silk.NET.SDL;
using Thread = System.Threading.Thread;
using ThreadPriority = System.Threading.ThreadPriority;

namespace Defender.Infrastructure.Audio;

/// <summary>
/// SDL2 audio output using the push (queue) API from a dedicated mixer thread, so no native→managed
/// callback is required. Any failure during initialisation yields an engine with IsAvailable=false;
/// callers should then fall back to <see cref="NullSoundEngine"/>.
/// </summary>
public sealed unsafe class SdlSoundEngine : ISoundEngine
{
    private const int ChunkFrames = 512;
    private const int TargetQueuedFrames = ChunkFrames * 4; // ~46 ms at 44.1 kHz
    private readonly Sdl? _sdl;
    private readonly uint _device;
    private readonly Mixer? _mixer;
    private readonly Thread? _thread;
    private volatile bool _running;

    private SdlSoundEngine(Sdl sdl, uint device, Mixer mixer)
    {
        _sdl = sdl; _device = device; _mixer = mixer;
        IsAvailable = true;
        Status = "SDL audio active";
        _running = true;
        _thread = new Thread(Pump) { IsBackground = true, Name = "Defender audio", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    public bool IsAvailable { get; }
    public string Status { get; }

    public float EffectsVolume { get => _mixer!.EffectsVolume; set => _mixer!.EffectsVolume = Math.Clamp(value, 0, 1); }
    public float AmbienceVolume { get => _mixer!.AmbienceVolume; set => _mixer!.AmbienceVolume = Math.Clamp(value, 0, 1); }
    public bool Muted { get => _mixer!.Muted; set => _mixer!.Muted = value; }
    public bool Monophonic { get => _mixer!.Monophonic; set => _mixer!.Monophonic = value; }

    public static ISoundEngine CreateOrFallback()
    {
        try
        {
            var sdl = SdlProvider.Api;
            if (sdl is null) return new NullSoundEngine("SDL unavailable: " + SdlProvider.Error);
            if (sdl.InitSubSystem(Sdl.InitAudio) != 0)
                return new NullSoundEngine("SDL audio init failed: " + sdl.GetErrorS());
            var want = new AudioSpec { Freq = SoundSynth.SampleRate, Format = (ushort)Sdl.AudioF32, Channels = 1, Samples = ChunkFrames };
            AudioSpec have;
            uint dev = sdl.OpenAudioDevice((string)null!, 0, &want, &have, 0);
            if (dev == 0) return new NullSoundEngine("No audio device: " + sdl.GetErrorS());
            var mixer = new Mixer(SoundSynth.RenderAll());
            sdl.PauseAudioDevice(dev, 0);
            return new SdlSoundEngine(sdl, dev, mixer);
        }
        catch (Exception ex)
        {
            return new NullSoundEngine("Audio unavailable: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void Pump()
    {
        var buf = new float[ChunkFrames];
        try
        {
            while (_running)
            {
                uint queuedFrames = _sdl!.GetQueuedAudioSize(_device) / sizeof(float);
                if (queuedFrames < TargetQueuedFrames)
                {
                    _mixer!.Mix(buf);
                    fixed (float* p = buf) _sdl.QueueAudio(_device, p, (uint)(buf.Length * sizeof(float)));
                }
                else
                {
                    Thread.Sleep(2);
                }
            }
        }
        catch (Exception)
        {
            _running = false; // audio dies quietly; the game continues
        }
    }

    public void Play(SoundId id) => _mixer?.Play(id);
    public void SetLooping(SoundId id, bool on) => _mixer?.SetLooping(id, on);
    public void StopAll() => _mixer?.StopAll();

    public void Dispose()
    {
        _running = false;
        _thread?.Join(500);
        if (_sdl is not null && _device != 0)
        {
            _sdl.CloseAudioDevice(_device);
            _sdl.QuitSubSystem(Sdl.InitAudio);
        }
    }
}
