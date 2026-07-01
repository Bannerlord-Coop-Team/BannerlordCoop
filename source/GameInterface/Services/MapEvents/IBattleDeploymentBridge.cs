namespace GameInterface.Services.MapEvents;

/// <summary>
/// The mission/network side effects <see cref="BattleDeploymentActivator"/> drives, abstracted out so the
/// activator stays a pure, unit-testable state machine with no mission or network dependencies. Implemented by
/// the per-battle controller (<c>CoopBattleController</c>), which owns the mesh send and the NPC-release effect.
/// </summary>
public interface IBattleDeploymentBridge
{
    /// <summary>Whether this client is the elected battle host — the authority that owns and drives the NPC AI.</summary>
    bool IsLocalHost { get; }

    /// <summary>Tell the other players in this battle that the local player finished deploying.</summary>
    void AnnounceLocalDeploymentFinished();

    /// <summary>Tell the other players in this battle that the NPC AI has been released (the battle is live).</summary>
    void BroadcastBattleActivated();

    /// <summary>[Host] Un-freeze the host-driven NPC AI so it engages.</summary>
    void ReleaseNpcAi();
}
