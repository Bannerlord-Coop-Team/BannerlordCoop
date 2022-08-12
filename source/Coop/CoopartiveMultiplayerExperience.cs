using Autofac;
using Common;
using Coop.Communication;
using Coop.Configuration;
using Coop.Mod;
using Coop.Mod.Client;
using Coop.Mod.PacketHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop
{
    public class CoopartiveMultiplayerExperience
    {
        public static UpdateableList Updateables { get; } = new UpdateableList();

        private static IContainer _container;

        private static ICoopNetwork coopNetwork;

        private static void Initialize()
        {
            Updateables.Add(GameLoopRunner.Instance);

            PacketManager.RegisterPacketHandler(new ExamplePacketHandler());
        }

        public static void StartAsServer()
        {
            Initialize();
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            _container = builder.Build();

            coopNetwork = _container.Resolve<ICoopNetwork>();

            coopNetwork.Start();
        }

        public static void StartAsClient()
        {
            Initialize();
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ClientModule>();
            _container = builder.Build();

            coopNetwork = _container.Resolve<ICoopNetwork>();

            coopNetwork.Start();
        }
    }
}
