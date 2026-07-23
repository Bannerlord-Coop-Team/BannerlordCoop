using Autofac;
using Common.Network.Session;
using GameInterface;
using GameInterface.Services.Locations;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Tournaments;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Agents.Patches;
using Missions.Agents.Voice;
using Missions.Battles;
using Missions.Missiles.Handlers;
using Missions.Missiles.Patches;
using Missions.Services.Network;
using Missions.Taverns;
using Missions.Tournaments;
using Missions.Tournaments.Spectators;

namespace Missions;

/// <summary>
/// Composition root for the mission/P2P stack. Lives in the Missions assembly alongside the services it
/// registers (the mesh client, mission context, agent registry, sync handlers) so the whole stack is
/// self-contained. Registered into the client container by <c>ClientModule</c>; standalone mission test
/// harnesses register it directly on top of the base services it depends on.
/// </summary>
public class MissionModule : Module
{
    internal const string MissilePatchCategory = "CoopMissilePatches";
    internal const string AgentVoicePatchCategory = "CoopAgentVoicePatches";

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterInstance(new HarmonyPatchCategoryRegistration(
            typeof(AddMissileAuxPatch).Assembly,
            MissilePatchCategory));
        builder.RegisterInstance(new HarmonyPatchCategoryRegistration(
            typeof(AgentVoicePatch).Assembly,
            AgentVoicePatchCategory));

        builder.RegisterType<LiteNetP2PClient>().As<IBattleNetwork>().InstancePerLifetimeScope();
        builder.RegisterType<NoopSteamMissionBridge>().As<ISteamMissionBridge>().InstancePerLifetimeScope();

        // MissionContext mirrors the server's instance membership and must live for the whole client
        // session (it subscribes to the MissionPeer* messages over the campaign connection), so it is a
        // single per-scope instance, AutoActivated to subscribe up front. AsSelf for tests that resolve
        // the concrete type; As<IMissionContext> for LiteNetP2PClient and the membership handlers. It is
        // registered here (not via the IHandler auto-scan) because it no longer lives in the client
        // namespace the scan filters on — and registering it once keeps a single broker subscription.
        builder.RegisterType<MissionContext>()
            .AsSelf()
            .As<IMissionContext>()
            .InstancePerLifetimeScope()
            .AutoActivate();

        // Everything a mission composes is transient (InstancePerDependency): each mission resolves a
        // fresh CoopMissionController, which pulls a fresh ICoopMissionComponent and a fresh set of sync
        // handlers — so no agent/registry state from a previous mission leaks into the next.
        builder.RegisterType<CoopMissionComponent>().As<ICoopMissionComponent>().InstancePerDependency();

        // The location P2P controller. Resolved as ILocationMissionBehavior by PlayerLocationEntryPatches
        // and attached to every opened location mission (tavern/indoor, town centre, castle courtyard,
        // village). Battles are not location missions and so are intentionally excluded. Transient so each
        // mission gets a fresh controller that is disposed with that mission — InstancePerLifetimeScope
        // would hand a disposed instance to the next mission.
        builder.RegisterType<CoopLocationsController>()
            .AsSelf()
            .As<ILocationMissionBehavior>()
            .InstancePerDependency();

        // BR-102 host-epoch receiver policy. InstancePerDependency so each CoopBattleController (one per
        // battle) is injected a FRESH policy whose accepted-epoch watermark starts clean and never leaks
        // across battles — the controller's per-battle lifetime is the watermark's natural reset. The
        // controller passes that ONE instance to BOTH siege replicators, so they SHARE a single watermark:
        // a superseded hosting generation is then dropped consistently across every host-authority message
        // type (engine placement and machine state/authority), not tracked independently per replicator.
        builder.RegisterType<HostEpochPolicy>()
            .As<IHostEpochPolicy>()
            .InstancePerDependency();

        // The field-battle P2P controller — the battle counterpart to CoopLocationsController. Transient so
        // each mission gets a fresh controller that is disposed with that mission. Attached to the mission by
        // CoopBattleBehaviorAttacher (below), never resolved by type from outside the Missions assembly.
        builder.RegisterType<CoopBattleController>()
            .AsSelf()
            .InstancePerDependency();

        // Attaches the coop battle behaviors to a freshly opened battle mission. Resolved from the container
        // by BattleMissionEntryPatch (the native OpenBattleMission path) and injected into the coop launcher;
        // lives in Missions so it can name the concrete behavior types GameInterface cannot reference.
        // Stateless, so a single per-scope instance is fine.
        builder.RegisterType<CoopBattleBehaviorAttacher>()
            .As<ICoopBattleBehaviorAttacher>()
            .InstancePerLifetimeScope();

        // Builds the coop field-battle mission (mirrors SandBoxMissions.OpenBattleMission with coop suppliers,
        // no deployment phase, and the coop behaviors attached). Resolved from the container by the GameInterface
        // battle flow (OpenAttackMission) as ICoopFieldBattleLauncher; lives in Missions so it can reference the
        // SandBox mission behaviors. Stateless, so a single per-scope instance is fine.
        builder.RegisterType<CoopFieldBattleLauncher>()
            .As<ICoopFieldBattleLauncher>()
            .InstancePerLifetimeScope();

        // Builds the coop walls-assault siege mission (mirrors SandBoxMissions.OpenSiegeMissionWithDeployment
        // with the same coop swaps). Resolved by the GameInterface battle flow as ICoopSiegeBattleLauncher.
        builder.RegisterType<CoopSiegeBattleLauncher>()
            .As<ICoopSiegeBattleLauncher>()
            .InstancePerLifetimeScope();

        builder.RegisterType<CoopTournamentController>()
            .AsSelf()
            .InstancePerDependency();

        builder.RegisterType<TournamentSpectatorAgentManagerFactory>()
            .As<ITournamentSpectatorAgentManagerFactory>()
            .InstancePerLifetimeScope();

        builder.RegisterType<CoopTournamentLauncher>()
            .As<ICoopTournamentLauncher>()
            .InstancePerLifetimeScope();

        // Battle host election: elects on the server, stores the broadcast on clients, AutoActivated so it
        // subscribes up front on both. The assignment store itself (IBattleHostRegistry) is registered by
        // GameInterfaceModule — its handlers gate finalizes/conclusions on it too.
        builder.RegisterType<BattleHostHandler>()
            .AsSelf()
            .InstancePerLifetimeScope()
            .AutoActivate();

        // [Server] Applies owner-reported battle casualties to the authoritative map-event roster.
        builder.RegisterType<BattleCasualtyHandler>()
            .AsSelf()
            .InstancePerLifetimeScope()
            .AutoActivate();

        // Slots spawned agents into their team formation so vanilla's formation markers/order-targeting see
        // them. Injected into the battle spawn sub-services (stateless, so transient lifetime is moot).
        builder.RegisterType<AgentFormationAssigner>().As<IAgentFormationAssigner>().InstancePerDependency();

        builder.RegisterType<NetworkAgentRegistry>().As<INetworkAgentRegistry>().InstancePerLifetimeScope();
        //builder.RegisterType<NetworkMissileRegistry>().As<INetworkMissileRegistry>().InstancePerDependency();
        builder.RegisterType<NetworkWorldItemRegistry>().As<INetworkWorldItemRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<MissileHandler>().As<IMissileHandler>().InstancePerDependency();
        builder.RegisterType<AgentMovementHandler>().As<IAgentMovementHandler>().InstancePerDependency();
        builder.RegisterType<AgentVisualActionAccessor>()
            .As<IAgentVisualActionAccessor>()
            .InstancePerDependency();
        builder.RegisterType<RemoteAgentActionProcessor>()
            .As<IRemoteAgentActionProcessor>()
            .InstancePerDependency();
        builder.RegisterType<AgentActionHandler>().As<IAgentActionHandler>().InstancePerDependency();
        builder.RegisterType<VanillaOrderVoiceService>()
            .As<IVanillaOrderVoiceService>()
            .InstancePerDependency();
        builder.RegisterType<AgentVoiceHandler>().As<IAgentVoiceHandler>().InstancePerDependency();
        builder.RegisterType<WeaponDropHandler>().As<IWeaponDropHandler>().InstancePerDependency();
        builder.RegisterType<WeaponPickupHandler>().As<IWeaponPickupHandler>().InstancePerDependency();
        builder.RegisterType<ShieldDamageHandler>().As<IShieldDamageHandler>().InstancePerDependency();
        //builder.RegisterType<AgentDamageHandler>().As<IAgentDamageHandler>().InstancePerDependency();
        builder.RegisterType<AgentDeathHandler>().As<IAgentDeathHandler>().InstancePerDependency();
    }
}
