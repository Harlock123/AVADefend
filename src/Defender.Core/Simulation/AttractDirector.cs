namespace Defender.Core.Simulation;

public enum AttractPhase { Title, Demo, HallOfFame }

/// <summary>
/// Attract cycle in the original order (amode1.src): logo/title page (AMODES, ~960 frames) → Hall of Fame
/// (HALDIS, 600) → scripted instructions demo (LEDRET, ~2279) → logo page … Starts on the logo page, as at power-on.
/// Coin-skip and the "start only after the first logo page" lock are not reproduced (free play).
/// </summary>
public sealed class AttractDirector
{
    public const int TitleFrames = 960, HallFrames = 600;

    // Kept for API compatibility with earlier callers; the demo is no longer an autopilot game.
    public AttractDirector(Func<GamePolicy>? policy = null, IRandom? seeds = null) { }

    public AttractPhase Phase { get; private set; } = AttractPhase.Title;
    public int PhaseTimer { get; private set; }
    public AttractDemo? Demo { get; private set; }

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
                Enter(AttractPhase.HallOfFame);
                break;
            case AttractPhase.HallOfFame when PhaseTimer >= HallFrames:
                Enter(AttractPhase.Demo);
                Demo = new AttractDemo();
                break;
            case AttractPhase.Demo:
                Demo!.Step();
                if (Demo.Finished) Enter(AttractPhase.Title);
                break;
        }
    }

    private void Enter(AttractPhase p)
    {
        Phase = p;
        PhaseTimer = 0;
        if (p != AttractPhase.Demo) Demo = null;
    }
}
