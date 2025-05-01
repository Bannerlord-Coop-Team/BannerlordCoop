using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.UI.Messages;
using System;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience : IDisposable
    {
        private IMessageBroker messageBroker;
        private INetworkConfiguration configuration;
        private IContainer container;
        private INetwork network;

        public CoopartiveMultiplayerExperience()
        {
            configuration = new NetworkConfiguration();
            messageBroker = new MessageBroker();

            messageBroker.Subscribe<AttemptJoin>(Handle);
            messageBroker.Subscribe<HostSaveGame>(Handle);
            messageBroker.Subscribe<EndCoopMode>(Handle);
        }

        public bool Running { get
            {
                if (container == null) return false;

                var logic = container.Resolve<ILogic>();

                return logic.RunningState;
            }
        }

        public void Dispose() => DestroyContainer();

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
            var config = new GameInterfaceConfig()
            {
                IsServer = true,
            };

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterInstance(messageBroker).InstancePerLifetimeScope().ExternallyOwned();
            builder.RegisterInstance(config).As<IGameInterfaceConfig>().InstancePerLifetimeScope();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            container = builder.Build();

            ContainerProvider.SetContainer(container);

            // Create harmony patches
            container.Resolve<IGameInterface>().PatchAll();
            container.Resolve<IAutoSyncPatchCollector>().PatchAll();

            network = container.Resolve<INetwork>();

            var logic = container.Resolve<ILogic>();
            logic.Start();
        }

        public void StartAsClient(INetworkConfiguration networkConfig)
        {
            var config = new GameInterfaceConfig()
            {
                IsServer = false,
            };

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterInstance(messageBroker).InstancePerLifetimeScope().ExternallyOwned();
            builder.RegisterInstance(config).As<IGameInterfaceConfig>().InstancePerLifetimeScope();
            builder.RegisterModule<ClientModule>();
            builder.RegisterModule<GameInterfaceModule>();

            builder.RegisterInstance(networkConfig).As<INetworkConfiguration>().SingleInstance();

            container = builder.Build();

            ContainerProvider.SetContainer(container);

            // Create harmony patches
            container.Resolve<IGameInterface>().PatchAll();
            container.Resolve<IAutoSyncPatchCollector>().PatchAll();

            network = container.Resolve<INetwork>();

            var logic = container.Resolve<ILogic>();
            logic.Start();
        }

        private void DestroyContainer()
        {
            //container?.Resolve<Harmony>().UnpatchAll();
            container?.Dispose();
            container = null;
        }
    }
}
