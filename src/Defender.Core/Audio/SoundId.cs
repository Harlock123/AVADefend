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
    Count
}
