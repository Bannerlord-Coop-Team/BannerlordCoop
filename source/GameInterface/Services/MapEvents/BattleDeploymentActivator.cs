namespace GameInterface.Services.MapEvents;

/// <summary>
/// Gates the release of the host-driven NPC AI at the start of a coop field battle (the "NPC parties do not
/// begin moving until any client has finished deployment" rule). Implemented by <see cref="BattleDeploymentActivator"/>.
/// </summary>
public interface IBattleDeploymentActivator
{
    /// <summary>True once the NPC AI has been released, or recorded as released by the host's broadcast.</summary>
    bool IsActivated { get; }

    /// <summary>The local player finished their own deployment (Start Battle). True = broadcast battle-activated.</summary>
    bool OnLocalDeploymentFinished(bool isLocalHost);

    /// <summary>A peer announced it finished deploying. True = broadcast battle-activated AND release the NPC AI.</summary>
    bool OnRemoteDeploymentFinished(bool isLocalHost);

    /// <summary>The host announced the battle is live (NPCs released); recorded for a possible later promotion.</summary>
    void OnBattleActivatedReceived();

    /// <summary>This client was just promoted to host (migration). True = release the adopted NPC AI.</summary>
    bool OnPromotedToHost();
}

/// <summary>
/// Pure decision logic for releasing the host-driven NPC AI at the start of a coop field battle. NPCs spawn
/// frozen during deployment; the battle goes live on the FIRST deployment-finish from ANY client — the "any
/// client" rule of "NPC parties do not begin moving until any client has finished deployment". Also tracks the
/// live state so a host promoted by migration releases the NPCs it adopts.
/// <para>
/// Inputs in, verdicts out: the caller (<c>BattleDeploymentCoordinator</c>) owns every mission/mesh side
/// effect, so this stays free of mission and network types and its tests assert return values, not callbacks.
/// </para>
/// </summary>
public sealed class BattleDeploymentActivator : IBattleDeploymentActivator
{
    private bool activated;

    /// <summary>True once the NPC AI has been released, or recorded as released by the host's broadcast.</summary>
    public bool IsActivated => activated;

    /// <summary>
    /// The local player finished their own deployment (Start Battle). If we are the host, the native
    /// FinishDeployment that triggered this already un-paused our NPCs, so the battle just went live.
    /// Returns true when the caller must broadcast the battle-activated signal (the host's first activation);
    /// NO NPC release is asked for — the surgical release is only for a still-deploying host. A non-host
    /// activates nothing (its NPCs are host-driven puppets); duplicate finishes are no-ops.
    /// </summary>
    public bool OnLocalDeploymentFinished(bool isLocalHost)
    {
        if (!isLocalHost) return false;
        return MarkActivated();
    }

    /// <summary>
    /// A peer finished deploying. Only the host acts (it owns the NPCs); a non-host's NPCs are host-driven
    /// puppets. On the host's first peer-finish we are still in our own deployment, so the caller must release
    /// the NPC AI now AND broadcast that the battle is live — that is what a true return asks for. Once
    /// activated (our own finish, or an earlier peer), later finishes are no-ops.
    /// </summary>
    public bool OnRemoteDeploymentFinished(bool isLocalHost)
    {
        if (!isLocalHost) return false;
        return MarkActivated();
    }

    /// <summary>
    /// The host announced the battle is live. Record it (idempotent). A non-host takes no NPC action here — the
    /// value is for migration: if this client is later promoted to host, it knows the NPCs it adopts must be
    /// released (see <see cref="OnPromotedToHost"/>).
    /// </summary>
    public void OnBattleActivatedReceived() => activated = true;

    /// <summary>
    /// We were just promoted to host (the previous host left). Returns true when the caller must release the
    /// NPC AI we just adopted (the battle was already live) — otherwise the deployment freeze (a still-deploying
    /// new host has AI ticking off) would hold them frozen even though they were moving under the old host.
    /// False while the battle is not live: the NPCs stay frozen until the first finish, preserving the gate.
    /// </summary>
    public bool OnPromotedToHost() => activated;

    private bool MarkActivated()
    {
        if (activated) return false;
        activated = true;
        return true;
    }
}
