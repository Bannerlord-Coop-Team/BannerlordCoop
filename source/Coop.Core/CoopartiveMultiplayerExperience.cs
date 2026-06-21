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

        // True while a server host start is in flight. The graphical host applies its patches off the
        // main thread (so the engine keeps painting the loading screen), which returns control to the
        // host menu immediately; this guard drops a second "Host" click during that window, which would
        // otherwise race two server bring-ups and reorder the load events (a null-savedSession NRE).
        // Cleared when the bring-up finishes, on teardown, and on failure. Written from the off-thread
        // failure path, so volatile.
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
            if (!TryBeginServerStart()) return;

            string saveName = obj.What.SaveName;

            // Build the container and show the loading screen now, so the engine paints it before the
            // blocking patch work runs.
            PrepareServerContainer();
            StartServer(() =>
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

        public void StartAsServer()
        {
            if (!TryBeginServerStart()) return;

            // Build the container and show the loading screen now, so the engine paints it before the
            // blocking patch work runs. DEBUG "Host Co-op Campaign" / autoconnect path; the save itself
            // is loaded from InitialServerState once the server logic starts.
            PrepareServerContainer();
            StartServer(() => { });
        }

        // Drops a host start when one is already in flight. The graphical host applies its patches off
        // the main thread and so returns to the host menu immediately; without this, a second "Host"
        // click would race a second server bring-up and reorder the load events (a null-savedSession NRE).
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

        // Applies the patches, then starts the server logic and loads the save (afterStart). Patches must
        // be live before the save loads (the save-load patches publish GameLoaded, which the load flow
        // needs), so they run before the bring-up, not inside it. The patch work takes tens of seconds and
        // would freeze the loading screen on the main thread, so a graphical host runs it OFF the main
        // thread (the engine keeps painting) and marshals the bring-up (which creates objects and publishes
        // messages) BACK to the game thread to keep it ordered. Headless has no loading window, so it runs
        // synchronously (the launcher relies on the load state being pushed before this returns).
        private void StartServer(Action afterStart)
        {
            var startedContainer = container;
            var loadingInterface = container.Resolve<ILoadingInterface>();

            // Runs on the game thread. Self-contained: clears the guard in a finally so a failure can't
            // wedge it (a stuck guard would silently drop every later host attempt), and never lets an
            // exception escape into the game-loop pump.
            void BringUp()
            {
                try
                {
                    container.Resolve<ILogic>().Start();
                    afterStart();
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
                        // A teardown (EndCoopMode / a client join) between scheduling and running
                        // rebuilds/disposes the container; this stale work must not run against it. Those
                        // paths clear serverStarting themselves, so the stale early-return below is safe.
                        if (container != startedContainer) return;
                        container.Resolve<IGameInterface>().PatchAll();

                        // RunSafe so a throw in the bring-up can't escape into the game-loop pump.
                        GameThread.RunSafe(() =>
                        {
                            if (container != startedContainer) return;
                            BringUp();
                        }, context: "server host bring-up");
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Failed to apply patches off the main thread; aborting host start.");
                        serverStarting = false;
                        GameThread.RunSafe(() => loadingInterface.HideLoadingScreen(),
                            context: "hide loading screen after failed host start");
                    }
                });
            }
            else
            {
                Logger.Information("Host start: no loading screen, applying patches synchronously.");
                // PatchAll is outside BringUp's try, so clear the guard here too if it throws; let the
                // exception propagate to the headless launcher.
                try
                {
                    container.Resolve<IGameInterface>().PatchAll();
                    BringUp();
                }
                catch
                {
                    serverStarting = false;
                    throw;
                }
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
