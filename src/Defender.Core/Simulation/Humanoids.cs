using Defender.Core.Audio;

namespace Defender.Core.Simulation;

public sealed partial class GameSession
{
    private int _walkSlot;

    /// <summary>Spread over the four quadrants when more than 7, remainder random (defa7.src:1517-1577).</summary>
    private void PlaceHumanoids(int count)
    {
        for (int i = 0; i < Humanoids.Length; i++)
        {
            var h = Humanoids[i];
            if (i >= count) { h.State = HumanoidState.Dead; continue; }
            int x = count > 7 && i < 8
                ? ((i % 4) * 0x40 + Rng.Next(0x20)) << 8 | Rng.NextByte()
                : Rng.Next(Arcade.WorldUnits);
            h.State = HumanoidState.Walking;
            h.X = x & Arcade.WorldMask;
            h.Y = Arcade.TerrainStartY << 8;   // Y = $E0; they settle onto the terrain as they walk on screen
            h.Vy = 0;
            h.Facing = Rng.Sign();
            h.Carrier = -1;
        }
    }

    private void UpdateHumanoids()
    {
        // Walker: one slot every 2 frames, only on screen (defb6.src:294-359).
        if (Frame % 2 == 0)
        {
            // TLIST has 16 slots, so each humanoid is visited every 32 frames (defb6.src:294-299).
            int slot = _walkSlot++ % 16;
            var w = slot < Humanoids.Length ? Humanoids[slot] : null;
            if (w is { State: HumanoidState.Walking } && OnScreen(w.X))
            {
                w.X = WrapX(w.X + w.Facing * Arcade.UnitsPerPixel);
                int goal = Math.Min(Terrain.HeightAtUnits(w.X) + (w.Facing < 0 ? 4 : 15), Arcade.HumanoidMaxWalkY);
                w.Y += Math.Sign(goal - w.PixelY) << 8;
                if (Rng.NextByte() < 9) w.Facing = -w.Facing;
            }
        }

        foreach (var h in Humanoids)
        {
            switch (h.State)
            {
                case HumanoidState.Grabbed:
                {
                    var c = Enemies.FirstOrDefault(e => e.Id == h.Carrier && !e.Dead);
                    if (c is null) { h.State = HumanoidState.Falling; h.Carrier = -1; h.Vy = 0; break; }
                    h.X = WrapX(c.X + 3 * Arcade.UnitsPerPixel);
                    if (c.Phase == LanderPhase.Lift) h.Y = c.Y + (12 << 8);
                    break;
                }
                case HumanoidState.Falling:
                {
                    // AFALL (defb6.src:927-944): every 4 frames, +8 (below $300) and a ground test (altitude ≤ Y);
                    // the velocity is integrated every frame and Y is not snapped on landing.
                    h.Y += h.Vy;
                    if (++h.FallFrames % 4 != 0) break;
                    if (h.Vy + Arcade.HumanoidFallAccel < Arcade.HumanoidFallMax) h.Vy += Arcade.HumanoidFallAccel;
                    if (Terrain.HeightAtUnits(h.X) <= h.PixelY)
                    {
                        if (h.Vy > Arcade.HumanoidSafeLandingSpeed) KillHumanoid(h);
                        else
                        {
                            h.State = HumanoidState.Walking;
                            h.Vy = 0;
                            AddScore(250);
                            AddPopup(h.X, h.PixelY - 10, "250");
                            _sounds.Add(SoundId.HumanoidLanded);
                        }
                    }
                    break;
                }
                case HumanoidState.Rescued:
                {
                    // Rides below the ship; set down for another 500 once the ground is reached (defb6.src:945-962).
                    h.X = WrapX(PlayerWorldX + 4 * Arcade.UnitsPerPixel);
                    h.Y = Player.Y + (10 << 8);
                    int alt = Terrain.HeightAtUnits(h.X);
                    if (PlanetActive && h.PixelY > alt)
                    {
                        h.State = HumanoidState.Walking;
                        h.Y = Math.Min(alt + 4, Arcade.HumanoidMaxWalkY) << 8;
                        AddScore(500);
                        AddPopup(h.X, h.PixelY - 10, "500");
                        _sounds.Add(SoundId.HumanoidLanded);
                    }
                    break;
                }
            }
        }
    }

    private void KillHumanoid(Humanoid h)
    {
        if (!h.Alive) return;
        h.State = HumanoidState.Dead;
        h.Carrier = -1;
        SpawnExplosion(h.X, h.Y, 8, Pal.Purple, speed: 1);
        _sounds.Add(SoundId.HumanoidDies);
        OnHumanoidLost();
    }

    /// <summary>When the last humanoid is gone the planet explodes (TERBLO, defb6.src:421-494).</summary>
    private void OnHumanoidLost()
    {
        if (HumanoidsAlive > 0 || !PlanetActive) return;
        PlanetActive = false;
        _planetBlowTimer = 1;
        _blowIteration = 0;
        _blowWait = 0;
    }

    public bool PlanetExploding => _planetBlowTimer > 0;

    private int _blowIteration, _blowWait;

    /// <summary>TERBLO (defb6.src:437-494): 16 iterations of 2 explosions at the terrain altitude, a random background
    /// colour for 2 frames and the "lightning" sound, then a random 1-3 frame pause; the terrain-blow sound at the end.</summary>
    private void UpdatePlanetBlow()
    {
        if (_planetBlowTimer <= 0) return;
        if (_blowWait > 0) { _blowWait--; return; }
        if (_blowIteration >= 16) { _planetBlowTimer = 0; _sounds.Add(SoundId.PlanetExplode); return; }
        for (int k = 0; k < 2; k++)
        {
            int x = WrapX(CameraX + ((Rng.NextByte() & 0x3F) << 8 | Rng.NextByte()));
            SpawnExplosion(x, (Terrain.HeightAtUnits(x) - 10) << 8, 16, (byte)Rng.Range(2, 9), speed: 3);
        }
        PlanetFlashColor = PlanetFlashCycle[(Rng.NextByte() & 0x1F) % PlanetFlashCycle.Length];   // COLTAB[SEED&$1F]
        _planetFlashFrames = 2;
        _sounds.Add(SoundId.HumanoidDies);   // AHSND: the same "lightning" command as a humanoid being hit
        _blowWait = 2 + Rng.RMax(_blowIteration / 8 + 1) - 1;
        _blowIteration++;
    }

    private int _planetFlashFrames;
    public byte PlanetFlashColor { get; private set; }
    internal (int iteration, int wait, int flash, byte color) BlowState
    {
        get => (_blowIteration, _blowWait, _planetFlashFrames, PlanetFlashColor);
        set => (_blowIteration, _blowWait, _planetFlashFrames, PlanetFlashColor) = value;
    }
    private static readonly byte[] PlanetFlashCycle = [0x07, 0x3F, 0x38, 0xC0, 0xC7, 0xFF, 0x2F, 0x15];

}
