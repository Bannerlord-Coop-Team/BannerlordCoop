using Autofac;
using Common;
using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.Network.Session.Messages;
using Coop.Core.Client;
using Coop.Core.Client.Messages;
using Coop.Core.Client.Services.Session;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Core.Common.Session;
using Coop.Core.Common.Session.Messages;
using Coop.Core.Server;
using GameInterface;
using GameInterface.AutoSync;
using GameInterface.Services.GameDebug.Messages;
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
        private readonly ServerProcessManager serverProcessManager;
        private volatile bool coopStarting;
        private volatile bool hostedSession;
        private volatile bool clientConnectedOnce;
        // Bumped when a new host attempt starts, so a prior attempt's deferred exit handling drops out.
        private volatile int hostSessionGeneration;

        // A spawned server has to load the whole campaign save before it binds its port.
        public static readonly TimeSpan HostedServerStartTimeout = TimeSpan.FromMinutes(5);

        public CoopartiveMultiplayerExperience()
        {
            // TODO use DI maybe?
            messageBroker = MessageBroker.Instance;
            configuration = new NetworkConfig();
            serverProcessManager = new ServerProcessManager(messageBroker);

            messageBroker.Subscribe<AttemptJoin>(Handle);
            messageBroker.Subscribe<AttemptHost>(Handle);
            messageBroker.Subscribe<HostSaveGame>(Handle);
            messageBroker.Subscribe<EndCoopMode>(Handle);
            messageBroker.Subscribe<SessionJoinInfoResolved>(Handle);
            messageBroker.Subscribe<SessionJoinFailed>(Handle);
            messageBroker.Subscribe<HostedServerExited>(Handle);
            messageBroker.Subscribe<NetworkConnected>(Handle);
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

            AbandonAnyStartingSession();

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

        private void Handle(MessagePayload<AttemptHost> obj)
        {
            if (!GameStateQuery.IsAtMainMenu)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Return to the main menu to host a co-op session"));
                return;
            }

            // A previous start is still applying patches; StartAsClient/StartAsServer would no-op,
            // so bail before spawning a server this instance can't wire itself to.
            if (coopStarting) return;

            AbandonAnyStartingSession();

            // Off Steam, keep the in-process dedicated-server behavior: this instance becomes
            // the server, and the player launches a second instance to join it.
            if (!SessionDiscovery.SteamAvailable)
            {
                StartAsServer(obj.What.SaveName);
                return;
            }

            if (serverProcessManager.IsRunning)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "A hosted co-op server is still running; wait for it to shut down before hosting again"));
                return;
            }

            // Mark the session active before spawning, so an instantly-crashing child's exit event
            // is recognised by Handle(HostedServerExited) instead of dropped on !hostedSession.
            hostedSession = true;
            clientConnectedOnce = false;

            try
            {
                serverProcessManager.Start(obj.What.SaveName);
            }
            catch (Exception ex)
            {
                hostedSession = false;
                Logger.Error(ex, "Failed to spawn the co-op server process");
                InformationManager.DisplayMessage(new InformationMessage(
                    "Could not start the co-op server process"));
                return;
            }

            configuration = new NetworkConfig()
            {
                Address = "127.0.0.1",
            };

            var advertisementConfig = new SessionAdvertisementConfig
            {
                EnableSteamInvites = SessionDiscovery.SteamAvailable,
                PublicAddress = string.Empty,
            };

            try
            {
                StartAsClient(configuration, advertisementConfig);

                container.Resolve<SteamJoinWatchdog>().Arm(configuration.Address, configuration.Port,
                    timeout: HostedServerStartTimeout,
                    timeoutText: "The co-op server did not finish starting. Check that the save loads in singleplayer, then try hosting again.");

                InformationManager.DisplayMessage(new InformationMessage(
                    "Starting the co-op server; you will join it automatically once it is up"));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Hosted co-op session failed to start");
                hostedSession = false;
                DestroyContainer();
                serverProcessManager.Stop();
                InformationManager.DisplayMessage(new InformationMessage(
                    "Could not start the co-op session"));
            }
        }

        // A deliberate new session supersedes any half-started one left at the main menu.
        private void AbandonAnyStartingSession()
        {
            // A never-joined spawned server from a prior Host click would otherwise idle until its
            // own timeout; kill it now. StartAsClient/StartAsServer tear the container down themselves.
            if (hostedSession && !clientConnectedOnce)
            {
                serverProcessManager.Stop();
            }

            hostSessionGeneration++;
            hostedSession = false;
            clientConnectedOnce = false;
        }

        private void Handle(MessagePayload<HostedServerExited> obj)
        {
            if (!hostedSession) return;

            // Once connected, a dead server surfaces as a normal disconnect and that
            // path already returns the player to the main menu with a message.
            if (clientConnectedOnce) return;

            // The exit is for this host attempt; a newer attempt bumps the generation so its
            // just-built session isn't torn down by a stale exit that fires a frame later.
            var generation = hostSessionGeneration;

            GameThread.RunSafe(() =>
            {
                if (hostSessionGeneration != generation) return;
                if (!hostedSession || clientConnectedOnce || container == null) return;

                messageBroker.Publish(this, new SendPopupMessage(
                    "The co-op server closed before the session could start"));
                messageBroker.Publish(this, new EndCoopMode());
            }, context: "HostedServerExited");
        }

        private void Handle(MessagePayload<NetworkConnected> obj)
        {
            clientConnectedOnce = true;
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

            // A pre-connect abort leaves a starting server behind, so backstop it. Once a
            // connection happened the server shuts itself down when its players leave, and
            // may legitimately keep running for friends still on it.
            if (hostedSession && !clientConnectedOnce)
            {
                serverProcessManager.Stop();
            }

            hostedSession = false;
            clientConnectedOnce = false;

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

            // UnpatchAll is currently disabled (see GameInterface.UnpatchAll), so AutoSync-intercepted setters
            // stay live through container.Dispose() below. Clearing the provider first makes any patched call
            // triggered by a disposed handler (e.g. ConversationPartyTracker releasing a held party) see "no
            // container" and fail open, instead of resolving against a lifetime scope mid-disposal and throwing
            // ObjectDisposedException.
            GameInterface.ContainerProvider.Clear();

            container?.Dispose();
            container = null;

            // Post-session resolves (console cheats, leftover patches) must fail gracefully
            // instead of hitting the disposed scope.
            GameInterface.ContainerProvider.Clear();
        }
    }
}
