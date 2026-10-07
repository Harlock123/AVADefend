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
        // APST (samexap7.src): the appear effect only runs on screen (OX16 − BGL ≤ $2600); off-screen spawns are
        // immediate. While appearing the object is flagged (OTYP bit 1), so smart bombs skip it.
        var e = new Enemy { Id = _nextEnemyId++, Kind = kind, X = WrapX(x), Y = yPx << 8, Nap = 1 };
        bool visible = ((e.X - CameraX) & Arcade.WorldMask) <= 0x2600;
        if (appear && visible) { e.Appear = AppearFrames; _sounds.Add(SoundId.LanderMaterialize); }
        Enemies.Add(e);
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

    /// <summary>Pods, bombers and any reserved mutants/swarmers enter when a life (or wave) starts (PLRES).</summary>
    private void SpawnLifeStartEnemies()
    {
        for (; PodReserve > 0; PodReserve--)
        {
            // PRBST (defb6.src:87-114): X high byte $10-$4F, Y = rnd/2 + YMIN, vx -32..31, |vy| 32..64.
            int h = Rng.NextByte(), l = Rng.NextByte(), sd = Rng.NextByte();
            var e = NewEnemy(EnemyKind.Pod, (((h & 0x3F) + 0x10) << 8) | l, (l >> 1) + Arcade.YMin);
            e.Vx = (sd & 0x3F) - 0x20;
            int vy = (Rng.NextByte() & 0x7F) - 0x40;
            e.Vy = vy >= 0 ? vy | 0x20 : vy & ~0x20;
        }
        while (BomberReserve > 0)
        {
            int n = Math.Min(3, BomberReserve);
            SpawnBomberSquad(n);
            BomberReserve -= n;
        }
        // RSW0 (defa7.src:1590-1617): swarmers return in clusters of up to 6, 1024-1535 px ahead of the screen.
        while (SwarmerReserve > 0)
        {
            int y = (Rng.NextByte() >> 1) + Arcade.YMin;
            int x = CameraX + ((((Rng.NextByte() & 0x3F) + 0x80) << 8) | Rng.NextByte());
            int n = Math.Min(6, SwarmerReserve);
            for (int i = 0; i < n; i++) SpawnSwarmer(x, y);
            SwarmerReserve -= n;
        }
        for (; MutantReserve > 0; MutantReserve--) SpawnMutant(RandomXAwayFromPlayer(), Rng.Range(Arcade.YMin + 10, 150));
    }

    /// <summary>TIEST (defb6.src:977-1021): members $180 units apart at player X + $8000, Y = cruise = $50; direction alternates.</summary>
    private void SpawnBomberSquad(int n)
    {
        int squad = ++_bomberSquadCounter;
        _bomberFlip = !_bomberFlip;
        int vx = (_bomberFlip ? 1 : -1) * Params[WaveVar.BomberXV];
        for (int i = n; i >= 1; i--)
        {
            var b = NewEnemy(EnemyKind.Bomber, PlayerWorldX + 0x8000 + i * 0x180, 0x50, appear: false);
            b.Squad = squad;
            b.Dir = n - i;          // slot index within the squad (PD..PD6)
            b.Vx = vx;
            b.CruiseY = 0x50;
        }
    }

    /// <summary>MMSW/RANDV (defb6.src:128-172): at the given point, vy = signed rnd × 2, vx -32..31, random first nap.</summary>
    private void SpawnSwarmer(int x, int yPx)
    {
        if (Enemies.Count(e => !e.Dead && e.Kind == EnemyKind.Swarmer) >= Arcade.MaxSwarmers) return;
        var e = NewEnemy(EnemyKind.Swarmer, x, yPx, appear: false);
        e.Vy = (sbyte)Rng.NextByte() * 2;
        e.Vx = (Rng.NextByte() & 0x3F) - 0x20;
        e.Accel = Rng.NextByte() & Params[WaveVar.SwarmerYAccelMask];
        e.Nap = (Rng.NextByte() & 0x1F) + 1;
        e.ShotTimer = Rng.RMax(Params[WaveVar.SwarmerShotTimer]);
        e.Dir = DxToPlayerPx(e.X) >= 0 ? 1 : -1;
    }

    private void SpawnBaiter()
    {
        // World X = BGL + random(0..31)*256 → on the visible screen; random Y (defb6.src:5-24). First shot timer 8.
        var e = NewEnemy(EnemyKind.Baiter, CameraX + Rng.Next(32) * 256, Rng.Range(Arcade.YMin + 10, Arcade.YMax - 20));
        e.ShotTimer = 8;
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
        // GEX2 (defa7.src:1692-1709) runs on the first tick, when the squad timer expires, or when no landers
        // are alive; it reloads the timer every time it runs, whether or not a squad could launch.
        if (_firstGexec || _waveTimer == 0 || landersAlive == 0)
        {
            if (LanderReserve > 0 && landersAlive < Arcade.MaxLandersAlive)
            {
                int n = Math.Min(Params[WaveVar.WaveSize], LanderReserve);
                for (int i = 0; i < n; i++) SpawnLander();
                LanderReserve -= n;
            }
            _waveTimer = Params[WaveVar.WaveTime];
        }
        _firstGexec = false;

        int remaining = EnemiesRemainingForWave;
        if (remaining == 0 && Player.Alive && !TestHoldWave)   // may end during hyperspace too
        {
            BeginWaveComplete();
            return;
        }

        // Baiter countdown, shortened when few enemies remain (defa7.src:1667-1691).
        int ufo = Params[WaveVar.BaiterTime];
        if (remaining <= 3) _baiterTimer = Math.Min(_baiterTimer, ufo / 4 + 1);
        else if (remaining <= 8) _baiterTimer = Math.Min(_baiterTimer, ufo / 2 + 1);
        if (--_baiterTimer <= 0 && !TestHoldWave)
        {
            if (Enemies.Count(e => !e.Dead && e.Kind == EnemyKind.Baiter) < Arcade.MaxBaiters) SpawnBaiter();
            _baiterTimer = remaining >= 4 ? ufo : Rng.Next(ufo / 4 + 1) + 1;
        }

        // Intra-wave escalation every 40 executive ticks (10 s); the count restarts with each life.
        if (++_intraCounter >= Arcade.IntraWaveGexecTicks)
        {
            _intraCounter = 0;
            Params.ApplyIntra(_table);
        }
    }

    // ----- AI ---------------------------------------------------------------------------------------

    private void UpdateEnemies()
    {
        // Bomber squads: each frame one of the squad's 4 slots is picked; an empty slot means no update (TIE).
        foreach (var squad in Enemies.Where(e => !e.Dead && e.Kind == EnemyKind.Bomber).GroupBy(e => e.Squad).ToList())
        {
            int slot = (Rng.NextByte() & 6) >> 1;
            var member = squad.FirstOrDefault(m => m.Dir == slot);
            if (member is not null) BomberThink(member);
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
                TryShoot(e, WaveVar.LanderShotTimer);   // LANDG calls LSHOT every frame (defb6.src:775)
                var h = e.Target >= 0 ? Humanoids[e.Target] : null;
                if (h is null || h.State != HumanoidState.Walking)
                {
                    // Retarget without restoring X velocity: the lander now hovers in place (LNDSAA → LANDSA).
                    e.Phase = LanderPhase.Cruise;
                    e.Target = -1;
                    e.Nap = 6;
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
                // LANDF: climbs without checking the humanoid; shoots every 4 frames (defb6.src:795-802).
                e.Nap = 1;
                if (Frame % 4 == 0) TryShoot(e, WaveVar.LanderShotTimer);
                if (e.PixelY <= Arcade.YMin + 8)
                {
                    if (!CarryingTarget(e)) { ReturnToReserve(e); return; }
                    e.Phase = LanderPhase.Absorb;
                    e.Vy = 0;
                }
                break;
            case LanderPhase.Absorb:
            {
                e.Nap = 1;
                if (!CarryingTarget(e)) { ReturnToReserve(e); return; }
                var h = Humanoids[e.Target];
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

    /// <summary>LNDFXA (defb6.src:805-812): a lander that reaches the top without its humanoid is removed unscored and re-queued.</summary>
    private void ReturnToReserve(Enemy e)
    {
        e.Dead = true;
        LanderReserve++;
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

    /// <summary>SCZ0 (defb6.src:846-901).</summary>
    private void MutantThink(Enemy e)
    {
        int dxUnits = (short)(ushort)((PlayerWorldX - e.X) & Arcade.WorldMask);
        e.Vx = (dxUnits >= 0 ? 1 : -1) * Params[WaveVar.MutantXV];
        int dy = Player.PixelY - e.PixelY;
        int syv = Params[WaveVar.MutantYV];
        // Seek when (player − mutant) is within −380..+1412 units (about −12..+44 px).
        if (dxUnits >= -380 && dxUnits <= 1412)
        {
            e.Vy = dy >= 0 ? syv : -syv;
            if (!OnScreen(e.X)) return;              // off screen: no hop, no shot this tick
        }
        else if (dy > 0) e.Vy = dy <= 8 ? -syv : 0;  // avoid the player's line
        else e.Vy = dy > -8 ? syv : 0;
        // Hop exactly ±SZRY every tick, then the shot timer (SHOOT itself refuses off-screen shots).
        int hop = Params[WaveVar.MutantRandomY];
        int ny = e.PixelY + ((Rng.NextByte() & 0x80) != 0 ? hop : -hop);
        if (ny < Arcade.YMin) ny = Arcade.YMax;
        e.Y = (ny << 8) | (e.Y & 0xFF);
        TryShoot(e, WaveVar.MutantShotTimer);
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

    /// <summary>UFO (defb6.src:1-83): re-seeks on each 18-frame cycle when rnd > UFOSK; a close axis keeps its velocity.</summary>
    private void BaiterThink(Enemy e)
    {
        e.Anim = (e.Anim + 1) % 3;
        if ((e.Anim == 0 || e.Dir == 0) && (e.Dir == 0 || Rng.NextByte() > Params[WaveVar.BaiterSeek]))
        {
            e.Dir = 1;
            int dx = DxToPlayerPx(e.X);
            if (Math.Abs(dx) > 20) e.Vx = Player.V16 + Math.Sign(dx) * 0x40;
            int dy = Player.PixelY - e.PixelY;
            if (Math.Abs(dy) > 10) e.Vy = (Player.Vy + Math.Sign(dy) * 0x100) / 2;
        }
        TryShoot(e, WaveVar.BaiterShotTimer);
    }

    /// <summary>TIE (defb6.src:1027-1116).</summary>
    private void BomberThink(Enemy b)
    {
        b.Vy += (Rng.NextByte() & 0x3F) - 0x20;   // random nudge −32..+31
        b.Vy -= b.Vy / 32;                        // damping
        int y = b.PixelY;
        if (OnScreen(b.X))
        {
            int d = y - Player.PixelY;            // positive: bomber below the player
            if (d >= 0) { if (d >= 0x20) b.Vy -= 0x10; else if (d <= 0x10) b.Vy += 0x10; }
            else { if (d <= -0x20) b.Vy += 0x10; else if (d >= -0x10) b.Vy -= 0x10; }
            // Mines only from the on-screen branch, 1 in 8 updates, fewer than 10 at a time (BOMBST).
            if ((Rng.NextByte() & 7) == 0 && Shells.Count < Arcade.MaxShells && Shells.Count(s => s.Mine) < Arcade.MaxMines)
                Shells.Add(new Shell { X16 = b.X << 4, Y = b.Y + (3 << 8), Mine = true, Life = ((Rng.NextByte() & 0x1F) + 1) * 8 });
        }
        else
        {
            int sd = Rng.NextByte();
            if (sd <= 0x40) b.CruiseY = Math.Clamp(b.CruiseY + (sd & 3) - 2, 0x40, 0x67);
            int diff = b.CruiseY - y;
            // As written in the source the correction pushes *away* from the cruise altitude (INF: an original quirk).
            if (Math.Abs(diff) > 0x10) b.Vy += diff < 0 ? 0x10 : -0x10;
        }
        b.Vy = Math.Clamp(b.Vy, -0x200, 0x200);
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
            if (s.Mine) { if (!OnScreen(s.X)) s.Dead = true; continue; } // world-fixed; gone once off screen
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
            int n = Rng.RMax(6);   // 1..7 swarmers; MMSW stops at the cap of 20
            for (int i = 0; i < n; i++) SpawnSwarmer(e.X, e.PixelY);   // exactly at the pod (MMSW)
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
