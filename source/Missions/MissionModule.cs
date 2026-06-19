using Autofac;
using GameInterface.Missions.Agents.Handlers;
using GameInterface.Missions.Missiles;
using GameInterface.Missions.Missiles.Handlers;
using GameInterface.Missions.Services.Network;

namespace GameInterface.Missions;

/// <summary>
/// Composition root for the mission/P2P stack. Lives in the Missions assembly alongside the services it
/// registers (the mesh client, mission context, agent registry, sync handlers) so the whole stack is
/// self-contained. Registered into the client container by <c>ClientModule</c>; standalone mission test
/// harnesses register it directly on top of the base services it depends on.
/// </summary>
public class MissionModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<LiteNetP2PClient>().As<IBattleNetwork>().InstancePerLifetimeScope();

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

        builder.RegisterType<NetworkAgentRegistry>().As<INetworkAgentRegistry>().InstancePerDependency();
        //builder.RegisterType<NetworkMissileRegistry>().As<INetworkMissileRegistry>().InstancePerDependency();
        builder.RegisterType<MissileHandler>().As<IMissileHandler>().InstancePerDependency();
        builder.RegisterType<WeaponDropHandler>().As<IWeaponDropHandler>().InstancePerDependency();
        builder.RegisterType<WeaponPickupHandler>().As<IWeaponPickupHandler>().InstancePerDependency();
        builder.RegisterType<ShieldDamageHandler>().As<IShieldDamageHandler>().InstancePerDependency();
        //builder.RegisterType<AgentDamageHandler>().As<IAgentDamageHandler>().InstancePerDependency();
        builder.RegisterType<AgentDeathHandler>().As<IAgentDeathHandler>().InstancePerDependency();
    }
}
