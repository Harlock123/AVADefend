using Defender.Core.Audio;

namespace Defender.Infrastructure.Audio;

/// <summary>
/// Procedurally synthesised effects. No samples are taken from the original ROMs or MAME recordings;
/// these are original approximations of the *character* of the Williams sound board (pitch sweeps,
/// filtered noise, square-wave warbles). See THIRD_PARTY.md / asset provenance.
/// </summary>
public static class SoundSynth
{
    public const int SampleRate = 44100;

    public static float[][] RenderAll()
    {
        var clips = new float[(int)SoundId.Count][];
        for (int i = 0; i < clips.Length; i++) clips[i] = Render((SoundId)i);
        return clips;
    }

    public static float[] Render(SoundId id) => id switch
    {
        SoundId.Thrust => ThrustLoop(),
        SoundId.Fire => Sweep(0.16, 2400, 400, Square, 0.35, decay: 2),
        SoundId.EnemyShot => Sweep(0.06, 1800, 1500, Square, 0.15),
        SoundId.LanderMaterialize => Warble(0.35, 300, 900, 30, 0.2),
        SoundId.Abduction => Siren(0.8, 500, 700, 8, 0.3),
        SoundId.HumanoidFalling => Sweep(1.4, 1400, 300, Triangle, 0.25, vibratoHz: 10),
        SoundId.HumanoidCaught => Arpeggio([523, 659, 784, 1047], 0.06, 0.3),
        SoundId.HumanoidLanded => Arpeggio([1047, 1319, 1568], 0.05, 0.3),
        SoundId.HumanoidDies => Sweep(0.7, 2200, 200, Square, 0.3, vibratoHz: 25),
        SoundId.MutantCreated => Warble(0.6, 120, 1500, 60, 0.3),
        SoundId.EnemyExplode => Noise(0.45, 0.6, cutoffStart: 0.9, cutoffEnd: 0.05, seed: 11),
        SoundId.PodExplode => Noise(0.7, 0.7, cutoffStart: 0.7, cutoffEnd: 0.03, seed: 12),
        SoundId.BaiterAppear => Warble(0.5, 900, 1200, 40, 0.2),
        SoundId.PlayerExplode => Mix(Noise(2.2, 0.8, 0.8, 0.01, seed: 13), Sweep(2.2, 220, 30, Square, 0.3, decay: 1.5)),
        SoundId.SmartBomb => Mix(Noise(1.3, 0.9, 1.0, 0.02, seed: 14), Sweep(1.3, 120, 40, Sine, 0.5, decay: 1.5)),
        SoundId.Hyperspace => Sweep(0.6, 200, 3000, Square, 0.25, vibratoHz: 30),
        SoundId.PlanetExplode => Mix(Noise(3.5, 0.9, 0.6, 0.005, seed: 15), Sweep(3.5, 90, 25, Sine, 0.6, decay: 0.8)),
        SoundId.WaveStart => Arpeggio([262, 330, 392, 523, 659, 784], 0.07, 0.3),
        SoundId.WaveBonus => Sweep(0.05, 1200, 1200, Square, 0.2),
        SoundId.ExtraLife => Arpeggio([784, 1047, 1319, 1568, 2093, 1568, 2093], 0.06, 0.3),
        SoundId.GameStart => Sweep(0.9, 150, 1600, Square, 0.25, vibratoHz: 14),
        SoundId.GameOver => Arpeggio([523, 392, 330, 262, 196], 0.18, 0.3),
        _ => [],
    };

    private static float Square(double phase) => phase % 1.0 < 0.5 ? 1f : -1f;
    private static float Triangle(double phase) { double p = phase % 1.0; return (float)(p < 0.5 ? 4 * p - 1 : 3 - 4 * p); }
    private static float Sine(double phase) => MathF.Sin((float)(phase * 2 * Math.PI));

    private static float[] Sweep(double secs, double f0, double f1, Func<double, float> osc, double amp,
                                 double decay = 0, double vibratoHz = 0)
    {
        int n = (int)(secs * SampleRate);
        var buf = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            double f = f0 * Math.Pow(f1 / f0, t);
            if (vibratoHz > 0) f *= 1 + 0.06 * Math.Sin(2 * Math.PI * vibratoHz * i / SampleRate);
            phase += f / SampleRate;
            double env = decay > 0 ? Math.Pow(1 - t, decay) : 1 - t * 0.3;
            buf[i] = (float)(osc(phase) * amp * env * Edge(i, n));
        }
        return buf;
    }

    private static float[] Warble(double secs, double fLow, double fHigh, double rateHz, double amp)
    {
        int n = (int)(secs * SampleRate);
        var buf = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double tri = Triangle(rateHz * i / SampleRate) * 0.5 + 0.5;
            double f = fLow + (fHigh - fLow) * tri;
            phase += f / SampleRate;
            buf[i] = (float)(Square(phase) * amp * Edge(i, n));
        }
        return buf;
    }

    private static float[] Siren(double secs, double fa, double fb, double rateHz, double amp)
    {
        int n = (int)(secs * SampleRate);
        var buf = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            double f = Square(rateHz * i / SampleRate) > 0 ? fa : fb;
            phase += f / SampleRate;
            buf[i] = (float)(Square(phase) * amp * Edge(i, n));
        }
        return buf;
    }

    private static float[] Arpeggio(double[] notes, double noteSecs, double amp)
    {
        int per = (int)(noteSecs * SampleRate);
        var buf = new float[per * notes.Length];
        double phase = 0;
        for (int k = 0; k < notes.Length; k++)
            for (int i = 0; i < per; i++)
            {
                phase += notes[k] / SampleRate;
                buf[k * per + i] = (float)(Square(phase) * amp * (1 - 0.5 * i / per) * Edge(i, per));
            }
        return buf;
    }

    /// <summary>Decaying white noise through a one-pole low-pass whose cutoff glides down (the "boom").</summary>
    private static float[] Noise(double secs, double amp, double cutoffStart, double cutoffEnd, int seed)
    {
        int n = (int)(secs * SampleRate);
        var buf = new float[n];
        var rng = new Random(seed);
        double y = 0, held = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / n;
            // Sample-and-hold at a falling rate gives the crunchy, bit-crushed texture of DAC noise.
            if (i % (1 + (int)(t * 12)) == 0) held = rng.NextDouble() * 2 - 1;
            double a = cutoffStart * Math.Pow(cutoffEnd / cutoffStart, t);
            y += a * (held - y);
            buf[i] = (float)(y * amp * Math.Pow(1 - t, 1.6) * Edge(i, n));
        }
        return buf;
    }

    /// <summary>Seamlessly loopable low rumble for the thrust engine.</summary>
    private static float[] ThrustLoop()
    {
        int n = SampleRate / 2;
        var rng = new Random(7);
        var raw = new float[n];
        double y = 0;
        for (int i = 0; i < n; i++)
        {
            y += 0.08 * ((rng.NextDouble() * 2 - 1) - y);
            raw[i] = (float)(y * 0.9);
        }
        // Crossfade tail into head so the loop point is click-free.
        int xf = 2048;
        var buf = new float[n - xf];
        for (int i = 0; i < buf.Length; i++) buf[i] = raw[i + xf];
        for (int i = 0; i < xf; i++)
        {
            float t = (float)i / xf;
            buf[buf.Length - xf + i] = raw[n - xf + i] * (1 - t) + raw[i] * t;
        }
        return buf;
    }

    private static float[] Mix(float[] a, float[] b)
    {
        var r = new float[Math.Max(a.Length, b.Length)];
        for (int i = 0; i < r.Length; i++) r[i] = (i < a.Length ? a[i] : 0) + (i < b.Length ? b[i] : 0);
        return r;
    }

    /// <summary>3 ms attack/release ramps to avoid clicks.</summary>
    private static double Edge(int i, int n)
    {
        const int ramp = 132;
        if (i < ramp) return (double)i / ramp;
        if (i > n - ramp) return Math.Max(0, (double)(n - i) / ramp);
        return 1;
    }
}
