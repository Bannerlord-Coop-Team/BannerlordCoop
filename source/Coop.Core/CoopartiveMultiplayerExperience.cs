using Autofac;
using Common;
using Common.LogicStates;
using Coop.Core.Client;
using Coop.Core.Communication.PacketHandlers;
using Coop.Core.Server;
using System;
using System.Xml.Serialization;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience
    {
        public static UpdateableList Updateables { get; } = new UpdateableList();

        private static IContainer _container;

        public static void Initialize()
        {
            GameLoopRunner.Instance.SetGameLoopThread();
            Updateables.Add(GameLoopRunner.Instance);
        }

        public static void Update(TimeSpan deltaTime)
        {
            Updateables.UpdateAll(deltaTime);
        }

        public static void StartAsServer()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ServerModule>();
            _container = builder.Build();

            var logic = _container.Resolve<ILogic>();
            logic.Start();
        }

        public static void StartAsClient()
        {
            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CoopModule>();
            builder.RegisterModule<ClientModule>();
            _container = builder.Build();

            var logic = _container.Resolve<ILogic>();
            logic.Start();
        }
    }
}
