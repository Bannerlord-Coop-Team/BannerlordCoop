using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using GameInterface;
using GameInterface.Serialization;
using GameInterface.Services.ObjectManager;
using IntroServer.Config;
using Missions.Messages;
using Missions.Services;
using Missions.Services.Agents.Handlers;
using Missions.Services.Agents.Packets;
using Missions.Services.Arena;
using Missions.Services.BoardGames;
using Missions.Services.Missiles.Handlers;
using Missions.Services.Network;
using Missions.Services.Taverns;
using TaleWorlds.ObjectSystem;

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

            builder.RegisterType<RandomEquipmentGenerator>().As<IRandomEquipmentGenerator>();
            builder.RegisterType<PacketManager>().As<IPacketManager>().InstancePerLifetimeScope();
            builder.RegisterType<EventPacketHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<MovementHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<MissileHandler>().As<IMissileHandler>().InstancePerLifetimeScope().AutoActivate();
            builder.RegisterType<WeaponPickupHandler>().As<IWeaponPickupHandler>().InstancePerLifetimeScope().AutoActivate();
            builder.RegisterType<WeaponDropHandler>().As<IWeaponDropHandler>().InstancePerLifetimeScope().AutoActivate();
            builder.RegisterType<ShieldBreakHandler>().As<IShieldBreakHandler>().InstancePerLifetimeScope().AutoActivate();
            builder.RegisterType<AgentDamageHandler>().As<IAgentDamageHandler>().InstancePerLifetimeScope().AutoActivate();

            base.Load(builder);
        }
    }
}