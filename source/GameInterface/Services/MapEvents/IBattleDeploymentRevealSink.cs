namespace GameInterface.Services.MapEvents;

/// <summary>
/// The reveal effect <see cref="BattleDeploymentRevealGate"/> drives on the local deployment commit: replicate
/// the local player's own-party troops to the other clients at their deployed positions. Implemented by the
/// per-battle controller (<c>CoopBattleController</c>), which owns the mesh send.
/// </summary>
public interface IBattleDeploymentRevealSink
{
    /// <summary>Replicate the local player's own-party troops to peers at their deployed positions.</summary>
    void RevealOwnTroopsAtDeployedPositions();
}
