using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// The deployment face of a coop battle, composed by <see cref="CoopBattleController"/>. Holds the pure
/// activation gate (<see cref="BattleDeploymentActivator"/> — release the host's NPC AI on the FIRST
/// deployment-finish from ANY client) and the commit latch for the reveal rule (own-party troops hidden
/// everywhere until the local deployment commit), and performs their mission/mesh side effects inline.
/// </summary>
public interface IBattleDeploymentCoordinator : IDisposable
{
    /// <summary>True once the battle is live: the NPC AI was released, or the host's broadcast recorded it as released.</summary>
    bool IsActivated { get; }

    /// <summary>True once the local player has committed deployment (the withheld own-party troops were revealed).</summary>
    bool IsCommitted { get; }

    /// <summary>Whether a freshly-captured owned spawn must be withheld from peers (own-party troop, pre-commit).</summary>
    bool ShouldWithhold(bool isOwnPartyTroop);

    /// <summary>
    /// The local player finished their own deployment (Start Battle): announce it to the mesh, mark the battle
    /// live if we are the host, and charge our own troops when no player agent leads them. Returns true on the
    /// FIRST commit only — the caller must then reveal the withheld own-party troops at their deployed
    /// positions (requirement #4), synchronously, before the native un-pause moves them.
    /// </summary>
    bool OnLocalDeploymentFinished();

    /// <summary>This client was just promoted to host (migration): release the adopted NPCs if the battle is live.</summary>
    void OnPromotedToHost();

    /// <summary>
    /// Catch a joining <paramref name="controllerId"/> up on the ACTIVATION state. NetworkBattleActivated is a
    /// one-shot broadcast sent when the battle went live, so a client that joins afterward never heard it and
    /// would otherwise sit at activated=false — its puppets stay frozen through its own deployment (they aren't
    /// un-paused; see the puppet spawn path) and a later promotion to host wouldn't release the NPCs it adopts
    /// (<see cref="OnPromotedToHost"/> gates on activation). Any already-activated peer re-tells the joiner;
    /// the receipt is idempotent. A no-op while the battle is not live.
    /// </summary>
    void CatchUpJoiner(string controllerId);
}

/// <inheritdoc cref="IBattleDeploymentCoordinator"/>
public class BattleDeploymentCoordinator : IBattleDeploymentCoordinator
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleDeploymentCoordinator>();

    private readonly IBattleNetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly IBattleSession session;

    private readonly IBattleDeploymentActivator activator = new BattleDeploymentActivator();

    // Requirement #4 "hidden everywhere until deployed": own-party spawns are withheld from peers until the
    // local deployment commit (spawned locally so they can be placed, but not replicated); this latch is the
    // whole reveal gate. After commit nothing is withheld: own-party reinforcements replicate at once.
    private bool committed;

    // Whether the engine deployer (the mission host) has announced its deployment finished — the moment its
    // engine placements are final on this client. Our siege tactic re-latches once both this and the local
    // commit have happened.
    private bool deployerFinished;

    public BattleDeploymentCoordinator(IBattleNetwork network, IMessageBroker messageBroker, IBattleSession session)
    {
        this.network = network;
        this.messageBroker = messageBroker;
        this.session = session;

        // Clients announce when they finish deploying; the host releases the NPC AI on the first announcement
        // from any client and broadcasts that the battle is live (so a migrated host knows to release the NPCs
        // it adopts).
        messageBroker.Subscribe<NetworkBattleDeploymentFinished>(Handle_NetworkBattleDeploymentFinished);
        messageBroker.Subscribe<NetworkBattleActivated>(Handle_NetworkBattleActivated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkBattleDeploymentFinished>(Handle_NetworkBattleDeploymentFinished);
        messageBroker.Unsubscribe<NetworkBattleActivated>(Handle_NetworkBattleActivated);
    }

    public bool IsActivated => activator.IsActivated;

    public bool IsCommitted => committed;

    // Own-party troops are withheld until the local deployment commit (#4); everything else — the host's
    // NPC/AI — is replicated immediately so it shows frozen on every client during deployment (#1).
    public bool ShouldWithhold(bool isOwnPartyTroop) => !committed && isOwnPartyTroop;

    // [All clients] The local player just finished their own deployment (Start Battle): the native
    // FinishDeployment un-paused our own troops and handed us our hero.
    public bool OnLocalDeploymentFinished()
    {
        // Announce it to the battle mesh so the host releases the NPC AI on the first finish from ANY client.
        network.SendAll(new NetworkBattleDeploymentFinished(session.OwnControllerId));

        // If WE are the host, that same native FinishDeployment also released our NPCs — record the battle as
        // live (ignoring later finishes) and tell everyone; no release needed.
        if (activator.OnLocalDeploymentFinished(session.IsLocalHost))
            network.SendAll(new NetworkBattleActivated(session.InstanceId));

        if (Agent.Main == null)
            ChargeLeaderlessOwnTroops();

        Logger.Information("[BattleSync] Local deployment finished (host={IsHost}, activated={Active})",
            session.IsLocalHost, activator.IsActivated);

        // First commit → the caller reveals the withheld own-party troops; idempotent afterwards.
        if (committed) return false;
        committed = true;

        if (deployerFinished) RelatchSiegeTactic();

        return true;
    }

    public void OnPromotedToHost()
    {
        // If the battle was already live when we were promoted, release the NPC AI we just adopted — a still-
        // deploying new host has AI ticking off, which would otherwise hold them frozen even though they were
        // moving under the previous host. If the battle was not live yet, the gate holds.
        if (activator.OnPromotedToHost())
            ActivateNpcAi();
    }

    public void CatchUpJoiner(string controllerId)
    {
        if (!activator.IsActivated) return;

        network.Send(controllerId, new NetworkBattleActivated(session.InstanceId));
        Logger.Information("[BattleSync] Told joining {Controller} the battle is already activated", controllerId);
    }

    // [Host] A peer finished deploying before we did. Release the NPC AI now so it engages while we (and any
    // other players) are still placing our own formations — the "any client" gate. Our own troops stay frozen
    // until our own Start Battle. Non-hosts drive no NPCs (theirs are puppets that follow us), so the activator
    // returns false for them; and once activated (our own finish, or an earlier peer), later finishes are no-ops.
    private void Handle_NetworkBattleDeploymentFinished(MessagePayload<NetworkBattleDeploymentFinished> payload)
    {
        Logger.Information("[BattleSync] Peer {Controller} finished deployment", payload.What.ControllerId);

        if (activator.OnRemoteDeploymentFinished(session.IsLocalHost))
        {
            network.SendAll(new NetworkBattleActivated(session.InstanceId));
            ActivateNpcAi();
        }

        // The engine deployer finished: its placements are final here (same ReliableOrdered channel).
        // This handler runs on the poll thread while the commit path runs on the game thread; marshal
        // the flag work there so the deployerFinished/committed pair is read and written on one thread.
        if (session.IsHostController(payload.What.ControllerId) && !session.IsLocalHost)
        {
            GameThread.RunSafe(() =>
            {
                deployerFinished = true;
                if (committed) RelatchSiegeTactic();
            });
        }
    }

    // [Non-deployer, game thread] Our siege tactic latched its assault lanes at our own commit, possibly
    // against a not-yet-complete engine set — formations then charge the gate on foot instead of escorting
    // the ram — and a same-side lane reassignment never re-fires the behaviors' machine scan. Reset the
    // latched sides and the tactic so they rescan the final machine set.
    private static void RelatchSiegeTactic()
    {
        GameThread.RunSafe(() =>
        {
            var mission = Mission.Current;
            if (mission?.PlayerTeam == null || !mission.IsSiegeBattle) return;

            foreach (var formation in mission.PlayerTeam.FormationsIncludingSpecialAndEmpty)
            {
                if (formation.CountOfUnits <= 0) continue;
                formation.AI.Side = FormationAI.BehaviorSide.BehaviorSideNotSet;
            }

            mission.PlayerTeam.QuerySystem.Expire();
            mission.PlayerTeam.ResetTactic();

            Logger.Information("[BattleSync] Re-latched the siege tactic against the deployer's final engine set");
        });
    }

    // The host announced the battle is live (NPCs released). Record it so a later promotion to host (migration)
    // releases the NPCs we adopt even while we are still in our own deployment. Non-hosts otherwise need no
    // action — their NPCs are host-driven puppets that follow the host's movement.
    private void Handle_NetworkBattleActivated(MessagePayload<NetworkBattleActivated> payload)
    {
        Logger.Information("[BattleSync] Battle-activated signal received for {Instance}", payload.What.MapEventId);
        activator.OnBattleActivatedReceived();
    }

    // [Host, game thread] Release the host-driven NPC AI so it engages mid-deployment. The mission gates AI
    // globally during deployment (Mission.AllowAiTicking == false) and the agents are AI-paused; turn ticking
    // back on and un-pause the enemy side exactly as the native FinishDeployment does per agent. The host's OWN
    // deploying troops stay put because they remain AI-paused until the host's own Start Battle.
    // Phase B scope: the enemy side (the NPCs the host owns and drives). Releasing allied AI on the host's own
    // side, while excluding the host's own still-deploying party, needs the per-party ownership info that comes
    // with the deployment-authority work (requirement #3/#4) — until then those release on the host's own finish.
    private static void ActivateNpcAi()
    {
        GameThread.RunSafe(() =>
        {
            var mission = Mission.Current;
            if (mission == null) return;

            var hostSide = PartyBase.MainParty?.Side ?? BattleSideEnum.None;
            var enemySide = hostSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;

            // Global AI gate back on so the enemy formations tick; our own side stays put because it is AI-paused.
            mission.AllowAiTicking = true;

            int released = 0;
            foreach (var team in mission.Teams)
            {
                if (team.Side != enemySide) continue;

                foreach (var formation in team.FormationsIncludingSpecialAndEmpty)
                {
                    if (formation.CountOfUnits <= 0) continue;

                    formation.SetControlledByAI(true);
                    formation.ApplyActionOnEachUnit(agent =>
                    {
                        if (!agent.IsAIControlled) return;
                        AgentAiWaker.Wake(agent, onlyResetCachesIfCanWieldWeapon: true);
                        released++;
                    });
                }

                team.QuerySystem.Expire();
                team.ResetTactic();
            }

            Logger.Information("[BattleSync] Released {Count} enemy NPC agent(s) on first deployment finish", released);
        });
    }

    // [Leaderless client, game thread] Our own-party troops spawned but nothing drives them: no player agent (our
    // hero is down) and, in a coop battle, no AI general on the player team. Give each of our formations to the AI
    // and charge so they fight instead of standing idle. Mirrors the per-agent wake the NPC-release and adopt paths
    // do (native FinishDeployment un-pauses agents but issues no formation movement order — that is normally the
    // player's or the AI general's job). Deferred so it runs after native FinishDeployment completes
    // (AllowAiTicking already true). Puppets sit on the ally team, so this only charges our own troops.
    private static void ChargeLeaderlessOwnTroops()
    {
        GameThread.RunSafe(() =>
        {
            var mission = Mission.Current;
            if (mission?.PlayerTeam == null) return;

            mission.AllowAiTicking = true;

            int charged = 0;
            foreach (var formation in mission.PlayerTeam.FormationsIncludingSpecialAndEmpty)
            {
                if (formation.CountOfUnits <= 0) continue;

                formation.SetControlledByAI(true);
                formation.ApplyActionOnEachUnit(agent => AgentAiWaker.Wake(agent, onlyResetCachesIfCanWieldWeapon: true));
                formation.SetMovementOrder(MovementOrder.MovementOrderCharge);
                charged++;
            }

            mission.PlayerTeam.QuerySystem.Expire();
            mission.PlayerTeam.ResetTactic();

            Logger.Information("[BattleSync] Leaderless deployment: charged {Count} own formation(s)", charged);
        });
    }
}
