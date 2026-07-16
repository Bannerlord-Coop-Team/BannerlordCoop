namespace GameInterface.Services.MapEvents;

/// <summary>
/// Game-configuration values for the coop battle deployment phase.
/// </summary>
public static class BattleDeploymentConfig
{
    /// <summary>
    /// BR-025 — each player's deployment phase is limited to this many seconds, measured from the moment
    /// that player becomes MISSION-READY (finished loading the battle mission). On expiry the player's
    /// deployment is finished automatically through the same native path the deployment UI's Start Battle
    /// button uses, with their troops at their current positions — making them visible (BR-023) and
    /// counting as a deployment finish for activation (BR-024).
    /// <para>
    /// Zero or a negative value DISABLES the limit: deployment may take unlimited time.
    /// </para>
    /// </summary>
    public static float DeploymentTimeLimitSeconds = 120f;
}
