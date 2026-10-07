using Defender.Core;
using Defender.Core.Audio;
using Defender.Core.Input;
using Defender.Core.Scoring;
using Defender.Core.Simulation;
using Defender.Infrastructure.Audio;
using Defender.Infrastructure.Input;
using Defender.Infrastructure.Persistence;

namespace Defender.Avalonia;

/// <summary>
/// Owns the simulation and the infrastructure services, and runs input → fixed ticks → audio per frame.
/// UI-thread only.
/// </summary>
public sealed class GameHost : IDisposable
{
    private readonly InputEdgeDetector _edges = new();
    private readonly HashSet<string> _heldKeys = new();
    private readonly Storage _storage;
    private JsonStore<HighScoreFile> _hsStore = null!;

    public GameHost(string? dataDir = null, ISoundEngine? audio = null, bool enableGamepad = true)
    {
        _storage = new Storage(dataDir ?? AppPaths.DefaultDataDirectory());
        var settings = _storage.Settings().Load();
        Settings = settings.Value.Sanitized();
        if (settings.Message is { } m) Messages.Add(m);

        Audio = audio ?? SdlSoundEngine.CreateOrFallback();
        if (!Audio.IsAvailable) Messages.Add(Audio.Status);
        Gamepad = enableGamepad ? new SdlGamepad() : null;
        Scheduler = new FixedStepScheduler(Arcade.TicksPerSecond);
        Session = CreateSession();
        ApplySettings();
    }

    public GameSettings Settings { get; }
    public ISoundEngine Audio { get; }
    public SdlGamepad? Gamepad { get; }
    public FixedStepScheduler Scheduler { get; }
    public GameSession Session { get; private set; }
    public FrameSnapshot Snapshot { get; } = new();
    public List<string> Messages { get; } = new();

    /// <summary>Diagnostic/demo: when set, input comes from the scripted pilot instead of devices.</summary>
    public Autopilot? Autopilot { get; set; }

    private GameSession CreateSession()
    {
        _hsStore = _storage.HighScores(Settings.Mode.ToString());
        var loaded = _hsStore.Load();
        if (loaded.Message is { } m) Messages.Add(m);
        var table = new HighScoreTable();
        table.LoadFrom(loaded.Value.Entries);
        var s = new GameSession(policy: PolicyFor(Settings), highScores: table);
        s.HighScoreCommitted += _ => SaveHighScores();
        return s;
    }

    public static GamePolicy PolicyFor(GameSettings s) => s.Mode == GameMode.Classic
        ? GamePolicy.Classic
        : GamePolicy.Modern with { HoldToFire = s.HoldToFire, SuppressFlashes = s.FlickerSuppression || s.ReducedMotion };

    public void SaveHighScores()
    {
        try { _hsStore.Save(new HighScoreFile { Entries = Session.HighScores.Entries.ToList() }); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Messages.Add("Could not save high scores: " + ex.Message); }
    }

    public void SaveSettings()
    {
        try { _storage.Settings().Save(Settings); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Messages.Add("Could not save settings: " + ex.Message); }
    }

    /// <summary>Re-applies settings. Switching preset replaces the session (only allowed outside a game).</summary>
    public void ApplySettings()
    {
        Settings.Sanitized();
        bool modern = Settings.Mode == GameMode.Modern;
        Scheduler.TimeScale = modern ? Settings.GameSpeed : 1.0;
        Audio.EffectsVolume = Settings.EffectsVolume;
        Audio.AmbienceVolume = Settings.AmbienceVolume;
        Audio.Muted = Settings.AudioMuted;
        if (Session.Policy.Mode != Settings.Mode && Session.State == SessionState.Attract) Session = CreateSession();
        else if (Session.Policy.Mode == Settings.Mode) Session.Policy = PolicyFor(Settings);
    }

    public bool CanChangeMode => Session.State == SessionState.Attract;

    public void KeyDown(string key) { _heldKeys.Add(key); SampleDevices(); }
    public void KeyUp(string key) { _heldKeys.Remove(key); SampleDevices(); }
    public void ReleaseAllKeys() { _heldKeys.Clear(); SampleDevices(); }

    private void SampleDevices()
    {
        var b = Settings.Bindings;
        bool Held(LogicalButton lb) => b.Keyboard.TryGetValue(lb, out var keys) && keys.Any(_heldKeys.Contains);
        var levels = new InputLevels
        {
            Thrust = Held(LogicalButton.Thrust), Reverse = Held(LogicalButton.Reverse),
            Up = Held(LogicalButton.Up), Down = Held(LogicalButton.Down), Fire = Held(LogicalButton.Fire),
            SmartBomb = Held(LogicalButton.SmartBomb), Hyperspace = Held(LogicalButton.Hyperspace),
            Pause = Held(LogicalButton.Pause), Start = Held(LogicalButton.Start),
        };
        Gamepad?.Poll(b, ref levels);
        _edges.Sample(levels);
    }

    /// <summary>One presented frame: sample devices, run whole ticks, drive audio, build the snapshot.</summary>
    public void Frame(TimeSpan elapsed)
    {
        SampleDevices();
        int ticks = Scheduler.Advance(elapsed);
        for (int i = 0; i < ticks; i++)
        {
            var input = _edges.Consume(Session.Player.Facing);
            if (Autopilot is not null) input = Autopilot.Next(Session) with { PausePressed = input.PausePressed };
            Session.Step(input);
            foreach (var snd in Session.Sounds) Audio.Play(snd);
        }
        Audio.SetLooping(SoundId.Thrust, Session.ThrustSoundOn && !Session.Paused);
        Session.BuildSnapshot(Snapshot);
    }

    public void Dispose()
    {
        Audio.Dispose();
        Gamepad?.Dispose();
    }
}
