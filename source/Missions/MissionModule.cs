using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using GameInterface;
using IntroServer.Config;
using Missions.Services;
using Missions.Services.Agents.Handlers;
using Missions.Services.Arena;
using Missions.Services.BoardGames;
using Missions.Services.Missiles;
using Missions.Services.Missiles.Handlers;
using Missions.Services.Network;
using Missions.Services.Taverns;

namespace Missions
{
    public class MissionModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // TODO find how to make this not disgusting
            if (NetworkMessageBroker.Instance == null)
            {
                // Creates new singleton
                new NetworkMessageBroker();
            }

            builder.RegisterModule<GameInterfaceModule>();

            // Non interface classes
            builder.RegisterType<NetworkConfiguration>().AsSelf().InstancePerLifetimeScope();


            // TODO create handler collector
            builder.RegisterType<ArenaTestGameManager>().AsSelf();
            builder.RegisterType<TavernsGameManager>().AsSelf();
            builder.RegisterType<CoopArenaController>().AsSelf();
            builder.RegisterType<CoopTavernsController>().AsSelf();
            builder.RegisterType<BoardGameManager>().AsSelf();
            builder.RegisterType<CoopMissionNetworkBehavior>().AsSelf();
            
            // Singletons
            builder.RegisterInstance(NetworkMessageBroker.Instance)
                .As<INetworkMessageBroker>()
                .As<IMessageBroker>()
                .SingleInstance()
                .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);


            builder.RegisterInstance(NetworkAgentRegistry.Instance)
                .As<INetworkAgentRegistry>()
                .SingleInstance();

            // Interface classes
            builder.RegisterType<LiteNetP2PClient>().As<INetwork>().AsSelf().InstancePerLifetimeScope();

            builder.RegisterType<NetworkMissileRegistry>().As<INetworkMissileRegistry>();

            builder.RegisterType<RandomEquipmentGenerator>().As<IRandomEquipmentGenerator>();
            builder.RegisterType<PacketManager>().As<IPacketManager>().InstancePerLifetimeScope();
            builder.RegisterType<EventQueueManager>().As<IEventPacketHandler>().InstancePerLifetimeScope();
            builder.RegisterType<AgentMovementHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<MissileHandler>().As<IMissileHandler>();
            builder.RegisterType<WeaponPickupHandler>().As<IWeaponPickupHandler>();
            builder.RegisterType<WeaponDropHandler>().As<IWeaponDropHandler>();
            builder.RegisterType<ShieldDamageHandler>().As<IShieldDamageHandler>();
            builder.RegisterType<AgentDamageHandler>().As<IAgentDamageHandler>();
            builder.RegisterType<AgentDeathHandler>().As<IAgentDeathHandler>();

            base.Load(builder);
        }
    }
}