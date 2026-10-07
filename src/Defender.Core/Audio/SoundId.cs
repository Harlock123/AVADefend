namespace Defender.Core.Audio;

/// <summary>Logical sound events emitted by the engine. The presentation layer decides how they sound.</summary>
public enum SoundId
{
    Thrust,          // continuous loop while thrust held
    Fire,
    EnemyShot,
    LanderMaterialize,
    Abduction,       // lander grabs a humanoid
    HumanoidFalling,
    HumanoidCaught,
    HumanoidLanded,
    HumanoidDies,
    MutantCreated,
    EnemyExplode,
    PodExplode,
    BaiterAppear,
    PlayerExplode,
    SmartBomb,
    Hyperspace,
    PlanetExplode,
    WaveStart,
    WaveBonus,
    ExtraLife,
    GameStart,
    GameOver,
    MutantHit,       // SCHSND
    BomberHit,       // TIHSND
    BaiterHit,       // UFHSND (same board sound as SWHSND, higher priority)
    SwarmerHit,      // SWHSND
    MutantShot,      // SSHSND
    SwarmerShot,     // SWSSND
    LanderSuck,      // LSKSND (lander reaches the top with its humanoid)
    Count
}
