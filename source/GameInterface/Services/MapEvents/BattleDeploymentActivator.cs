using System;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Gates the release of the host-driven NPC AI at the start of a coop field battle (the "NPC parties do not
/// begin moving until any client has finished deployment" rule). Implemented by <see cref="BattleDeploymentActivator"/>.
/// </summary>
public interface IBattleDeploymentActivator
{
    /// <summary>True once the NPC AI has been released, or recorded as released by the host's broadcast.</summary>
    bool IsActivated { get; }

    /// <summary>The local player finished their own deployment (Start Battle): announce it; if host, mark live.</summary>
    void OnLocalDeploymentFinished();

    /// <summary>A peer announced it finished deploying: the host releases the NPC AI on the first such finish.</summary>
    void OnRemoteDeploymentFinished();

    /// <summary>The host announced the battle is live (NPCs released); recorded for a possible later promotion.</summary>
    void OnBattleActivatedReceived();

    /// <summary>This client was just promoted to host (migration): release adopted NPCs if the battle is live.</summary>
    void OnPromotedToHost();
}

/// <summary>
/// Pure coordination for releasing the host-driven NPC AI at the start of a coop field battle — kept free of
/// mission and network types (the side effects are the injected <see cref="IBattleDeploymentBridge"/>) so it is
/// unit-testable. NPCs spawn frozen during deployment; this gate releases them on the FIRST deployment-finish
/// from ANY client — the "any client" rule of "NPC parties do not begin moving until any client has finished
/// deployment". It also tracks the live state so a host promoted by migration releases the NPCs it adopts.
/// </summary>
public sealed class BattleDeploymentActivator : IBattleDeploymentActivator
{
    private readonly IBattleDeploymentBridge bridge;
    private bool activated;

    /// <summary>True once the NPC AI has been released, or recorded as released by the host's broadcast.</summary>
    public bool IsActivated => activated;

    public BattleDeploymentActivator(IBattleDeploymentBridge bridge)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <summary>
    /// The local player finished their own deployment (Start Battle). Always tell peers. If we are the host, the
    /// native FinishDeployment that triggered this already un-paused our NPCs, so just record the battle as live
    /// and broadcast that — we do NOT release again (the surgical release is only for a still-deploying host).
    /// </summary>
    public void OnLocalDeploymentFinished()
    {
        bridge.AnnounceLocalDeploymentFinished();
        if (bridge.IsLocalHost)
            MarkActivatedAndBroadcast();
    }

    /// <summary>
    /// A peer finished deploying. Only the host acts (it owns the NPCs); a non-host's NPCs are host-driven
    /// puppets. On the host's first peer-finish we are still in our own deployment, so release the NPC AI now and
    /// tell everyone the battle is live. Once activated, later finishes are no-ops.
    /// </summary>
    public void OnRemoteDeploymentFinished()
    {
        if (!bridge.IsLocalHost) return;
        if (activated) return;

        MarkActivatedAndBroadcast();
        bridge.ReleaseNpcAi();
    }

    /// <summary>
    /// The host announced the battle is live. Record it (idempotent). A non-host takes no NPC action here — the
    /// value is for migration: if this client is later promoted to host, it knows the NPCs it adopts must be
    /// released (see <see cref="OnPromotedToHost"/>).
    /// </summary>
    public void OnBattleActivatedReceived() => activated = true;

    /// <summary>
    /// We were just promoted to host (the previous host left). If the battle was already live, release the NPC AI
    /// we just adopted — otherwise the deployment freeze (a still-deploying new host has AI ticking off) would
    /// hold them frozen even though they were moving under the old host. If the battle is not live yet, do
    /// nothing: the NPCs stay frozen until the first finish, preserving the gate.
    /// </summary>
    public void OnPromotedToHost()
    {
        if (activated)
            bridge.ReleaseNpcAi();
    }

    private void MarkActivatedAndBroadcast()
    {
        if (activated) return;
        activated = true;
        bridge.BroadcastBattleActivated();
    }
}
