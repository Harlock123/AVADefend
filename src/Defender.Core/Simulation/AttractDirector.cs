using Defender.Core.Scoring;

namespace Defender.Core.Simulation;

public enum AttractPhase { Title, Demo, HallOfFame }

/// <summary>
/// Attract-mode cycle: title + scoring legend → demonstration flight → hall of fame → repeat.
/// The demo is a separate, silent <see cref="GameSession"/> flown by <see cref="Autopilot"/> with a
/// throwaway score book, so it can never affect real scores or settings. The original's attract
/// sequence was not studied in detail; this cycle is a reconstruction (FIDELITY.md).
/// </summary>
public sealed class AttractDirector
{
    public const int TitleFrames = 600, DemoMaxFrames = 1500, HallFrames = 600, DemoEndAfterDeathFrames = 150;

    private readonly Func<GamePolicy> _policy;
    private readonly IRandom _seeds;
    private Autopilot _pilot = new();
    private int _deathFrames;

    public AttractDirector(Func<GamePolicy> policy, IRandom seeds)
    {
        _policy = policy;
        _seeds = seeds;
    }

    public AttractPhase Phase { get; private set; } = AttractPhase.Title;
    public int PhaseTimer { get; private set; }
    public GameSession? Demo { get; private set; }

    public void Reset()
    {
        Phase = AttractPhase.Title;
        PhaseTimer = 0;
        Demo = null;
    }

    public void Step()
    {
        PhaseTimer++;
        switch (Phase)
        {
            case AttractPhase.Title when PhaseTimer >= TitleFrames:
                StartDemo();
                break;
            case AttractPhase.Demo:
                StepDemo();
                break;
            case AttractPhase.HallOfFame when PhaseTimer >= HallFrames:
                Enter(AttractPhase.Title);
                break;
        }
    }

    private void Enter(AttractPhase p)
    {
        Phase = p;
        PhaseTimer = 0;
        if (p != AttractPhase.Demo) Demo = null;
    }

    private void StartDemo()
    {
        Enter(AttractPhase.Demo);
        Demo = new GameSession(policy: _policy() with { AllowPause = false }, rng: new XorShiftRandom((uint)_seeds.Next(int.MaxValue) + 1),
                               highScores: new HighScoreBook());
        Demo.StartGame();
        _pilot = new Autopilot();
        _deathFrames = 0;
    }

    private void StepDemo()
    {
        var d = Demo!;
        d.Step(_pilot.Next(d));
        if (d.State is SessionState.Dying or SessionState.GameOver) _deathFrames++;
        if (PhaseTimer >= DemoMaxFrames || _deathFrames >= DemoEndAfterDeathFrames || d.State is SessionState.EnterInitials or SessionState.Attract)
            Enter(AttractPhase.HallOfFame);
    }
}
