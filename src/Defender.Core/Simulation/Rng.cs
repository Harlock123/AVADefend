namespace Defender.Core.Simulation;

/// <summary>
/// Injectable deterministic RNG. The original's LFSR (defa7.src:945) is not replicated bit-for-bit
/// (documented in FIDELITY.md); xorshift32 gives reproducible, serialisable streams for tests/replays.
/// </summary>
public interface IRandom
{
    uint State { get; set; }
    byte NextByte();
    int Next(int maxExclusive);
}

public sealed class XorShiftRandom : IRandom
{
    private uint _s;
    public XorShiftRandom(uint seed) => _s = seed == 0 ? 0x9E3779B9u : seed;

    public uint State { get => _s; set => _s = value == 0 ? 0x9E3779B9u : value; }

    private uint NextUInt()
    {
        uint x = _s;
        x ^= x << 13; x ^= x >> 17; x ^= x << 5;
        return _s = x;
    }

    public byte NextByte() => (byte)(NextUInt() >> 24);

    public int Next(int maxExclusive) => maxExclusive <= 1 ? 0 : (int)((NextUInt() >> 1) % (uint)maxExclusive);
}

public static class RandomExtensions
{
    /// <summary>Source RMAX semantics: random 0..n, then +1 (so 1..n+1). INF from defb6.src:118-124.</summary>
    public static int RMax(this IRandom r, int n) => n <= 0 ? 1 : r.Next(n + 1) + 1;
    public static int Range(this IRandom r, int minInclusive, int maxInclusive) => minInclusive + r.Next(maxInclusive - minInclusive + 1);
    public static int Sign(this IRandom r) => (r.NextByte() & 1) == 0 ? 1 : -1;
}
