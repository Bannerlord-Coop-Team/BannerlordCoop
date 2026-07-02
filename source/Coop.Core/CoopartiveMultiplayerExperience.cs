using Autofac;
using Common;
using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.Network.Session.Messages;
using Coop.Core.Client;
using Coop.Core.Client.Services.Session;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Common.Session;
using Coop.Core.Server;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.Services.GameState;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.UI.Messages;
using Serilog;
using System;
using TaleWorlds.Library;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopartiveMultiplayerExperience>();

        private IMessageBroker messageBroker;
        private INetworkConfig configuration;
        private IContainer container;
        private readonly IJoinEndpointPreparer joinEndpointPreparer = new DirectJoinEndpointPreparer();

        public CoopartiveMultiplayerExperience()
        {
            // TODO use DI maybe?
            messageBroker = MessageBroker.Instance;
            configuration = new NetworkConfig();

            messageBroker.Subscribe<AttemptJoin>(Handle);
            messageBroker.Subscribe<HostSaveGame>(Handle);
            messageBroker.Subscribe<EndCoopMode>(Handle);
            messageBroker.Subscribe<SessionJoinInfoResolved>(Handle);
            messageBroker.Subscribe<SessionJoinFailed>(Handle);
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

            configuration = new NetworkConfig()
            {
                Address = connectMessage.Address.ToString(),
                Port = connectMessage.Port,
            };

            var advertisementConfig = new SessionAdvertisementConfig
            {
                EnableSteamInvites = connectMessage.EnableSteamInvites,
                PublicAddress = connectMessage.PublicAddress ?? string.Empty,
            };

            StartAsClient(configuration, advertisementConfig);
        }

        private void Handle(MessagePayload<SessionJoinInfoResolved> obj)
        {
            // Steam callbacks can fire at any moment, so this join must check state itself.
            if (container != null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Already in a co-op session; leave it before joining another"));
                return;
            }

            if (!GameStateQuery.IsAtMainMenu)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Return to the main menu to join a co-op session"));
                return;
            }

            var prepared = joinEndpointPreparer.PrepareAsync(obj.What.JoinInfo).GetAwaiter().GetResult();

            configuration = new NetworkConfig()
            {
                Address = prepared.Address,
                Port = prepared.Port,
            };

            try
            {
                StartAsClient(configuration);

                container.Resolve<SteamJoinWatchdog>().Arm(prepared.Address, prepared.Port);
            }
            catch (Exception ex)
            {
                // Tear down the half-built container, otherwise it blocks every later Steam join.
                Logger.Error(ex, "Steam-initiated join to {Address}:{Port} failed to start", prepared.Address, prepared.Port);
                DestroyContainer();
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Could not connect to the advertised address '{prepared.Address}:{prepared.Port}'"));
            }
        }

        private void Handle(MessagePayload<SessionJoinFailed> obj)
        {
            InformationManager.DisplayMessage(new InformationMessage(obj.What.Reason));
        }

        private void Handle(MessagePayload<HostSaveGame> obj)
        {
            StartAsServer();

            container
                .Resolve<IGameStateInterface>()
                .LoadGame(obj.What.SaveName);
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

            ModInformation.IsServer = true;

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            container = builder.Build();

            GameInterface.ContainerProvider.SetContainer(container);

            // Create harmony patches
            container.Resolve<IGameInterface>().PatchAll();

            var logic = container.Resolve<ILogic>();
            logic.Start();
        }

        public void StartAsClient(INetworkConfig configuration = null, SessionAdvertisementConfig advertisementConfig = null)
        {
            DestroyContainer();

            ModInformation.IsServer = false;

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ClientModule>();
            builder.RegisterModule<GameInterfaceModule>();

            if (configuration != null)
            {
                builder.RegisterInstance(configuration).As<INetworkConfig>().SingleInstance();
            }

            if (advertisementConfig != null)
            {
                builder.RegisterInstance(advertisementConfig).AsSelf().SingleInstance();
            }

            container = builder.Build();

            GameInterface.ContainerProvider.SetContainer(container);

            // Client process does not own the export directory — only the server writes
            // debug export files. This prevents DebugAutoConnect races on that directory.
            AutoSyncConfiguration.ExportFiles = false;

#if DEBUG
            // For debugging faster, normally this is done after connection
            container.Resolve<IGameInterface>().PatchAll();
#endif

            var logic = container.Resolve<ILogic>();
            logic.Start();
        }

        private void DestroyContainer()
        {
            container?.Resolve<IGameInterface>().UnpatchAll();
            container?.Dispose();
            container = null;

            // Post-session resolves (console cheats, leftover patches) must fail gracefully
            // instead of hitting the disposed scope.
            GameInterface.ContainerProvider.Clear();
        }
    }
}
