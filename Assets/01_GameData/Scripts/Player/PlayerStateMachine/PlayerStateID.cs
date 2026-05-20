/// <summary>
/// Enumeration of all player action states in the state machine.
/// </summary>
public enum PlayerStateID
{
    /// <summary>Player is standing or moving on ground</summary>
    Grounded,
    /// <summary>Player is in the air (jumping or falling)</summary>
    Airborne,
    /// <summary>Player is sliding on a wall</summary>
    WallSlide,
    /// <summary>Player is performing a dash move</summary>
    Dash,
    /// <summary>Player is performing a melee attack</summary>
    Attack,
    /// <summary>Player is performing a dash attack</summary>
    DashAttack,
    /// <summary>Player is in hitstun from taking damage</summary>
    Hurt,
    /// <summary>Player is in knockdown state (air-to-ground sequence)</summary>
    Knockdown,
    /// <summary>Player is playing turbo mode activation sequence</summary>
    TurboStart,
    /// <summary>Player is dead</summary>
    Dead,
    /// <summary>Player has won the level</summary>
    Win
}
