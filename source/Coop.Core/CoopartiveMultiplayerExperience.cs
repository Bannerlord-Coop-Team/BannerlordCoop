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
        private bool passwordInquiryPending;
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
                Token = connectMessage.Password ?? string.Empty,
            };

            var advertisementConfig = new SessionAdvertisementConfig
            {
                EnableSteamInvites = connectMessage.EnableSteamInvites,
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

            var password = obj.What.Password ?? string.Empty;
            if (!ConnectionPassword.IsValid(password))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"The server password cannot exceed {ConnectionPassword.MaxLength} characters"));
                return;
            }

            var visibility = obj.What.Visibility;
            if (!Enum.IsDefined(typeof(ServerVisibility), visibility))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Choose a valid server visibility setting"));
                return;
            }

            AbandonAnyStartingSession();

            // Off Steam, keep the in-process dedicated-server behavior: this instance becomes
            // the server, and the player launches a second instance to join it.
            if (!SessionDiscovery.SteamAvailable)
            {
                StartAsServer(obj.What.SaveName, password, visibility);
                return;
            }

            if (serverProcessManager.IsRunning)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "A hosted co-op server is still running; close its standalone server window before hosting again"));
                return;
            }

            // Mark the session active before spawning, so an instantly-crashing child's exit event
            // is recognised by Handle(HostedServerExited) instead of dropped on !hostedSession.
            hostedSession = true;
            clientConnectedOnce = false;

            try
            {
                serverProcessManager.Start(obj.What.SaveName, password, visibility);
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
                Token = password,
            };

            var advertisementConfig = new SessionAdvertisementConfig
            {
                // The spawned server owns the Steam listener and public lobby. This loopback
                // client must not create a second lobby and user-flavor tunnel.
                EnableSteamInvites = false,
                Visibility = visibility,
            };

            try
            {
                StartAsClient(configuration, advertisementConfig);

                container.Resolve<SteamJoinWatchdog>().Arm(configuration.Address, configuration.Port,
                    timeout: HostedServerStartTimeout,
                    timeoutText: "The co-op server did not finish starting. Check that the save loads in singleplayer. The standalone server remains open until you close it.");

                InformationManager.DisplayMessage(new InformationMessage(
                    "Starting the co-op server; you will join it automatically once it is up"));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Hosted co-op session failed to start");
                hostedSession = false;
                DestroyContainer();
                InformationManager.DisplayMessage(new InformationMessage(
                    "Could not start the co-op client; the standalone server remains open until you close it"));
            }
        }

        // A deliberate new session supersedes the client-side state for any half-started attempt.
        // Its standalone server remains independent and must be closed by the user.
        private void AbandonAnyStartingSession()
        {
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
            var joinInfo = obj.What.JoinInfo;
            if (!CanStartResolvedJoin()) return;

            if (joinInfo.PasswordRequired)
            {
                PromptForSessionPassword(joinInfo);
                return;
            }

            StartResolvedJoin(joinInfo);
        }

        private void PromptForSessionPassword(SessionJoinInfo joinInfo)
        {
            if (passwordInquiryPending) return;
            passwordInquiryPending = true;

            InformationManager.ShowTextInquiry(new TextInquiryData(
                "Server Password",
                "This server requires a password.",
                true,
                true,
                "Join",
                "Cancel",
                password =>
                {
                    passwordInquiryPending = false;
                    joinInfo.Password = password ?? string.Empty;
                    StartResolvedJoin(joinInfo);
                },
                () => passwordInquiryPending = false,
                shouldInputBeObfuscated: true,
                textCondition: password =>
                {
                    bool valid = ConnectionPassword.IsValid(password);
                    return Tuple.Create(valid, valid
                        ? string.Empty
                        : $"Password cannot exceed {ConnectionPassword.MaxLength} characters");
                }));
        }

        private void StartResolvedJoin(SessionJoinInfo joinInfo)
        {
            if (!CanStartResolvedJoin()) return;

            var prepared = joinEndpointPreparer.PrepareAsync(joinInfo).GetAwaiter().GetResult();

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
                Token = joinInfo.Password ?? string.Empty,
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

        private bool CanStartResolvedJoin()
        {
            // Steam callbacks can fire at any moment, so every prompt and callback rechecks state.
            if (container != null)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Already in a co-op session; leave it before joining another"));
                return false;
            }

            if (!GameStateQuery.IsAtMainMenu)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Return to the main menu to join a co-op session"));
                return false;
            }

            return true;
        }

        private void Handle(MessagePayload<SessionJoinFailed> obj)
        {
            InformationManager.DisplayMessage(new InformationMessage(obj.What.Reason));
        }

        private void Handle(MessagePayload<HostSaveGame> obj)
        {
            StartAsServer(obj.What.SaveName, ManagedServerConfig.Password, ManagedServerConfig.Visibility);
        }

        private void Handle(MessagePayload<EndCoopMode> payload)
        {
            DestroyContainer();

            // Ending the client session never owns the standalone server's lifetime. This includes
            // startup failures and watchdog timeouts; only the user closing that process stops it.
            hostedSession = false;
            clientConnectedOnce = false;

            messageBroker.Publish(this, new CoopModeEnded());
        }

        public int Priority => 0;

        public void StartAsServer(string saveName = null) =>
            StartAsServer(saveName, null, ServerVisibility.Public);

        public void StartAsServer(string saveName, string password) =>
            StartAsServer(saveName, password, ServerVisibility.Public);

        public void StartAsServer(string saveName, string password, ServerVisibility visibility)
        {
            // A second Host or Join click while patches are still applying would tear down the in-flight start
            if (coopStarting) return;

            if (!ConnectionPassword.IsValid(password))
                throw new ArgumentOutOfRangeException(nameof(password),
                    $"The server password cannot exceed {ConnectionPassword.MaxLength} characters");
            if (!Enum.IsDefined(typeof(ServerVisibility), visibility))
                throw new ArgumentOutOfRangeException(nameof(visibility));

            DestroyContainer();

            ModInformation.IsServer = true;

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            builder.RegisterInstance(new NetworkConfig { Token = password ?? string.Empty })
                .As<INetworkConfig>()
                .SingleInstance();
            builder.RegisterInstance(new SessionAdvertisementConfig { Visibility = visibility })
                .AsSelf()
                .SingleInstance();
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
