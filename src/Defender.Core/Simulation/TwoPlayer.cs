namespace Defender.Core.Simulation;

/// <summary>
/// Everything that belongs to one player while the other is flying (the original keeps a per-player
/// save area, PLSAV/PLRES, defa7.src:1491-1585): score, ships, bombs, wave, wave parameters, reserves,
/// humanoids and the planet.
/// </summary>
public sealed class PlayerState
{
    public int Score { get; set; }
    public int NextReplay { get; set; }
    public int Lives { get; set; }
    public int SmartBombs { get; set; }
    public int Wave { get; set; }
    public int[] Params { get; set; } = [];
    public bool PlanetActive { get; set; }
    public int[] Reserves { get; set; } = [];
    public int IntraCounter { get; set; }
    public List<Humanoid> Humanoids { get; set; } = new();
    /// <summary>Lost their last ship; the game continues with the other player.</summary>
    public bool Out { get; set; }
}

public readonly record struct PendingInitials(int Player, int Score, int Wave);

public sealed partial class GameSession
{
    private readonly PlayerState?[] _slots = new PlayerState?[2];
    private readonly Queue<PendingInitials> _initialsQueue = new();

    public int PlayerCount { get; private set; } = 1;
    public int CurrentPlayer { get; private set; }
    /// <summary>The waiting player's saved state (2-player games only).</summary>
    public PlayerState? WaitingPlayer => PlayerCount == 2 ? _slots[1 - CurrentPlayer] : null;
    public int InitialsPlayer { get; private set; }

    private PlayerState CapturePlayer() => new()
    {
        Score = Score, NextReplay = NextReplay, Lives = Lives, SmartBombs = SmartBombs, Wave = Wave,
        Params = (int[])Params.Raw.Clone(), PlanetActive = PlanetActive,
        Reserves = [LanderReserve, BomberReserve, PodReserve, MutantReserve, SwarmerReserve],
        IntraCounter = _intraCounter, Humanoids = Humanoids.Select(Clone).ToList(),
    };

    private void ApplyPlayer(PlayerState p)
    {
        Score = p.Score; NextReplay = p.NextReplay; Lives = p.Lives; SmartBombs = p.SmartBombs; Wave = p.Wave;
        Params = new WaveParams();
        p.Params.CopyTo(Params.Raw, 0);
        PlanetActive = p.PlanetActive;
        (LanderReserve, BomberReserve, PodReserve, MutantReserve, SwarmerReserve) = (p.Reserves[0], p.Reserves[1], p.Reserves[2], p.Reserves[3], p.Reserves[4]);
        _intraCounter = p.IntraCounter;
        for (int i = 0; i < Humanoids.Length; i++) Humanoids[i] = Clone(p.Humanoids[i]);
    }

    /// <summary>Fresh state for a player who has not flown yet (ships not yet consumed).</summary>
    private PlayerState FreshPlayer()
    {
        Score = 0;
        NextReplay = Rules.ReplayEvery > 0 ? Rules.ReplayEvery : int.MaxValue;
        Lives = Rules.Ships;
        SmartBombs = Rules.Ships;
        Wave = 1;
        PlanetActive = true;
        PlaceHumanoids(Arcade.HumanoidCount);
        LoadWave();
        return CapturePlayer();
    }

    /// <summary>After a death: alternate to the other player if they still have ships, else continue or end.</summary>
    private void NextTurnAfterDeath()
    {
        bool currentOut = Lives <= 0;
        if (PlayerCount == 2)
        {
            var other = _slots[1 - CurrentPlayer];
            if (other is { Out: false })
            {
                if (currentOut)
                {
                    // "PLAYER n / GAME OVER" for 96 frames before the other player continues (defa7.src:1391-1411).
                    State = SessionState.TurnOver;
                    StateTimer = 0;
                    return;
                }
                SwitchPlayers();
                return;
            }
        }
        if (!currentOut) { StartLife(consumeShip: true); return; }
        EnterGameOver();
    }

    private void SwitchPlayers()
    {
        var other = _slots[1 - CurrentPlayer]!;
        var mine = CapturePlayer();
        mine.Out = Lives <= 0;
        _slots[CurrentPlayer] = mine;
        _slots[1 - CurrentPlayer] = null;
        ApplyPlayer(other);
        CurrentPlayer = 1 - CurrentPlayer;
        StartLife(consumeShip: true);
    }

    /// <summary>Queue initials entry for every player whose final score qualifies (player one first).</summary>
    private void QueueInitials()
    {
        _initialsQueue.Clear();
        var finals = new List<PendingInitials>();
        for (int p = 0; p < PlayerCount; p++)
        {
            if (p == CurrentPlayer) finals.Add(new PendingInitials(p, Score, Wave));
            else if (_slots[p] is { } st) finals.Add(new PendingInitials(p, st.Score, st.Wave));
        }
        // Check qualification against the table as it will be after earlier players are inserted.
        foreach (var f in finals.OrderBy(f => f.Player)) if (HighScores.Qualifies(f.Score)) _initialsQueue.Enqueue(f);
    }

    public int ScoreOf(int player) => player == CurrentPlayer ? Score : _slots[player]?.Score ?? 0;
    public int LivesOf(int player) => player == CurrentPlayer ? Lives : _slots[player]?.Lives ?? 0;
    public int BombsOf(int player) => player == CurrentPlayer ? SmartBombs : _slots[player]?.SmartBombs ?? 0;
}
