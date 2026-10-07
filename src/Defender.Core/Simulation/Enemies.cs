using Defender.Core.Audio;

namespace Defender.Core.Simulation;

public sealed partial class GameSession
{
    private const int AppearFrames = 32;

    public static Sprite SpriteOf(Enemy e) => e.Kind switch
    {
        EnemyKind.Lander => Sprites.Lander[e.Anim % 3],
        EnemyKind.Mutant => Sprites.Mutant,
        EnemyKind.Bomber => Sprites.Bomber,
        EnemyKind.Pod => Sprites.Pod,
        EnemyKind.Swarmer => Sprites.Swarmer,
        _ => Sprites.Baiter,
    };

    private int PlayerWorldX => Player.WorldX(CameraX);

    /// <summary>Shortest signed pixel distance from <paramref name="fromX"/> to the player.</summary>
    private int DxToPlayerPx(int fromX) => (short)(ushort)((PlayerWorldX - fromX) & Arcade.WorldMask) / Arcade.UnitsPerPixel;

    // ----- spawning --------------------------------------------------------------------------------

    private Enemy NewEnemy(EnemyKind kind, int x, int yPx, bool appear = true)
    {
        var e = new Enemy { Id = _nextEnemyId++, Kind = kind, X = WrapX(x), Y = yPx << 8, Appear = appear ? AppearFrames : 0, Nap = 1 };
        Enemies.Add(e);
        if (appear && OnScreen(e.X)) _sounds.Add(SoundId.LanderMaterialize);
        return e;
    }

    private void SpawnLander()
    {
        if (!PlanetActive || HumanoidsAlive == 0)
        {
            SpawnMutant(Rng.Next(Arcade.WorldUnits), Arcade.YMin + 2); // LANDST → mutant start when no humanoids
            return;
        }
        var e = NewEnemy(EnemyKind.Lander, Rng.Next(Arcade.WorldUnits), Arcade.YMin + 2);
        e.Vx = Rng.RMax(Params[WaveVar.LanderXV]) * Rng.Sign();
        e.Vy = Params[WaveVar.LanderYV];
        e.ShotTimer = Rng.RMax(Params[WaveVar.LanderShotTimer]);
        e.Phase = LanderPhase.Cruise;
        e.Target = PickTarget();
        e.Anim = Rng.Next(3);
    }

    private Enemy SpawnMutant(int x, int yPx, bool appear = true)
    {
        var e = NewEnemy(EnemyKind.Mutant, x, yPx, appear);
        e.ShotTimer = Rng.RMax(Params[WaveVar.MutantShotTimer]);
        return e;
    }

    /// <summary>Random X at least 300 px away from the screen-left region (defb6.src:596-605).</summary>
    private int RandomXAwayFromPlayer()
    {
        for (int i = 0; i < 32; i++)
        {
            int x = Rng.Next(Arcade.WorldUnits);
            int d = Math.Abs((short)(ushort)((x - CameraX) & Arcade.WorldMask));
            if (d > 300 * Arcade.UnitsPerPixel) return x;
        }
        return WrapX(CameraX + Arcade.WorldUnits / 2);
    }

    /// <summary>Pods, bombers and any reserved mutants/swarmers enter when a life (or wave) starts.</summary>
    private void SpawnLifeStartEnemies()
    {
        for (; PodReserve > 0; PodReserve--)
        {
            // X high byte $10-$4F, random drift (defb6.src:87-114).
            var e = NewEnemy(EnemyKind.Pod, (Rng.Range(0x10, 0x4F) << 8) | Rng.NextByte(), Rng.Range(Arcade.YMin + 10, Arcade.YMax - 30));
            e.Vx = Rng.Range(-32, 31);
            e.Vy = Rng.Range(0x20, 0x7F) * Rng.Sign();
        }
        while (BomberReserve > 0)
        {
            int n = Math.Min(3, BomberReserve);
            int squad = ++_bomberSquadCounter;
            int dir = squad % 2 == 0 ? 1 : -1;
            int baseX = PlayerWorldX + 0x8000 + Rng.Next(0x1000);
            for (int i = 0; i < n; i++)
            {
                var b = NewEnemy(EnemyKind.Bomber, baseX + i * 0x0300, 80 + i * 6);
                b.Squad = squad;
                b.Vx = dir * Params[WaveVar.BomberXV];
                b.CruiseY = Rng.Range(64, 104);
            }
            BomberReserve -= n;
        }
        for (; MutantReserve > 0; MutantReserve--) SpawnMutant(RandomXAwayFromPlayer(), Rng.Range(Arcade.YMin + 10, 150));
        for (; SwarmerReserve > 0; SwarmerReserve--) SpawnSwarmer(RandomXAwayFromPlayer(), Rng.Range(Arcade.YMin + 10, 150), appear: true);
    }

    private void SpawnSwarmer(int x, int yPx, bool appear)
    {
        var e = NewEnemy(EnemyKind.Swarmer, x, yPx, appear);
        e.Dir = DxToPlayerPx(e.X) >= 0 ? 1 : -1;
        e.Accel = Rng.NextByte() & Params[WaveVar.SwarmerYAccelMask];
        e.ShotTimer = Rng.RMax(Params[WaveVar.SwarmerShotTimer]);
        e.Vy = Rng.Range(-0x80, 0x80);
    }

    private void SpawnBaiter()
    {
        // World X = BGL + random(0..31)*256 → on the visible screen; random Y (defb6.src:5-24).
        var e = NewEnemy(EnemyKind.Baiter, CameraX + Rng.Next(32) * 256, Rng.Range(Arcade.YMin + 10, Arcade.YMax - 20));
        e.ShotTimer = Rng.RMax(Params[WaveVar.BaiterShotTimer]);
        e.Anim = 0;
        e.Nap = 1;
        _sounds.Add(SoundId.BaiterAppear);
    }

    // ----- game executive (every 15 frames) --------------------------------------------------------

    public int EnemiesRemainingForWave =>
        LanderReserve + MutantReserve + BomberReserve + PodReserve + SwarmerReserve +
        Enemies.Count(e => !e.Dead && e.Kind != EnemyKind.Baiter);

    private void GameExecutive()
    {
        int landersAlive = Enemies.Count(e => !e.Dead && e.Kind == EnemyKind.Lander);
        if (_waveTimer > 0) _waveTimer--;
        if ((_firstGexec || _waveTimer == 0 || landersAlive == 0) && LanderReserve > 0 && landersAlive < Arcade.MaxLandersAlive)
        {
            int n = Math.Min(Params[WaveVar.WaveSize], LanderReserve);
            for (int i = 0; i < n; i++) SpawnLander();
            LanderReserve -= n;
            _waveTimer = Params[WaveVar.WaveTime];
        }
        _firstGexec = false;

        int remaining = EnemiesRemainingForWave;
        if (remaining == 0 && Player.Alive && !Player.InHyperspace)
        {
            BeginWaveComplete();
            return;
        }

        // Baiter countdown, shortened when few enemies remain (defa7.src:1667-1691).
        int ufo = Params[WaveVar.BaiterTime];
        if (remaining <= 3) _baiterTimer = Math.Min(_baiterTimer, ufo / 4 + 1);
        else if (remaining <= 8) _baiterTimer = Math.Min(_baiterTimer, ufo / 2 + 1);
        if (--_baiterTimer <= 0)
        {
            if (Enemies.Count(e => !e.Dead && e.Kind == EnemyKind.Baiter) < Arcade.MaxBaiters) SpawnBaiter();
            _baiterTimer = remaining >= 4 ? ufo : Rng.Next(ufo / 4 + 1) + 1;
        }

        // Intra-wave escalation every 40 executive ticks (10 s).
        if (++_intraCounter >= Arcade.IntraWaveGexecTicks)
        {
            _intraCounter = 0;
            Params.ApplyIntra(_table);
        }
    }

    // ----- AI ---------------------------------------------------------------------------------------

    private void UpdateEnemies()
    {
        // Bomber squads: one random member per squad gets attention each frame (defb6.src:1027-1116).
        foreach (var squad in Enemies.Where(e => !e.Dead && e.Kind == EnemyKind.Bomber && e.Appear == 0).GroupBy(e => e.Squad).ToList())
        {
            var members = squad.ToList();
            BomberThink(members[Rng.Next(members.Count)]);
        }

        for (int i = 0; i < Enemies.Count; i++)
        {
            var e = Enemies[i];
            if (e.Dead) continue;
            if (e.Appear > 0) { e.Appear--; continue; }
            if (--e.Nap <= 0)
            {
                switch (e.Kind)
                {
                    case EnemyKind.Lander: LanderThink(e); break;
                    case EnemyKind.Mutant: MutantThink(e); e.Nap = 3; break;
                    case EnemyKind.Swarmer: SwarmerThink(e); e.Nap = 3; break;
                    case EnemyKind.Baiter: BaiterThink(e); e.Nap = 6; break;
                    default: e.Nap = 1000; break; // pods drift; bombers handled per squad
                }
            }
            if (e.Dead) continue;
            // VELO: integrate every frame, wrap X around the planet and Y between YMIN and YMAX.
            e.X = WrapX(e.X + e.Vx);
            if (e.Kind == EnemyKind.Lander && e.Phase is LanderPhase.Lift or LanderPhase.Absorb)
                e.Y = Math.Max(e.Y + e.Vy, (Arcade.YMin + 8) << 8);
            else
                e.Y = WrapY(e.Y + e.Vy);
        }
    }

    private int PickTarget()
    {
        var candidates = Enumerable.Range(0, Humanoids.Length).Where(i => Humanoids[i].State == HumanoidState.Walking).ToList();
        return candidates.Count == 0 ? -1 : candidates[Rng.Next(candidates.Count)];
    }

    private void LanderThink(Enemy e)
    {
        switch (e.Phase)
        {
            case LanderPhase.Cruise:
            {
                e.Nap = 6;
                if (e.Target < 0 || Humanoids[e.Target].State != HumanoidState.Walking)
                {
                    if (HumanoidsAlive == 0 || !PlanetActive) { Mutate(e); return; }
                    e.Target = PickTarget();
                }
                // Hover ~50 px above the terrain (defb6.src:736-776).
                int hover = Terrain.HeightAtUnits(e.X) - 50;
                int y = e.PixelY;
                int yv = Params[WaveVar.LanderYV];
                e.Vy = y < hover ? yv : y <= hover + 20 ? 0 : -yv;
                e.Anim = (e.Anim + 1) % 3;
                TryShoot(e, WaveVar.LanderShotTimer);
                if (e.Target >= 0 && (e.X & 0xFC00) == (Humanoids[e.Target].X & 0xFC00))
                {
                    e.Phase = LanderPhase.Descend;
                    e.Vx = 0;
                    e.Nap = 1;
                }
                break;
            }
            case LanderPhase.Descend:
            {
                e.Nap = 1;
                var h = e.Target >= 0 ? Humanoids[e.Target] : null;
                if (h is null || h.State != HumanoidState.Walking)
                {
                    e.Phase = LanderPhase.Cruise;
                    e.Vx = Rng.RMax(Params[WaveVar.LanderXV]) * Rng.Sign();
                    e.Target = -1;
                    return;
                }
                int dx = (short)(ushort)((h.X - e.X) & Arcade.WorldMask);
                if (Math.Abs(dx) > 2 * Arcade.UnitsPerPixel) e.X = WrapX(e.X + Math.Sign(dx) * Arcade.UnitsPerPixel);
                int goalY = h.PixelY - 12;
                e.Vy = e.PixelY < goalY ? Params[WaveVar.LanderYV] : 0;
                if (e.PixelY >= goalY && Math.Abs(dx) <= 2 * Arcade.UnitsPerPixel)
                {
                    e.Phase = LanderPhase.Lift;
                    e.Vy = -Params[WaveVar.LanderYV];
                    h.State = HumanoidState.Grabbed;
                    h.Carrier = e.Id;
                    _sounds.Add(SoundId.Abduction);
                }
                break;
            }
            case LanderPhase.Lift:
                e.Nap = 1;
                if (!CarryingTarget(e)) { ResumeCruise(e); return; }
                if (e.PixelY <= Arcade.YMin + 8) { e.Phase = LanderPhase.Absorb; e.Vy = 0; }
                break;
            case LanderPhase.Absorb:
            {
                e.Nap = 1;
                var h = e.Target >= 0 ? Humanoids[e.Target] : null;
                if (h is null || !CarryingTarget(e)) { ResumeCruise(e); return; }
                h.Y -= 1 << 8;   // pulled up into the lander 1 px/frame
                if (h.PixelY <= e.PixelY + 2)
                {
                    h.State = HumanoidState.Dead;
                    h.Carrier = -1;
                    OnHumanoidLost();
                    Mutate(e);
                    _sounds.Add(SoundId.MutantCreated);
                }
                break;
            }
        }
    }

    private bool CarryingTarget(Enemy e) =>
        e.Target >= 0 && Humanoids[e.Target].State == HumanoidState.Grabbed && Humanoids[e.Target].Carrier == e.Id;

    /// <summary>Lander lost its humanoid (e.g. the humanoid was shot): back to cruising.</summary>
    private void ResumeCruise(Enemy e)
    {
        e.Phase = LanderPhase.Cruise;
        e.Target = -1;
        e.Vx = Rng.RMax(Params[WaveVar.LanderXV]) * Rng.Sign();
        e.Nap = 6;
    }

    private void Mutate(Enemy e)
    {
        e.Kind = EnemyKind.Mutant;
        e.Phase = LanderPhase.Cruise;
        e.Target = -1;
        e.Vx = 0; e.Vy = 0;
        e.Nap = 1;
        e.ShotTimer = Rng.RMax(Params[WaveVar.MutantShotTimer]);
    }

    private void MutantThink(Enemy e)
    {
        int dx = DxToPlayerPx(e.X);
        e.Vx = Math.Sign(dx == 0 ? 1 : dx) * Params[WaveVar.MutantXV];
        int dy = Player.PixelY - e.PixelY;
        int syv = Params[WaveVar.MutantYV];
        int rel = -dx; // mutant relative to player, px
        if (rel >= -12 && rel <= 44)
        {
            e.Vy = Math.Sign(dy) * syv;              // close in X: seek the player's altitude
            TryShoot(e, WaveVar.MutantShotTimer);
        }
        else e.Vy = Math.Abs(dy) < 8 ? -Math.Sign(dy == 0 ? 1 : dy) * syv : 0; // far: dodge the laser line
        int hop = Params[WaveVar.MutantRandomY];
        if (hop > 0) e.Y = WrapY(e.Y + (Rng.Range(-hop, hop) << 8));
    }

    private void SwarmerThink(Enemy e)
    {
        int dx = DxToPlayerPx(e.X);
        if (Math.Sign(dx) != e.Dir && Math.Abs(dx) > 150) e.Dir = Math.Sign(dx);
        if (e.Dir == 0) e.Dir = 1;
        e.Vx = e.Dir * Params[WaveVar.SwarmerXV];
        int dy = Player.Y - e.Y;
        e.Vy += Math.Sign(dy) * e.Accel;
        e.Vy -= e.Vy / 64;
        e.Vy += Rng.Range(-16, 16);
        e.Vy = Math.Clamp(e.Vy, -0x200, 0x200);
        if (Math.Sign(dx) == e.Dir && OnScreen(e.X) && --e.ShotTimer <= 0)
        {
            e.ShotTimer = Rng.RMax(Params[WaveVar.SwarmerShotTimer]);
            FireShell(e.X, e.Y, e.Vx * 8 * 16, dy / 32);
        }
    }

    private void BaiterThink(Enemy e)
    {
        e.Anim = (e.Anim + 1) % 3;
        if (e.Anim == 0 || e.Dir == 0)
        {
            if (e.Dir == 0 || Rng.NextByte() > Params[WaveVar.BaiterSeek])
            {
                e.Dir = 1;
                int dx = DxToPlayerPx(e.X);
                e.Vx = Player.V16 + (Math.Abs(dx) > 20 ? Math.Sign(dx) * 0x40 : 0);
                int dy = Player.PixelY - e.PixelY;
                e.Vy = (Player.Vy + (Math.Abs(dy) > 10 ? Math.Sign(dy) * 0x100 : 0)) / 2;
            }
        }
        TryShoot(e, WaveVar.BaiterShotTimer);
    }

    private void BomberThink(Enemy b)
    {
        b.Vy += Rng.Sign() * 0x10;
        b.Vy -= b.Vy / 32;
        int y = b.PixelY;
        if (OnScreen(b.X))
        {
            int dy = Player.PixelY - y;
            if (Math.Abs(dy) < 16) b.Vy -= Math.Sign(dy == 0 ? 1 : dy) * 0x10;
            else if (Math.Abs(dy) > 32) b.Vy += Math.Sign(dy) * 0x10;
        }
        else
        {
            b.CruiseY = Math.Clamp(b.CruiseY + Rng.Range(-1, 1), 64, 104);
            b.Vy += Math.Sign(b.CruiseY - y) * 0x10;
        }
        b.Vy = Math.Clamp(b.Vy, -0x100, 0x100);
        // Lay a mine with probability 1/8 (defb6.src:1130-1149).
        if ((Rng.NextByte() & 7) == 0 && Shells.Count < Arcade.MaxShells && Shells.Count(s => s.Mine) < Arcade.MaxMines)
        {
            Shells.Add(new Shell { X16 = b.X << 4, Y = b.Y + (3 << 8), Mine = true, Life = ((Rng.NextByte() & 31) + 1) * 8 });
        }
    }

    // ----- shooting -----------------------------------------------------------------------------------

    private void TryShoot(Enemy e, WaveVar timer)
    {
        if (--e.ShotTimer > 0) return;
        e.ShotTimer = Rng.RMax(Params[timer]);
        if (!OnScreen(e.X) || e.PixelY <= Arcade.YMin || !Player.Alive || Player.InHyperspace) return;
        // Aimed shot (SHOOT, defb6.src:534-570): reaches a jittered aim point in ~64 frames.
        int dxPx = DxToPlayerPx(e.X) + Rng.Range(-16, 15);
        int dyPx = Player.PixelY + Rng.Range(-16, 15) - e.PixelY;
        int vx16 = dxPx * Arcade.UnitsPerPixel * 16 / Arcade.AimFrames;
        if (Rng.NextByte() > 120) vx16 += Player.V16 * 16;   // lead / world compensation in ~53% of shots
        FireShell(e.X, e.Y, vx16, (dyPx << 8) / Arcade.AimFrames);
    }

    private void FireShell(int x, int y, int vx16, int vy)
    {
        if (Shells.Count >= Arcade.MaxShells || !OnScreen(x) || (y >> 8) <= Arcade.YMin) return;
        Shells.Add(new Shell { X16 = x << 4, Y = y + (2 << 8), Vx16 = vx16, Vy = vy, Life = Arcade.ShellLifetimeFrames });
        _sounds.Add(SoundId.EnemyShot);
    }

    private void UpdateShells()
    {
        foreach (var s in Shells)
        {
            if (s.Dead) continue;
            if (--s.Life <= 0) { s.Dead = true; continue; }
            if (s.Mine) continue; // mines are world-fixed
            s.X16 = (s.X16 + s.Vx16) & ((Arcade.WorldMask << 4) | 0xF);
            s.Y += s.Vy;
            int py = s.Y >> 8;
            if (!OnScreen(s.X) || py <= Arcade.YMin || py >= Arcade.YMax) s.Dead = true;
        }
    }

    // ----- kills ---------------------------------------------------------------------------------------

    private void KillEnemy(Enemy e, bool scored)
    {
        if (e.Dead) return;
        e.Dead = true;
        int points = e.Kind switch
        {
            EnemyKind.Lander => 150, EnemyKind.Mutant => 150, EnemyKind.Swarmer => 150,
            EnemyKind.Baiter => 200, EnemyKind.Bomber => 250, EnemyKind.Pod => 1000, _ => 0,
        };
        if (scored) AddScore(points);
        SpawnExplosion(e.X, e.Y, e.Kind == EnemyKind.Pod ? 24 : 12, e.Kind switch
        {
            EnemyKind.Lander => Pal.Green, EnemyKind.Bomber => Pal.BomberD, EnemyKind.Pod => Pal.Purple,
            EnemyKind.Swarmer => Pal.Red, EnemyKind.Baiter => Pal.Green, _ => Pal.CycleC,
        });
        _sounds.Add(e.Kind == EnemyKind.Pod ? SoundId.PodExplode : SoundId.EnemyExplode);

        if (e.Kind == EnemyKind.Lander && e.Target >= 0)
        {
            var h = Humanoids[e.Target];
            if (h.State == HumanoidState.Grabbed && h.Carrier == e.Id)
            {
                h.State = HumanoidState.Falling;   // released with Y velocity 0; scream
                h.Carrier = -1;
                h.Vy = 0;
                h.FallFrames = 0;
                _sounds.Add(SoundId.HumanoidFalling);
            }
        }
        if (e.Kind == EnemyKind.Pod)
        {
            int alive = Enemies.Count(x => !x.Dead && x.Kind == EnemyKind.Swarmer);
            int n = Math.Min(Rng.RMax(6), Arcade.MaxSwarmers - alive);  // 1..7 swarmers (INF)
            for (int i = 0; i < n; i++) SpawnSwarmer(e.X + Rng.Range(-64, 64), e.PixelY + Rng.Range(-4, 4), appear: false);
        }
    }

    private void SpawnExplosion(int x, int y, int pieces, byte color, int speed = 2)
    {
        for (int i = 0; i < pieces; i++)
        {
            double a = i * Math.Tau / pieces;
            Particles.Add(new Particle
            {
                X = x + 4 * Arcade.UnitsPerPixel, Y = y + (4 << 8),
                Vx = (int)(Math.Cos(a) * speed * Arcade.UnitsPerPixel), Vy = (int)(Math.Sin(a) * speed * 256),
                Life = 30 + Rng.Next(10), Color = color,
            });
        }
    }

    private void UpdateParticles()
    {
        foreach (var p in Particles) { p.X = WrapX(p.X + p.Vx); p.Y += p.Vy; p.Life--; }
        Particles.RemoveAll(p => p.Life <= 0);
        foreach (var p in Popups) p.Life--;
        Popups.RemoveAll(p => p.Life <= 0);
    }
}
