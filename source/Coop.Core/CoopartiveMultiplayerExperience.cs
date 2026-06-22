using Autofac;
using Common;
using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Server;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.Services.GameState.Interfaces;
using GameInterface.Services.UI.Interfaces;
using GameInterface.Services.UI.Messages;
using Serilog;
using System;
using System.Threading.Tasks;

namespace Coop.Core
{
    public class CoopartiveMultiplayerExperience : IDisposable
    {
        private const string ServerLoadingTitle = "Hosting Coop Server";

        private static readonly ILogger Logger = LogManager.GetLogger<CoopartiveMultiplayerExperience>();

        private IMessageBroker messageBroker;
        private INetworkConfig configuration;
        private IContainer container;

        // True while a server host start is in flight.
        private volatile bool serverStarting;

        public CoopartiveMultiplayerExperience()
        {
            // TODO use DI maybe?
            messageBroker = MessageBroker.Instance;
            configuration = new NetworkConfig();

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

            configuration = new NetworkConfig()
            {
                Address = connectMessage.Address.ToString(),
                Port = connectMessage.Port,
            };

            StartAsClient(configuration);
        }

        private void Handle(MessagePayload<HostSaveGame> obj)
        {
            string saveName = obj.What.SaveName;

            StartAsServer(() =>
            {
                container.Resolve<ILoadingInterface>().SetLoadingMessage(
                    ServerLoadingTitle, "Loading campaign save...");
                container.Resolve<IGameStateInterface>().LoadGame(saveName);
            });
        }

        private void Handle(MessagePayload<EndCoopMode> payload)
        {
            serverStarting = false;
            DestroyContainer();

            messageBroker.Publish(this, new CoopModeEnded());
        }

        public int Priority => 0;

        // Debounces host starts (e.g. if the user clicks "Host Co-op Campaign" twice).
        private bool TryBeginServerStart()
        {
            if (serverStarting)
            {
                Logger.Information("Host start ignored: a server start is already in progress.");
                return false;
            }

            serverStarting = true;
            return true;
        }

        public void StartAsServer(Action afterStart = null)
        {
            if (!TryBeginServerStart()) return;
            PrepareServerContainer();

            var loadingInterface = container.Resolve<ILoadingInterface>();

            void BringUp()
            {
                try
                {
                    container.Resolve<ILogic>().Start();
                    afterStart?.Invoke();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Server host bring-up failed; hiding the loading screen.");
                    loadingInterface.HideLoadingScreen();
                }
                finally
                {
                    serverStarting = false;
                }
            }

            if (loadingInterface.IsLoadingScreenAvailable)
            {
                Logger.Information("Host start: applying patches off the main thread so the loading screen keeps rendering.");
                Task.Run(() =>
                {
                    try
                    {
                        container.Resolve<IGameInterface>().PatchAll();
                        // RunSafe so a throw in the bring-up can't escape into the game-loop pump.
                        GameThread.RunSafe(BringUp, context: "server host bring-up");
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Failed to apply patches off the main thread; aborting host start.");
                        serverStarting = false;
                    }
                });
            }
            else
            {
                Logger.Information("Host start: no loading screen, applying patches synchronously.");
                // BringUp clears the guard in its own finally; only PatchAll (outside it) needs the guard
                // cleared on a throw, which is then let propagate to the headless launcher.
                try
                {
                    container.Resolve<IGameInterface>().PatchAll();
                }
                catch
                {
                    serverStarting = false;
                    throw;
                }

                BringUp();
            }
        }

        // Builds the server container and shows the loading screen, without the heavy patching. Kept
        // separate so the host flow can paint the loading screen before the blocking patch work.
        private void PrepareServerContainer()
        {
            DestroyContainer();

            ModInformation.IsServer = true;

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            container = builder.Build();

            GameInterface.ContainerProvider.SetContainer(container);

            container.Resolve<ILoadingInterface>().ShowLoadingScreen(
                ServerLoadingTitle, "Applying patches...");
        }

        public void StartAsClient(INetworkConfig configuration = null)
        {
            // Joining as a client abandons any in-flight host start (it rebuilds the container, so that
            // start's marshaled bring-up will see a stale container and bail without clearing the guard).
            serverStarting = false;
            DestroyContainer();

            ModInformation.IsServer = false;

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ClientModule>();
            builder.RegisterModule<GameInterfaceModule>();

            if (configuration != null)
            {
                builder.RegisterInstance(configuration).As<INetworkConfig>().SingleInstance();
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
        }
    }
}
