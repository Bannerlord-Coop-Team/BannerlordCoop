using Autofac;
using Common.PacketHandlers;
using GameInterface.Services.Locations;
using Missions.Services;
using Missions.Services.Agents.Handlers;
using Missions.Services.Arena;
using Missions.Services.BoardGames;
using Missions.Services.Exceptions;
using Missions.Services.Missiles;
using Missions.Services.Missiles.Handlers;
using Missions.Services.Network;
using Missions.Services.Network.Handlers;
using Missions.Services.Taverns;

namespace Missions;

public class MissionModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ExceptionLogger>().AsSelf().AutoActivate().SingleInstance();

        // TODO create handler collector
        builder.RegisterType<BattlesTestGameManager>().AsSelf();
        builder.RegisterType<CoopBattlesController>().AsSelf();
        builder.RegisterType<ArenaTestGameManager>().AsSelf();
        builder.RegisterType<TavernsGameManager>().AsSelf();
        builder.RegisterType<CoopArenaController>().AsSelf();
        builder.RegisterType<BoardGameManager>().AsSelf();

        // The P2P location behaviors are attached to the interior mission by the OpenIndoorMission
        // postfix, which resolves them from the shared container as ILocationMissionBehavior (it cannot
        // reference these Missions types directly). Register the marker alongside AsSelf.
        builder.RegisterType<CoopTavernsController>().As<ILocationMissionBehavior>().AsSelf();
        builder.RegisterType<CoopMissionNetworkBehavior>().As<ILocationMissionBehavior>().AsSelf();

        // Singletons
        builder.RegisterInstance(NetworkAgentRegistry.Instance)
            .As<INetworkAgentRegistry>()
            .SingleInstance();

        // Interface classes. Registered As<IMissionNetwork> (NOT As<INetwork>) so it does not collide
        // with CoopClient's INetwork registration in the shared client container.
        builder.RegisterType<LiteNetP2PClient>().As<IMeshNetwork>().AsSelf().InstancePerLifetimeScope();

        builder.RegisterType<NetworkMissileRegistry>().As<INetworkMissileRegistry>();

        builder.RegisterType<RandomEquipmentGenerator>().As<IRandomEquipmentGenerator>();
        builder.RegisterType<EventQueueManager>().As<IMessagePacketHandler>();
        builder.RegisterType<AgentMovementHandler>().As<IAgentMovementHandler>();
        builder.RegisterType<MissileHandler>().As<IMissileHandler>();
        builder.RegisterType<WeaponPickupHandler>().As<IWeaponPickupHandler>();
        builder.RegisterType<WeaponDropHandler>().As<IWeaponDropHandler>();
        builder.RegisterType<ShieldDamageHandler>().As<IShieldDamageHandler>();
        builder.RegisterType<AgentDamageHandler>().As<IAgentDamageHandler>();
        builder.RegisterType<AgentDeathHandler>().As<IAgentDeathHandler>();
        builder.RegisterType<ServerDisconnectHandler>().As<IServerDisconnectHandler>();

        base.Load(builder);
    }
}