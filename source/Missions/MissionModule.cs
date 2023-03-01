using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using IntroServer.Config;
using Missions.Services;
using Missions.Services.Agents.Handlers;
using Missions.Services.Agents.Packets;
using Missions.Services.Arena;
using Missions.Services.Network;

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

            // Non interface classes
            builder.RegisterType<CoopMissionNetworkBehavior>().AsSelf();
            builder.RegisterType<CoopArenaController>().AsSelf();
            builder.RegisterType<NetworkConfiguration>().AsSelf();
            builder.RegisterType<MovementHandler>().AsSelf();
            builder.RegisterType<WeaponDropHandler>().AsSelf().AutoActivate();
            builder.RegisterType<WeaponPickupHandler>().AsSelf().AutoActivate();

            // Singletons
            builder.RegisterInstance(NetworkMessageBroker.Instance)
                .As<INetworkMessageBroker>()
                .As<IMessageBroker>()
                .SingleInstance()
                .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);


            // Interface classes
            builder.RegisterType<LiteNetP2PClient>().As<INetwork>().AsSelf().SingleInstance();
            builder.RegisterType<NetworkAgentRegistry>().As<INetworkAgentRegistry>().SingleInstance();
            builder.RegisterType<PacketManager>().As<IPacketManager>().SingleInstance();
            builder.RegisterType<RandomEquipmentGenerator>().As<IRandomEquipmentGenerator>();
            builder.RegisterType<EventPacketHandler>().As<IPacketHandler>().AsSelf();

            base.Load(builder);
        }
    }
}