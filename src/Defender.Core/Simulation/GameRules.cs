namespace Defender.Core.Simulation;

/// <summary>
/// Operator ("CMOS") adjustments. Defaults are the factory settings (romc8.src:801-814; operator manual
/// 16P-3001-103): 3 ships, bonus ship + smart bomb every 10,000, starting difficulty 5, ceiling 15, restore every 5.
/// </summary>
public sealed record GameRules
{
    public int Ships { get; init; } = 3;
    public int ReplayEvery { get; init; } = 10_000;
    public int InitialDifficulty { get; init; } = 5;
    public int DifficultyCeiling { get; init; } = 15;
    public int RestoreWave { get; init; } = 5;

    public GameRules Validated()
    {
        if (Ships is < 1 or > 9) throw new ArgumentOutOfRangeException(nameof(Ships));
        if (ReplayEvery is < 0 or > 990_000) throw new ArgumentOutOfRangeException(nameof(ReplayEvery));
        if (InitialDifficulty is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(InitialDifficulty));
        if (DifficultyCeiling is < 0 or > 30) throw new ArgumentOutOfRangeException(nameof(DifficultyCeiling));
        if (RestoreWave is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(RestoreWave));
        return this;
    }
}

/// <summary>
/// Explicit Classic/Modern differences. Everything not listed here is shared, so Modern can never
/// silently change wave content, physics or scoring.
/// </summary>
public sealed record GamePolicy
{
    public GameMode Mode { get; init; }
    /// <summary>Modern opt-in: holding fire repeats. The original is strictly one shot per press (defa7.src:760-790).</summary>
    public bool HoldToFire { get; init; }
    public int HoldToFireInterval { get; init; } = 8;
    /// <summary>Classic redraws the scanner every 8 frames like the original SCPROC; Modern every frame.</summary>
    public bool ScannerEveryFrame { get; init; }
    /// <summary>Modern: suppress full-screen flashes and rapid colour cycling (photosensitivity).</summary>
    public bool SuppressFlashes { get; init; }
    public bool AllowPause { get; init; } = true;

    public static GamePolicy Classic { get; } = new() { Mode = GameMode.Classic };
    public static GamePolicy Modern { get; } = new() { Mode = GameMode.Modern, ScannerEveryFrame = true, SuppressFlashes = false };
}
