namespace Defender.Core.Simulation;

/// <summary>
/// Player ship state. The ship lives in *screen* X (1/128 px, like PLAX16) while the camera (BGL) carries
/// the world motion; world X = BGL + screen X (PLABX, defa7.src:2433-2440).
/// </summary>
public sealed class Player
{
    public bool Alive { get; set; }
    public int Facing { get; set; } = 1;
    /// <summary>24-bit horizontal velocity: top 16 bits are world units/frame (PLAXV).</summary>
    public int V24 { get; set; }
    public int ScreenX128 { get; set; }
    public int Y { get; set; }
    public int VyMag { get; set; }
    public int Vy { get; set; }
    public int LastVertical { get; set; }
    public int ReverseCooldown { get; set; }
    public int BombCooldown { get; set; }
    public int AutoFireTimer { get; set; }
    public bool InHyperspace { get; set; }
    public int HyperTimer { get; set; }
    public bool HyperAppearing { get; set; }

    public int V16 => V24 >> 8;
    public int ScreenPx => ScreenX128 >> 7;
    public int PixelY => Y >> 8;
    public Sprite Sprite => Facing > 0 ? Sprites.ShipRight : Sprites.ShipLeft;

    public int WorldX(int cameraX) => (cameraX + ((ScreenX128 >> 2) & ~0x1F)) & Arcade.WorldMask;

    public void Reset()
    {
        Alive = true;
        Facing = 1;
        V24 = 0;
        ScreenX128 = Arcade.ShipBaseRightPx * 128;
        Y = Arcade.ShipStartY << 8;
        VyMag = Vy = LastVertical = 0;
        ReverseCooldown = BombCooldown = AutoFireTimer = 0;
        InHyperspace = HyperAppearing = false;
        HyperTimer = 0;
    }
}

public sealed partial class GameSession
{
    private void StepPlaying(in Input.PlayerInput input)
    {
        int cameraBefore = CameraX;
        var p = Player;
        // Hyperspace blank: STATUS $77 freezes all movement for those 15 frames (defa7.src:3216-3218).
        bool frozen = p.InHyperspace && !p.HyperAppearing;
        if (p.InHyperspace) StepHyperspace();
        else
        {
            HandleButtons(input);
            if (p.InHyperspace) { /* just entered */ }
            else StepPlayerPhysics(input);
        }
        int scroll = (short)(ushort)((CameraX - cameraBefore) & Arcade.WorldMask);
        UpdateStars(p.InHyperspace ? 0 : scroll);
        UpdateLasers();
        if (!frozen)
        {
            UpdateEnemies();
            UpdateHumanoids();
            UpdateShells();
        }
        if (State == SessionState.Playing && p.Alive && !p.InHyperspace && !TestInvulnerable) PlayerCollisions();
        if (State == SessionState.Playing && ++_gexecCounter >= Arcade.GexecFrames)
        {
            _gexecCounter = 0;
            GameExecutive();
        }
        Enemies.RemoveAll(e => e.Dead);
        Shells.RemoveAll(s => s.Dead);
        Lasers.RemoveAll(l => l.Done);
        UpdatePlanetBlow();
        UpdateParticles();
    }

    private void HandleButtons(in Input.PlayerInput input)
    {
        var p = Player;
        if (p.ReverseCooldown > 0) p.ReverseCooldown--;
        if (p.BombCooldown > 0) p.BombCooldown--;

        if (input.ReversePressed && p.ReverseCooldown == 0)
        {
            p.Facing = -p.Facing;     // velocity untouched; drag and thrust do the rest (defa7.src:3157)
            p.ReverseCooldown = Arcade.ReverseRearmFrames;
        }

        bool fire = input.FirePressed;
        if (Policy.HoldToFire && input.FireHeld)
        {
            if (p.AutoFireTimer <= 0) fire = true;
        }
        if (p.AutoFireTimer > 0) p.AutoFireTimer--;
        if (fire && FireLaser()) p.AutoFireTimer = Policy.HoldToFireInterval;

        if (input.SmartBombPressed && p.BombCooldown == 0 && SmartBombs > 0) DetonateSmartBomb();
        if (input.HyperspacePressed) EnterHyperspace();
    }

    private void StepPlayerPhysics(in Input.PlayerInput input)
    {
        var p = Player;
        // Drag then thrust, on the 24-bit velocity (defa7.src:2343-2371): V -= V/64; V += ±3.
        p.V24 -= p.V16 * 4;
        if (input.ThrustHeld) p.V24 += p.Facing * Arcade.ThrustPerFrame;
        p.V24 = Math.Clamp(p.V24, -Arcade.MaxPlayerSpeed16 << 8, Arcade.MaxPlayerSpeed16 << 8);
        int v16 = p.V16;

        // Ship screen position: base column by facing plus a speed lead; slides ≤2 px/frame while the scroll
        // compensates, so the ship's world velocity is unaffected by the slide (defa7.src:2373-2420).
        int target = (p.Facing > 0 ? Arcade.ShipBaseRightPx : Arcade.ShipBaseLeftPx) * 128;
        if (Math.Sign(v16) == p.Facing) target += v16 * 32;
        int delta = Math.Clamp(target - p.ScreenX128, -Arcade.ShipSlidePerFrame128, Arcade.ShipSlidePerFrame128);
        p.ScreenX128 += delta;
        CameraX = WrapX(CameraX + v16 - delta / 4);

        // Vertical: no inertia; 1 px/frame on the first frame, ramping by 8/256 to 2 px/frame (defa7.src:2442-2475).
        int dir = input.Vertical;
        if (dir == 0 || dir != p.LastVertical) p.VyMag = 0;
        if (dir != 0)
        {
            p.VyMag = p.VyMag == 0 ? Arcade.VerticalStartSpeed : Math.Min(p.VyMag + Arcade.VerticalAccel, Arcade.VerticalMaxSpeed);
            int py = p.PixelY;
            if (dir < 0 && py > Arcade.PlayerMinY) p.Y -= p.VyMag;
            if (dir > 0 && py < Arcade.PlayerMaxY) p.Y += p.VyMag;
            p.Y = Math.Clamp(p.Y, Arcade.PlayerMinY << 8, Arcade.PlayerMaxY << 8);
        }
        p.Vy = dir * p.VyMag;
        p.LastVertical = dir;
    }

    // ----- laser ----------------------------------------------------------------------------------

    private bool FireLaser()
    {
        if (Lasers.Count >= Arcade.MaxLasers) return false;
        var p = Player;
        int origin = p.Facing > 0 ? p.ScreenPx + 14 : p.ScreenPx;
        Lasers.Add(new Laser { Origin = origin, Head = origin, Fizzle = origin, Tail = origin, Y = p.PixelY + 4, Dir = p.Facing });
        _sounds.Add(Audio.SoundId.Fire);
        return true;
    }

    private const int LaserProbeWidthPx = 16;

    private void UpdateLasers()
    {
        foreach (var l in Lasers)
        {
            if (l.Done) continue;
            l.Head += l.Dir * Arcade.LaserHeadPxPerFrame;
            l.Fizzle += l.Dir * Arcade.LaserFizzlePxPerFrame;
            l.Tail += l.Dir * Arcade.LaserTailPxPerFrame;
            // LCOL tests the 16x1 probe LASP1 (8 bytes): 6 columns behind the head going right, at the head going left.
            int x0 = l.Dir > 0 ? l.Head - 12 : l.Head;
            if (LaserHit(x0, x0 + LaserProbeWidthPx, l.Y, l.Dir)) { l.Done = true; continue; }
            if (l.Head >= Arcade.ScreenWidth || l.Head <= 10) l.Done = true;
        }
    }

    /// <summary>First object along the beam's direction within this frame's swept segment is killed.</summary>
    private bool LaserHit(int x0, int x1, int y, int dir)
    {
        Enemy? best = null;
        Humanoid? bestH = null;
        int bestX = dir > 0 ? int.MaxValue : int.MinValue;
        foreach (var e in Enemies)
        {
            if (e.Dead || e.Appear > 0 || !OnScreen(e.X)) continue;
            var spr = SpriteOf(e);
            int sx = SignedScreenX(e.X);
            if (!spr.HitsSegment(sx, e.PixelY, x0, x1, y)) continue;
            if (dir > 0 ? sx < bestX : sx > bestX) { best = e; bestX = sx; }
        }
        foreach (var h in Humanoids)
        {
            // Any humanoid can be shot (no points): the laser scan does not skip them (ASTKIL ignores only the ship).
            if (!h.Alive || !OnScreen(h.X)) continue;
            int sx = SignedScreenX(h.X);
            if (!Sprites.Humanoid.HitsSegment(sx, h.PixelY, x0, x1, y)) continue;
            if (dir > 0 ? sx < bestX : sx > bestX) { best = null; bestH = h; bestX = sx; }
        }
        if (bestH is not null) { KillHumanoid(bestH); return true; }
        if (best is not null) { KillEnemy(best, scored: true); return true; }
        return false;
    }

    // ----- smart bomb -----------------------------------------------------------------------------

    private void DetonateSmartBomb()
    {
        SmartBombs--;
        Player.BombCooldown = Arcade.SmartBombRearmFrames;
        _sounds.Add(Audio.SoundId.SmartBomb);
        if (!Policy.SuppressFlashes) FlashFrames = 8; // background complemented every 2 frames (defa7.src:3196)
        // Every drawn enemy (not materialising) dies with normal scoring; humanoids and shells are untouched.
        foreach (var e in Enemies.ToList())
            if (!e.Dead && e.Appear == 0 && OnScreen(e.X)) KillEnemy(e, scored: true);
    }

    // ----- hyperspace -----------------------------------------------------------------------------

    private void EnterHyperspace()
    {
        var p = Player;
        p.InHyperspace = true;
        p.HyperAppearing = false;
        p.HyperTimer = Arcade.HyperspaceBlankFrames;
        Lasers.Clear();
        _sounds.Add(Audio.SoundId.Hyperspace);
    }

    private void StepHyperspace()
    {
        var p = Player;
        if (--p.HyperTimer > 0) return;
        if (!p.HyperAppearing)
        {
            // Relocate (defa7.src:3213-3278): shells deleted, random BGL, random facing, Y = rnd/2 + YMIN, velocity 0.
            Shells.Clear();
            CameraX = (Rng.NextByte() << 8 | Rng.NextByte()) & Arcade.WorldMask;
            p.Facing = (Rng.NextByte() & 1) == 0 ? 1 : -1;
            p.ScreenX128 = (p.Facing > 0 ? Arcade.ShipBaseRightPx : Arcade.ShipBaseLeftPx) * 128;
            p.Y = (Rng.NextByte() / 2 + Arcade.YMin) << 8;
            p.V24 = 0;
            p.VyMag = p.Vy = 0;
            p.HyperAppearing = true;
            p.HyperTimer = Arcade.HyperspaceAppearFrames;
            return;
        }
        p.InHyperspace = false;
        p.HyperAppearing = false;
        // Arrival gamble: dies if the random byte exceeds 192 (≈24.6%), every wave (defa7.src:3275-3277).
        if (Rng.NextByte() > Arcade.HyperspaceDeathThreshold) KillPlayer();
    }

    // ----- collisions -----------------------------------------------------------------------------

    private void PlayerCollisions()
    {
        var p = Player;
        var ps = p.Sprite;
        int px = p.ScreenPx, py = p.PixelY;
        foreach (var e in Enemies)
        {
            if (e.Dead || e.Appear > 0 || !OnScreen(e.X)) continue;
            if (!Sprite.Overlap(ps, px, py, SpriteOf(e), SignedScreenX(e.X), e.PixelY)) continue;
            KillEnemy(e, scored: true);  // ramming kills and scores the enemy, then the player dies (INF)
            KillPlayer();
            return;
        }
        foreach (var s in Shells)
        {
            if (s.Dead || !OnScreen(s.X)) continue;
            var spr = s.Mine ? Sprites.Mine : Sprites.Shot;
            if (!Sprite.Overlap(ps, px, py, spr, SignedScreenX(s.X), s.Y >> 8)) continue;
            s.Dead = true;
            AddScore(25);   // shell kill routine BKIL scores 25 (defa7.src:2700; INF trigger)
            KillPlayer();
            return;
        }
        foreach (var h in Humanoids)
        {
            if (h.State != HumanoidState.Falling || !OnScreen(h.X)) continue;
            if (!Sprite.Overlap(ps, px, py, Sprites.Humanoid, SignedScreenX(h.X), h.PixelY)) continue;
            h.State = HumanoidState.Rescued;     // caught: 500 (defb6.src:398-411)
            h.Vy = 0;
            AddScore(500);
            AddPopup(h.X, h.PixelY, "500");
            _sounds.Add(Audio.SoundId.HumanoidCaught);
        }
    }
}
