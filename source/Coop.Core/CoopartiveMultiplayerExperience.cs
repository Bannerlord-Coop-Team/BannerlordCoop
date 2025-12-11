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
using HarmonyLib;
using System;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience : IDisposable
    {
        private static readonly Serilog.ILogger Logger = global::Common.Logging.LogManager.GetLogger<CoopartiveMultiplayerExperience>();
        private readonly IMessageBroker messageBroker;
        private INetworkConfiguration configuration;
        private IContainer container;
        private INetwork network;

        public CoopartiveMultiplayerExperience()
        {
            // Central orchestrator for client/server startup driven by UI messages.
            // TODO consider DI bootstrap at module level.
            messageBroker = MessageBroker.Instance;
            configuration = new NetworkConfiguration();

            // Subscribe to UI commands to start client/server or end Coop mode.
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

            // Build configuration from UI input and start client container.
            configuration = new NetworkConfiguration()
            {
                Address = connectMessage.Address.ToString(),
                Port = connectMessage.Port,
            };

            Logger.Information("Client join requested {Address}:{Port}", configuration.Address, configuration.Port);
            messageBroker.Publish(this, new GameInterface.Services.GameDebug.Messages.SendInformationMessage($"Join requested {configuration.Address}:{configuration.Port}"));
            StartAsClient(configuration);
        }

        private void Handle(MessagePayload<HostSaveGame> obj)
        {
            Logger.Information("Host requested with save {Save}", obj.What.SaveName);
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
            // Compose server-side DI modules
            builder.RegisterModule<ServerModule>();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance().ExternallyOwned();
            builder.RegisterModule<GameInterfaceModule>();
            container = builder.Build();

            containerProvider.SetProvider(container);
            GameInterface.ContainerProvider.SetContainer(container);

            // Create harmony patches
            // Patch game engine interfaces and auto-sync patches before starting logic.
            container.Resolve<IGameInterface>().PatchAll();
            container.Resolve<IAutoSyncPatchCollector>().PatchAll();

            network = container.Resolve<INetwork>();

            var logic = container.Resolve<ILogic>();
            Logger.Information("Starting server logic");
            logic.Start();
        }

        public void StartAsClient(INetworkConfiguration configuration = null)
        {
            DestroyContainer();

            var containerProvider = new ContainerProvider();

            ContainerBuilder builder = new ContainerBuilder();
            // Compose client-side DI modules
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
            // Patch game engine interfaces and auto-sync patches before starting logic.
            container.Resolve<IGameInterface>().PatchAll();
            container.Resolve<IAutoSyncPatchCollector>().PatchAll();

            network = container.Resolve<INetwork>();

            var logic = container.Resolve<ILogic>();
            Logger.Information("Starting client logic");
            messageBroker.Publish(this, new GameInterface.Services.GameDebug.Messages.SendInformationMessage("Starting client logic"));
            logic.Start();
        }

        private void DestroyContainer()
        {
            // Cleanly unpatch and dispose DI container to tear down coop mode.
            container?.Resolve<Harmony>().UnpatchAll();
            container?.Dispose();
            container = null;
        }
    }
}
