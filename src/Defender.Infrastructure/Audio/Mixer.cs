using Defender.Core.Audio;

namespace Defender.Infrastructure.Audio;

/// <summary>
/// Thread-safe software mixer over pre-rendered mono float clips. A fixed voice pool with priority-based
/// stealing keeps heavy explosion bursts from dropping the important sounds (player death, bonus) or
/// exhausting memory. Loops are keyed by <see cref="SoundId"/> and fade briefly on stop to avoid clicks.
/// </summary>
public sealed class Mixer
{
    private sealed class Voice
    {
        public float[] Clip = [];
        public int Pos;
        public SoundId Id;
        public int Priority;
        public bool Loop;
        public bool Active;
        public bool Ambience;
        public int FadeOut = -1; // samples left in fade
        public long Started;
    }

    public const int MaxVoices = 16;
    private const int FadeSamples = 256;
    private readonly Voice[] _voices = Enumerable.Range(0, MaxVoices).Select(_ => new Voice()).ToArray();
    private readonly float[][] _clips;
    private readonly object _lock = new();
    private long _serial;

    public Mixer(float[][] clips)
    {
        if (clips.Length < (int)SoundId.Count) throw new ArgumentException("Missing clips", nameof(clips));
        _clips = clips;
    }

    public float EffectsVolume { get; set; } = 0.8f;
    public float AmbienceVolume { get; set; } = 0.6f;
    public bool Muted { get; set; }

    /// <summary>
    /// Classic sound model: the Williams board plays one sound at a time. A new sound starts only if its script
    /// priority is at least the current one's (SNDLD, defa7.src:708-722); thrust is heard only when no other
    /// sound is playing (SNDSEQ). Modern leaves this off and mixes everything.
    /// </summary>
    public bool Monophonic { get; set; }

    /// <summary>Script priorities from the original sound table (defa7.src:659-691).</summary>
    public static int OriginalPriority(SoundId id) => id switch
    {
        SoundId.ExtraLife => 0xFF,
        SoundId.PlayerExplode or SoundId.GameStart => 0xF0,
        SoundId.PlanetExplode or SoundId.SmartBomb => 0xE8,
        SoundId.HumanoidCaught or SoundId.HumanoidLanded or SoundId.HumanoidDies => 0xE0,
        SoundId.HumanoidFalling => 0xD8,
        SoundId.LanderMaterialize or SoundId.PodExplode or SoundId.MutantHit or SoundId.BaiterHit or SoundId.BomberHit
            or SoundId.EnemyExplode or SoundId.Abduction => 0xD0,
        SoundId.LanderSuck => 0xC8,
        _ => 0xC0, // laser, enemy shots, swarmer hit
    };

    public int ActiveVoices { get { lock (_lock) return _voices.Count(v => v.Active); } }

    public static int PriorityOf(SoundId id) => id switch
    {
        SoundId.PlayerExplode or SoundId.PlanetExplode or SoundId.GameOver or SoundId.ExtraLife => 10,
        SoundId.SmartBomb or SoundId.Hyperspace or SoundId.WaveStart or SoundId.WaveBonus or SoundId.GameStart => 8,
        SoundId.Abduction or SoundId.HumanoidCaught or SoundId.HumanoidLanded or SoundId.HumanoidDies or SoundId.MutantCreated => 6,
        SoundId.PodExplode or SoundId.BaiterAppear or SoundId.HumanoidFalling => 5,
        SoundId.EnemyExplode or SoundId.LanderMaterialize => 4,
        SoundId.Fire => 3,
        SoundId.Thrust => 9, // loop must never be stolen
        _ => 2,
    };

    public void Play(SoundId id)
    {
        var clip = _clips[(int)id];
        if (clip.Length == 0) return;
        int pri = PriorityOf(id);
        lock (_lock)
        {
            if (Monophonic)
            {
                var current = _voices.FirstOrDefault(x => x.Active && !x.Loop);
                if (current is not null && OriginalPriority(id) < OriginalPriority(current.Id)) return;
                foreach (var v1 in _voices) if (v1.Active && !v1.Loop) v1.Active = false;
                var free = _voices.FirstOrDefault(x => !x.Active);
                if (free is not null) Start(free, id, clip, pri, loop: false);
                return;
            }
            // Rapid re-triggers of the same short effect restart the existing voice instead of stacking.
            var v = _voices.FirstOrDefault(x => x.Active && !x.Loop && x.Id == id && x.Pos < 400)
                    ?? _voices.FirstOrDefault(x => !x.Active)
                    ?? _voices.Where(x => !x.Loop && x.Priority <= pri).OrderBy(x => x.Priority).ThenBy(x => x.Started).FirstOrDefault();
            if (v is null) return; // everything playing is more important
            Start(v, id, clip, pri, loop: false);
        }
    }

    public void SetLooping(SoundId id, bool on)
    {
        lock (_lock)
        {
            var existing = _voices.FirstOrDefault(x => x.Active && x.Loop && x.Id == id);
            if (on)
            {
                if (existing is not null) { existing.FadeOut = -1; return; } // revive a loop that was fading out
                var clip = _clips[(int)id];
                if (clip.Length == 0) return;
                var v = _voices.FirstOrDefault(x => !x.Active)
                        ?? _voices.Where(x => !x.Loop).OrderBy(x => x.Priority).ThenBy(x => x.Started).FirstOrDefault();
                if (v is null) return;   // every voice is a loop: drop rather than throw
                Start(v, id, clip, PriorityOf(id), loop: true);
            }
            else if (existing is not null && existing.FadeOut < 0)
            {
                existing.FadeOut = FadeSamples;
            }
        }
    }

    public void StopAll()
    {
        lock (_lock) foreach (var v in _voices) v.Active = false;
    }

    private void Start(Voice v, SoundId id, float[] clip, int pri, bool loop)
    {
        v.Clip = clip; v.Pos = 0; v.Id = id; v.Priority = pri; v.Loop = loop; v.Active = true;
        v.FadeOut = -1; v.Started = ++_serial; v.Ambience = loop;
    }

    /// <summary>Renders mono samples into <paramref name="output"/> (overwrites). Never throws for valid spans.</summary>
    public void Mix(Span<float> output)
    {
        output.Clear();
        lock (_lock)
        {
            // Voices advance even when muted (so loops finish fading and one-shots play out silently).
            foreach (var v in _voices)
            {
                if (!v.Active) continue;
                float vol = v.Ambience ? AmbienceVolume : EffectsVolume;
                // Mono board: the thrust drone is silent while any other sound plays.
                if (Monophonic && v.Loop && _voices.Any(x => x.Active && !x.Loop)) vol = 0;
                var clip = v.Clip;
                for (int i = 0; i < output.Length; i++)
                {
                    if (v.Pos >= clip.Length)
                    {
                        if (v.Loop) v.Pos = 0;
                        else { v.Active = false; break; }
                    }
                    float g = vol;
                    if (v.FadeOut >= 0)
                    {
                        if (v.FadeOut == 0) { v.Active = false; break; }
                        g *= v.FadeOut / (float)FadeSamples;
                        v.FadeOut--;
                    }
                    output[i] += clip[v.Pos++] * g;
                }
            }
        }
        if (Muted) { output.Clear(); return; }
        // Soft clip so dense explosions saturate musically instead of wrapping.
        for (int i = 0; i < output.Length; i++)
        {
            float x = output[i];
            output[i] = x / (1f + MathF.Abs(x));
        }
    }
}
