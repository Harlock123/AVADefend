using Defender.Core.Input;
using Defender.Core.Simulation;

namespace Defender.Tests;

public class TwoPlayerTests
{
    private static GameSession Start2(uint seed = 21)
    {
        var s = new GameSession(rng: new XorShiftRandom(seed));
        s.Step(new PlayerInput { Start2Pressed = true });
        return s;
    }

    /// <summary>Kill the flying player by ramming a mutant, then let the death sequence finish.</summary>
    private static void Die(GameSession s)
    {
        s.RunUntil(() => s.State == SessionState.Playing, 400);
        var e = s.TestSpawn(EnemyKind.Mutant, s.Player.WorldX(s.CameraX), s.Player.PixelY);
        e.Vx = e.Vy = 0; e.Nap = 1000;
        s.Step(default);
        Assert.Equal(SessionState.Dying, s.State);
        s.RunUntil(() => s.State != SessionState.Dying, 400);
    }

    [Fact]
    public void TwoPlayerStart_PlayerOneFliesFirst_PlayerTwoWaitsWithFullShips()
    {
        var s = Start2();
        Assert.Equal((2, 0), (s.PlayerCount, s.CurrentPlayer));
        Assert.Equal(SessionState.LifeStart, s.State);
        Assert.Equal(2, s.Lives);
        Assert.Equal(3, s.WaitingPlayer!.Lives);
        Assert.Equal(3, s.WaitingPlayer.SmartBombs);
        Assert.Equal(10, s.WaitingPlayer.Humanoids.Count(h => h.Alive));
    }

    [Fact]
    public void TurnsAlternateOnDeath_AndEachPlayerKeepsOwnScoreWaveAndHumanoids()
    {
        var s = Start2();
        s.RunUntil(() => s.State == SessionState.Playing, 400);
        s.TestAddScore(700);
        for (int i = 0; i < 4; i++) s.TestKillHumanoid(s.Humanoids[i]);
        Die(s);                                    // ram also scores 150 for player one
        Assert.Equal(1, s.CurrentPlayer);
        Assert.Equal(0, s.Score);
        Assert.Equal(10, s.HumanoidsAlive);         // player two's own planet
        Assert.Equal(2, s.Lives);                   // player two launched a ship
        Assert.Equal(850, s.ScoreOf(0));
        Die(s);
        Assert.Equal(0, s.CurrentPlayer);
        Assert.Equal(850, s.Score);
        Assert.Equal(6, s.HumanoidsAlive);          // player one's planet as they left it
        Assert.Equal(1, s.Lives);
    }

    [Fact]
    public void PlayerWhoRunsOut_DropsOut_OtherContinues_ThenGameOver_ThenBothEnterInitials()
    {
        var s = Start2();
        s.RunUntil(() => s.State == SessionState.Playing, 400);
        s.TestAddScore(2000);
        // P1, P2, P1, P2, P1 (P1 out), P2, P2? — each has 3 ships: 6 deaths end the game.
        var order = new List<int>();
        while (s.State is not (SessionState.GameOver or SessionState.EnterInitials or SessionState.Attract))
        {
            order.Add(s.CurrentPlayer);
            Die(s);
        }
        Assert.Equal([0, 1, 0, 1, 0, 1], order);
        Assert.Equal(SessionState.GameOver, s.State);
        s.RunUntil(() => s.State == SessionState.EnterInitials, 400);
        Assert.Equal(0, s.InitialsPlayer);
        foreach (var c in "ONE") s.TypeInitial(c);
        Assert.Equal(SessionState.EnterInitials, s.State);
        Assert.Equal(1, s.InitialsPlayer);
        foreach (var c in "TWO") s.TypeInitial(c);
        Assert.Equal(SessionState.Attract, s.State);
        Assert.Equal(["ONE", "TWO"], s.HighScores.AllTime.Entries.Select(e => e.Initials).ToArray());
    }

    [Fact]
    public void OnePlayerGame_IsUnchanged()
    {
        var s = new GameSession(rng: new XorShiftRandom(3));
        s.Step(new PlayerInput { StartPressed = true });
        Assert.Equal(1, s.PlayerCount);
        Assert.Null(s.WaitingPlayer);
        for (int i = 0; i < 3; i++) Die(s);
        Assert.Equal(SessionState.GameOver, s.State);
    }

    [Fact]
    public void TwoPlayerSuspend_RoundTripsWaitingPlayer()
    {
        var s = Start2();
        s.RunUntil(() => s.State == SessionState.Playing, 400);
        s.TestAddScore(1234);
        Die(s);
        s.RunUntil(() => s.State == SessionState.Playing, 400);
        var r = GameSession.Restore(s.CaptureSuspend(), GamePolicy.Modern);
        Assert.Equal((2, 1), (r.PlayerCount, r.CurrentPlayer));
        Assert.Equal(1234 + 150, r.ScoreOf(0));
        Assert.Equal(s.WaitingPlayer!.Lives, r.WaitingPlayer!.Lives);
    }
}
