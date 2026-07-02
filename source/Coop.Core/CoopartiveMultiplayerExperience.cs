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
        private static readonly ILogger Logger = LogManager.GetLogger<CoopartiveMultiplayerExperience>();

        private IMessageBroker messageBroker;
        private INetworkConfig configuration;
        private IContainer container;
        private volatile bool coopStarting;

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
            var saveName = obj.What.SaveName;

            StartAsServer(() => container
                .Resolve<IGameStateInterface>()
                .LoadGame(saveName));
        }

        private void Handle(MessagePayload<EndCoopMode> payload)
        {
            DestroyContainer();

            messageBroker.Publish(this, new CoopModeEnded());
        }

        public int Priority => 0;

        public void StartAsServer(Action afterStart = null)
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
                container.Resolve<ILogic>().Start();
                afterStart?.Invoke();
                return;
            }

            loadingInterface.ShowLoadingScreen("Hosting Coop Server", "Applying patches...");

            PatchAllOffGameThread(gameInterface, loadingInterface, () =>
            {
                loadingInterface.SetLoadingMessage("Hosting Coop Server", "Loading campaign save...");
                container.Resolve<ILogic>().Start();
                afterStart?.Invoke();
            });
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

        public void StartAsClient(INetworkConfig configuration = null)
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
        }
    }
}
