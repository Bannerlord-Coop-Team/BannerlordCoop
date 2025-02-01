using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server;
using GameInterface;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Messages;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience
    {
        private readonly IMessageBroker messageBroker;
        private INetworkConfiguration configuration;
        private IContainer container;
        private INetwork network;

        public CoopartiveMultiplayerExperience()
        {
            // TODO use DI maybe?
            messageBroker = MessageBroker.Instance;
            configuration = new NetworkConfiguration();

            messageBroker.Subscribe<AttemptJoin>(Handle);
            messageBroker.Subscribe<HostSaveGame>(Handle);
            messageBroker.Subscribe<EndCoopMode>(Handle);
        }

        private void Handle(MessagePayload<AttemptJoin> obj)
        {
            var connectMessage = obj.What;

            configuration = new NetworkConfiguration()
            {
                Address = connectMessage.Address.ToString(),
                Port = connectMessage.Port,
            };

            StartAsClient(configuration);
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

        public void StartAsServer()
        {
            DestroyContainer();

            var containerProvider = new ContainerProvider();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance().ExternallyOwned();
            builder.RegisterModule<GameInterfaceModule>();
            container = builder.Build();

            containerProvider.SetProvider(container);
            GameInterface.ContainerProvider.SetContainer(container);

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
            builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance().ExternallyOwned();
            builder.RegisterModule<GameInterfaceModule>();

            if (configuration != null)
            {
                builder.RegisterInstance(configuration).As<INetworkConfiguration>().SingleInstance();
            }

            container = builder.Build();

            containerProvider.SetProvider(container);
            GameInterface.ContainerProvider.SetContainer(container);

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
