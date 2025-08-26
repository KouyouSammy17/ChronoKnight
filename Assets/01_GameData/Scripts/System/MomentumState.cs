/// <summary>
/// Represents the current momentum level of the player.
/// Used for unlocking abilities, applying buffs, and triggering effects.
/// </summary>
public enum MomentumState
{
    None,      // 0% to <25%
    Tier1,     // ≥25% (attack boost)
    Tier2,     // ≥50% (double jump, more damage)
    Tier3,     // ≥75% (air dash, dash extension)
    Max        // 100% (combo finisher, max buffs)
}

