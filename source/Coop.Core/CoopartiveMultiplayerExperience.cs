using Autofac;
using Common;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Server;
using GameInterface;
using GameInterface.Services.CharacterCreation.Messages;
using System;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience : IUpdateable
    {
        public static UpdateableList Updateables { get; } = new UpdateableList();

        public static IContainer Container { get; private set; }

        public int Priority => 0;

        public void Update(TimeSpan deltaTime)
        {
            Updateables.UpdateAll(deltaTime);
        }

        public void StartAsServer()
        {
            if(Container == null)
            {
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterModule<CoopModule>();
                builder.RegisterModule<ServerModule>();
                Container = builder.Build();

                var server = Container.Resolve<INetwork>();
                Updateables.Add(server);
            }

            var logic = Container.Resolve<ILogic>();
            logic.Start();
        }

        public void StartAsClient()
        {
            if (Container == null)
            {
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterModule<CoopModule>();
                builder.RegisterModule<ClientModule>();
                Container = builder.Build();

                var client = Container.Resolve<INetwork>();
                Updateables.Add(client);
            }

            var logic = Container.Resolve<ILogic>();
            logic.Start();
        }
    }
}
