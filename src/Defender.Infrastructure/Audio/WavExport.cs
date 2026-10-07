using Defender.Core.Audio;

namespace Defender.Infrastructure.Audio;

/// <summary>Writes the synthesised effects as 16-bit mono WAV files for auditioning.</summary>
public static class WavExport
{
    public static IReadOnlyList<string> ExportAll(string directory)
    {
        Directory.CreateDirectory(directory);
        var written = new List<string>();
        for (var id = (SoundId)0; id < SoundId.Count; id++)
        {
            var clip = SoundSynth.Render(id);
            if (id == SoundId.Thrust) clip = Enumerable.Repeat(clip, 4).SelectMany(c => c).ToArray(); // a few loops
            var path = Path.Combine(directory, $"{(int)id:00}-{id}.wav");
            Write(path, clip, SoundSynth.SampleRate);
            written.Add(path);
        }
        return written;
    }

    public static void Write(string path, float[] samples, int sampleRate)
    {
        using var w = new BinaryWriter(File.Create(path));
        int dataBytes = samples.Length * 2;
        w.Write("RIFF"u8); w.Write(36 + dataBytes); w.Write("WAVE"u8);
        w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(sampleRate); w.Write(sampleRate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8); w.Write(dataBytes);
        foreach (float s in samples) w.Write((short)Math.Clamp(s * 32767f, -32768f, 32767f));
    }
}
