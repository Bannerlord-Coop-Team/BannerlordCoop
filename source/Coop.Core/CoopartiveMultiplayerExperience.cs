using Autofac;
using Common;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server;
using Coop.Core.Surrogates;
using GameInterface;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Messages;
using System;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience : IUpdateable
    {
        private readonly IMessageBroker messageBroker;
        private IContainer container;
        private INetwork network;

        public CoopartiveMultiplayerExperience(IMessageBroker messageBroker)
        {
            this.messageBroker = messageBroker;

            
            SurrogateCollection.AssignSurrogates();

            messageBroker.Subscribe<AttemptJoin>(Handle);
            messageBroker.Subscribe<HostSaveGame>(Handle);
            messageBroker.Subscribe<EndCoopMode>(Handle);
        }

        

        ~CoopartiveMultiplayerExperience()
        {
            messageBroker.Unsubscribe<AttemptJoin>(Handle);
            messageBroker.Unsubscribe<HostSaveGame>(Handle);
            messageBroker.Unsubscribe<EndCoopMode>(Handle);
        }

        private void Handle(MessagePayload<AttemptJoin> obj)
        {
            var connectMessage = obj.What;

            var config = new NetworkConfiguration()
            {
                Address = connectMessage.Address.ToString(),
                Port = connectMessage.Port,
            };

            StartAsClient(config);
        }

        private void Handle(MessagePayload<HostSaveGame> obj)
        {
            StartAsServer();

            messageBroker.Publish(this, new LoadGame(obj.What.SaveName));
        }

        private void Handle(MessagePayload<EndCoopMode> payload)
        {
            DestroyContainer();

            messageBroker.Publish(this, new CoopModeEnded());
        }

        public int Priority => 0;

        public void Update(TimeSpan deltaTime)
        {
            network?.Update(deltaTime);
        }

        public void StartAsServer()
        {
            DestroyContainer();

            var containerProvider = new ContainerProvider();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance();
            container = builder.Build();

            containerProvider.SetProvider(container);

            // Create harmony patches
            var gameInterface = container.Resolve<IGameInterface>();
            gameInterface.PatchAll();

            network = container.Resolve<INetwork>();

            var logic = container.Resolve<ILogic>();
            logic.Start();
        }

        public void StartAsClient(INetworkConfiguration configuration = null)
        {
            DestroyContainer();

            var containerProvider = new ContainerProvider();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ClientModule>();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance();

            if (configuration != null)
            {
                builder.RegisterInstance(configuration).As<INetworkConfiguration>().SingleInstance();
            }

            container = builder.Build();

            containerProvider.SetProvider(container);

            // Create harmony patches
            var gameInterface = container.Resolve<IGameInterface>();
            gameInterface.PatchAll();

            network = container.Resolve<INetwork>();

            var logic = container.Resolve<ILogic>();
            logic.Start();
        }

        private void DestroyContainer()
        {
            container?.Dispose();
            container = null;
        }
    }
}
