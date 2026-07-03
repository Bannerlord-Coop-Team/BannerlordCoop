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
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Messages;
using Serilog;
using System;
using System.Threading.Tasks;
using TaleWorlds.Library;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopartiveMultiplayerExperience>();

        private IMessageBroker messageBroker;
        private INetworkConfig configuration;
        private IContainer container;
        private readonly SteamOrDirectJoinEndpointPreparer joinEndpointPreparer = new SteamOrDirectJoinEndpointPreparer();
        private volatile bool coopStarting;

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

            // A failed tunnel setup falls back to the advertised address, which a
            // tunnel-only lobby doesn't have; an empty address would resolve to this machine.
            if (!prepared.HasAddress)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Could not set up the Steam connection to the host, and the host has not shared a public address to fall back to"));
                return;
            }

            configuration = new NetworkConfig()
            {
                Address = prepared.Address,
                Port = prepared.Port,
                IsTunneled = prepared.Tunneled,
            };

            try
            {
                StartAsClient(configuration);

                container.Resolve<SteamJoinWatchdog>().Arm(prepared.Address, prepared.Port, prepared.Tunneled);
            }
            catch (Exception ex)
            {
                // Tear down the half-built container, otherwise it blocks every later Steam join.
                Logger.Error(ex, "Steam-initiated join to {Address}:{Port} failed to start", prepared.Address, prepared.Port);
                DestroyContainer();
                // This failure exit publishes no session message, so the tunnel is closed here.
                joinEndpointPreparer.TearDownActiveTunnel();
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
            StartAsServer(obj.What.SaveName);
        }

        private void Handle(MessagePayload<EndCoopMode> payload)
        {
            DestroyContainer();

            messageBroker.Publish(this, new CoopModeEnded());
        }

        public int Priority => 0;

        public void StartAsServer(string saveName = null)
        {
            // A second Host or Join click while patches are still applying would tear down the in-flight start
            if (coopStarting) return;

            DestroyContainer();

            ModInformation.IsServer = true;

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            container = builder.Build();

            GameInterface.ContainerProvider.SetContainer(container);

            var gameInterface = container.Resolve<IGameInterface>();
            var loadingInterface = container.Resolve<ILoadingInterface>();

            // Headless server has no loading window to keep alive; patch synchronously
            if (!loadingInterface.IsLoadingScreenAvailable)
            {
                gameInterface.PatchAll();
                StartServerLogic(saveName);
                return;
            }

            loadingInterface.ShowLoadingScreen("Hosting Coop Server", "Applying patches...");

            PatchAllOffGameThread(gameInterface, loadingInterface, () =>
            {
                loadingInterface.SetLoadingMessage("Hosting Coop Server", "Loading campaign save...");
                StartServerLogic(saveName);
            });
        }

        // LoadGame must follow PatchAll (the LoadPatches postfix publishes GameLoaded), so it runs here, after patching, not at the caller
        private void StartServerLogic(string saveName)
        {
            container.Resolve<ILogic>().Start();

            if (saveName != null)
            {
                container.Resolve<IGameStateInterface>().LoadGame(saveName);
            }
        }

        // The ~30s patch compile must stay off the game thread so the loading window keeps drawing, like the client patching on its network thread
        private void PatchAllOffGameThread(IGameInterface gameInterface, ILoadingInterface loadingInterface, Action continueStart)
        {
            coopStarting = true;

            Task.Factory.StartNew(() =>
            {
                try
                {
                    gameInterface.PatchAll();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Applying patches failed while starting coop");
                    coopStarting = false;
                    GameThread.RunSafe(loadingInterface.HideLoadingScreen);
                    return;
                }

                GameThread.RunSafe(() =>
                {
                    try
                    {
                        continueStart();
                    }
                    catch
                    {
                        loadingInterface.HideLoadingScreen();
                        throw;
                    }
                    finally
                    {
                        coopStarting = false;
                    }
                });
            }, TaskCreationOptions.LongRunning);
        }

        public void StartAsClient(INetworkConfig configuration = null, SessionAdvertisementConfig advertisementConfig = null)
        {
            // A second Host or Join click while patches are still applying would tear down the in-flight start
            if (coopStarting) return;

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
            var gameInterface = container.Resolve<IGameInterface>();
            var loadingInterface = container.Resolve<ILoadingInterface>();

            if (loadingInterface.IsLoadingScreenAvailable)
            {
                loadingInterface.ShowLoadingScreen("Connecting to Coop Server", "Applying patches...");

                PatchAllOffGameThread(gameInterface, loadingInterface, () => container.Resolve<ILogic>().Start());
                return;
            }

            gameInterface.PatchAll();
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
