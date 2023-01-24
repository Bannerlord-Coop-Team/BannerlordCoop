using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Coop.Core.Client.States;
using Coop.Core.Communication.PacketHandlers;
using GameInterface;
using Serilog;

namespace Coop.Core.Client
{
    /// <summary>
    /// Top level client-side state machine logic orchestrator
    /// </summary>
    public interface IClientLogic : ILogic, IClientState
    {
        /// <summary>
        /// Client-side state
        /// </summary>
        IClientState State { get; set; }

        /// <summary>
        /// Networking Client for Client-side
        /// </summary>
        ICoopClient NetworkClient { get; }
    }

    /// <inheritdoc cref="IClientLogic"/>
    public class ClientLogic : IClientLogic
    {
        private readonly ILogger Logger = LogManager.GetLogger<EventPacketHandler>();
        public ICoopClient NetworkClient { get; }
        public IClientState State 
        {
            get { return _state; }
            set 
            {
                Logger.Debug("Client is changing to {state} State", value.GetType().Name);

                _state?.Dispose();
                _state = value;
            } 
        }

        private IClientState _state;

        private readonly IGameInterface gameInterface;

        public ClientLogic(
            ICoopClient networkClient, 
            IMessageBroker messageBroker,
            IGameInterface gameInterface)
        {
            NetworkClient = networkClient;
            State = new MainMenuState(this, messageBroker);
            this.gameInterface = gameInterface;
        }

        public void Start()
        {
            Connect();
        }

        public void Stop()
        {
            Disconnect();
        }

        public void Dispose()
        {
            State.Dispose();
        }

        public void Connect()
        {
            State.Connect();
        }

        public void Disconnect()
        {
            State.Disconnect();
        }

        public void StartCharacterCreation()
        {
            State.StartCharacterCreation();
        }

        public void LoadSavedData()
        {
            State.LoadSavedData();
        }

        public void ResolveNetworkGuids()
        {
            State.ResolveNetworkGuids();
        }

        public void ExitGame()
        {
            State.ExitGame();
        }

        public void EnterMainMenu()
        {
            State.EnterMainMenu();
        }

        public void EnterCampaignState()
        {
            State.EnterCampaignState();
        }

        public void EnterMissionState()
        {
            State.EnterMissionState();
        }
    }
}
