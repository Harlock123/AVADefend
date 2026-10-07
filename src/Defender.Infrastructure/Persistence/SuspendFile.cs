using Defender.Core.Simulation;

namespace Defender.Infrastructure.Persistence;

/// <summary>Modern-mode suspend slot. Consumed (deleted) on resume so it cannot act as a repeatable save state.</summary>
public sealed class SuspendFile : IVersioned
{
    public const int CurrentSchema = 1;
    public int SchemaVersion { get; set; } = CurrentSchema;
    public DateTime SavedUtc { get; set; }
    public SuspendData? Data { get; set; }
}
