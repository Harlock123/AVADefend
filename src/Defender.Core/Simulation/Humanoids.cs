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
            h.Y = Math.Min(Terrain.HeightAtUnits(h.X) + 4, Arcade.HumanoidMaxWalkY) << 8;
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
            var w = Humanoids[_walkSlot++ % Humanoids.Length];
            if (w.State == HumanoidState.Walking && OnScreen(w.X))
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
                    // Gravity: +8/256 px/frame every 4 frames, below $300 (defb6.src:927-944).
                    if (++h.FallFrames % 4 == 0 && h.Vy + Arcade.HumanoidFallAccel < Arcade.HumanoidFallMax) h.Vy += Arcade.HumanoidFallAccel;
                    h.Y += h.Vy;
                    int ground = Math.Min(Terrain.HeightAtUnits(h.X) + 4, Arcade.HumanoidMaxWalkY);
                    if (h.PixelY >= ground)
                    {
                        h.Y = ground << 8;
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
        _planetBlowTimer = 16 * 8;
        _sounds.Add(SoundId.PlanetExplode);
    }

    public bool PlanetExploding => _planetBlowTimer > 0;

    private void UpdatePlanetBlow()
    {
        if (_planetBlowTimer <= 0) return;
        _planetBlowTimer--;
        if (_planetBlowTimer % 8 != 0) return;
        // 16 bursts of debris near the screen with random flash colours and "lightning".
        int x = CameraX + Rng.Next(Arcade.PlayfieldVisibleWidth) * Arcade.UnitsPerPixel;
        SpawnExplosion(x, Rng.Range(170, 230) << 8, 16, (byte)Rng.Range(2, 9), speed: 3);
        if (!Policy.SuppressFlashes && Rng.Next(2) == 0) FlashFrames = 2;
        _sounds.Add(SoundId.EnemyExplode);
    }
}
