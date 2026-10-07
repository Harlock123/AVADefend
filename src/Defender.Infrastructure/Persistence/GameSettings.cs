using Defender.Core;
using Defender.Core.Input;

namespace Defender.Infrastructure.Persistence;

public sealed class GameSettings : IVersioned
{
    public const int CurrentSchema = 1;
    public int SchemaVersion { get; set; } = CurrentSchema;

    public GameMode Mode { get; set; } = GameMode.Classic;

    // Audio
    public float EffectsVolume { get; set; } = 0.8f;
    public float AmbienceVolume { get; set; } = 0.6f;
    public bool AudioMuted { get; set; }

    // Display
    public bool Fullscreen { get; set; }
    public bool SmoothScaling { get; set; }        // Modern only: bilinear instead of nearest
    public bool IntegerScaling { get; set; }
    public bool FlickerSuppression { get; set; } = true; // Modern only
    public bool ReducedMotion { get; set; }        // Modern only: no screen flashes/shake
    public bool ShowControlHints { get; set; } = true;

    // Modern only, opt-in: hold fire to repeat (the original is one shot per press).
    public bool HoldToFire { get; set; }

    /// <summary>Cocktail-table presentation: the picture rotates 180° while player two is up (both presets).</summary>
    public bool CocktailFlip { get; set; }

    /// <summary>Modern only: draw scanner blips 3×3 for readability on large displays.</summary>
    public bool BoldScanner { get; set; }

    /// <summary>Modern only: which suspend slot (1-3) is written on close and resumed.</summary>
    public int SuspendSlot { get; set; } = 1;

    // Accessibility (Modern only; disclosed on HUD when != 1)
    public double GameSpeed { get; set; } = 1.0;

    public InputBindings Bindings { get; set; } = InputBindings.CreateDefault();

    public GameSettings Sanitized()
    {
        EffectsVolume = Clamp01(EffectsVolume);
        AmbienceVolume = Clamp01(AmbienceVolume);
        GameSpeed = double.IsFinite(GameSpeed) ? Math.Clamp(GameSpeed, 0.5, 1.0) : 1.0;
        SuspendSlot = Math.Clamp(SuspendSlot, 1, 3);
        if (!Enum.IsDefined(Mode)) Mode = GameMode.Classic;
        Bindings = (Bindings ?? InputBindings.CreateDefault()).Sanitized();
        return this;
    }

    private static float Clamp01(float v) => float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : 0.8f;
}
