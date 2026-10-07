namespace Defender.Core.Simulation;

/// <summary>Wave-parameter variables, in the order of the original ELIST (phr6.src:382-405).</summary>
public enum WaveVar
{
    Landers, Bombers, Pods, Mutants, Swarmers,
    WaveTime, WaveSize,
    LanderXV, LanderYV, LanderShotTimer,
    BomberXV,
    MutantRandomY, MutantYV, MutantXV, MutantShotTimer,
    SwarmerXV, SwarmerShotTimer, SwarmerYAccelMask,
    BaiterTime, BaiterShotTimer, BaiterSeek,
}

/// <summary>One row: clamp range, intra-wave delta (every 10 s), inter-wave delta, and values for waves 1-4.</summary>
public sealed record WaveRow(WaveVar Var, int Max, int Min, int Intra, int Inter, int W1, int W2, int W3, int W4)
{
    public int Base(int wave) => Math.Min(Math.Max(wave, 1), 4) switch { 1 => W1, 2 => W2, 3 => W3, _ => W4 };
}

/// <summary>
/// Data-driven wave parameters. Default values are the WVTAB numbers from blk71.src:674-722 (game
/// rules, recorded as facts). LNDYV and SZYV are 16-bit there (MSB/LSB rows); here they are merged.
/// </summary>
public sealed class WaveTable
{
    public IReadOnlyList<WaveRow> Rows { get; }

    public WaveTable(IEnumerable<WaveRow> rows)
    {
        Rows = rows.ToList();
        Validate();
    }

    public static WaveTable Default { get; } = new(
    [
        new(WaveVar.Landers,          20,  0,   0,   0, 15, 20, 20, 20),
        new(WaveVar.Bombers,           3,  0,   0,   0,  0,  3,  4,  5),
        new(WaveVar.Pods,              6,  0,   0,   0,  0,  1,  3,  4),
        new(WaveVar.Mutants,          10,  0,   0,   0,  0,  0,  0,  0),
        new(WaveVar.Swarmers,         10,  0,   0,   0,  0,  0,  0,  0),
        new(WaveVar.WaveTime,         30,  0,   0,   0, 30, 25, 20, 16),
        new(WaveVar.WaveSize,          5,  0,   0,   0,  5,  5,  5,  5),
        new(WaveVar.LanderXV,       0x60,  0,   3,   2, 0x16, 0x1E, 0x26, 0x2E),
        new(WaveVar.LanderYV,      0x1FF,  0, 0x10,  0, 0x070, 0x0B0, 0x100, 0x100),
        new(WaveVar.LanderShotTimer,0x80, 0x10, -4, -2, 0x4A, 0x3A, 0x2A, 0x2A),
        new(WaveVar.BomberXV,       0x30,  0,   0,   0, 0x20, 0x28, 0x2C, 0x30),
        new(WaveVar.MutantRandomY,     2,  0,   0,   0,  1,  1,  2,  2),
        new(WaveVar.MutantYV,      0x1FF,  0,   8,   6, 0x062, 0x0E0, 0x102, 0x112),
        new(WaveVar.MutantXV,       0x60,  0,   8,   4, 0x0C, 0x1C, 0x24, 0x28),
        new(WaveVar.MutantShotTimer,0xFF,  8,  -2,  -2, 0x2A, 0x22, 0x1E, 0x1C),
        new(WaveVar.SwarmerXV,      0x60,  0,   8,   2, 0x16, 0x1E, 0x20, 0x22),
        new(WaveVar.SwarmerShotTimer, 40, 10,  -2,  -1, 25, 25, 25, 25),
        new(WaveVar.SwarmerYAccelMask,0x3F, 0,  0,   0, 0x1F, 0x1F, 0x1F, 0x3F),
        new(WaveVar.BaiterTime,     0xC0, 0x18,-12, -4, 0xD4, 0xC4, 0xA4, 0x94),
        new(WaveVar.BaiterShotTimer,  10,  3,  -1,  -1, 15, 13, 12, 10),
        new(WaveVar.BaiterSeek,      200, 40, -12,  -8, 240, 220, 200, 200),
    ]);

    private void Validate()
    {
        var missing = Enum.GetValues<WaveVar>().Except(Rows.Select(r => r.Var)).ToList();
        if (missing.Count > 0) throw new InvalidDataException("Wave table missing rows: " + string.Join(", ", missing));
        var dup = Rows.GroupBy(r => r.Var).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new InvalidDataException($"Wave table has duplicate row {dup.Key}");
        foreach (var r in Rows)
        {
            if (r.Min < 0 || r.Max < r.Min || r.Max > 0xFFFF)
                throw new InvalidDataException($"{r.Var}: invalid range [{r.Min},{r.Max}]");
            // Base values may exceed max (original LNDXV etc. are clamped only on delta application), but must be non-negative.
            foreach (var v in new[] { r.W1, r.W2, r.W3, r.W4 })
                if (v < 0 || v > 0xFFFF) throw new InvalidDataException($"{r.Var}: wave value {v} out of range");
            if (Math.Abs(r.Intra) > 0xFF || Math.Abs(r.Inter) > 0xFF) throw new InvalidDataException($"{r.Var}: delta out of range");
        }
    }

    public WaveRow Row(WaveVar v) => Rows.First(r => r.Var == v);
}

/// <summary>Live values for the current wave (GETWV / WDELT, defa7.src:1849-1927).</summary>
public sealed class WaveParams
{
    private readonly int[] _v = new int[Enum.GetValues<WaveVar>().Length];

    public int this[WaveVar v] { get => _v[(int)v]; set => _v[(int)v] = value; }
    public int[] Raw => _v;

    /// <summary>Base column for the wave, then the inter-wave delta applied N = min(max(wave-4,0)+initial, ceiling) times.</summary>
    public static WaveParams For(WaveTable table, int wave, int initialDifficulty, int difficultyCeiling)
    {
        var p = new WaveParams();
        int n = Math.Min(Math.Max(wave - 4, 0) + initialDifficulty, difficultyCeiling);
        foreach (var r in table.Rows)
        {
            int v = r.Base(wave);
            for (int i = 0; i < n; i++) v = ApplyDelta(v, r.Inter, r);
            p[r.Var] = v;
        }
        return p;
    }

    /// <summary>Intra-wave escalation applied every 10 s of play.</summary>
    public void ApplyIntra(WaveTable table)
    {
        foreach (var r in table.Rows) this[r.Var] = ApplyDelta(this[r.Var], r.Intra, r);
    }

    /// <summary>A delta that would cross max/min is skipped, not clamped (WDELT semantics).</summary>
    internal static int ApplyDelta(int v, int delta, WaveRow r)
    {
        if (delta == 0) return v;
        int n = v + delta;
        if (delta > 0 && n > r.Max) return v;
        if (delta < 0 && n < r.Min) return v;
        return n;
    }
}
