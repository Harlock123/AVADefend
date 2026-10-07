namespace Defender.Core.Simulation;

// Units: X = world units (32 per pixel, wraps at 65536). Y = 1/256 pixel. Velocities per frame in those units.
// Entities are plain mutable classes with ids/indices instead of object references so state serialises.

public enum EnemyKind { Lander, Mutant, Bomber, Pod, Swarmer, Baiter }

public enum LanderPhase { Cruise, Descend, Lift, Absorb }

public sealed class Enemy
{
    public int Id { get; set; }
    public EnemyKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Vx { get; set; }
    public int Vy { get; set; }
    public int Nap { get; set; }          // frames until next AI tick
    public int ShotTimer { get; set; }    // AI ticks until next shot
    public int Anim { get; set; }
    public int Appear { get; set; }       // >0 while materialising: no AI, no collision, immune to bomb
    public bool Dead { get; set; }

    // Lander
    public LanderPhase Phase { get; set; }
    public int Target { get; set; } = -1; // humanoid slot

    // Bomber
    public int Squad { get; set; }
    public int CruiseY { get; set; }

    // Swarmer
    public int Accel { get; set; }
    public int Dir { get; set; }

    public int PixelY => Y >> 8;
}

public enum HumanoidState { Dead, Walking, Grabbed, Falling, Rescued }

public sealed class Humanoid
{
    public HumanoidState State { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Vy { get; set; }
    public int Facing { get; set; } = 1;
    public int Carrier { get; set; } = -1;   // enemy id when grabbed
    public int FallFrames { get; set; }
    public bool Alive => State != HumanoidState.Dead;
    public int PixelY => Y >> 8;
}

public sealed class Shell
{
    public int X16 { get; set; }      // world units × 16
    public int Y { get; set; }        // 1/256 px
    public int Vx16 { get; set; }
    public int Vy { get; set; }
    public int Life { get; set; }
    public bool Mine { get; set; }
    public bool Dead { get; set; }
    public int X => (X16 >> 4) & Arcade.WorldMask;
}

/// <summary>Player laser. Lives in screen space (does not scroll with the world).</summary>
public sealed class Laser
{
    public int Origin { get; set; }
    public int Head { get; set; }
    public int Fizzle { get; set; }
    public int Tail { get; set; }
    public int Y { get; set; }
    public int Dir { get; set; }
    public bool Done { get; set; }
}

public sealed class Popup
{
    public int X { get; set; }
    public int Y { get; set; }       // pixels
    public string Text { get; set; } = "";
    public int Life { get; set; }
}

public sealed class Particle
{
    public int X { get; set; }       // world units
    public int Y { get; set; }       // 1/256 px
    public int Vx { get; set; }
    public int Vy { get; set; }
    public int Life { get; set; }
    public byte Color { get; set; }
}

public sealed class Star
{
    public int X { get; set; }       // screen px
    public int Y { get; set; }
    public byte Color { get; set; }  // pseudo-palette index
}
