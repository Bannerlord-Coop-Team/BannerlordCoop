using Common.Logging;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.States;
using GameInterface;
using Serilog;
using System;

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
        INetworkMessageBroker NetworkMessageBroker { get; }
        Guid ControlledHeroId { get; set; }
    }

    /// <inheritdoc cref="IClientLogic"/>
    public class ClientLogic : IClientLogic
    {
        private readonly ILogger Logger = LogManager.GetLogger<ClientLogic>();
        public ICoopClient NetworkClient { get; }
        public INetworkMessageBroker NetworkMessageBroker { get; }
        public Guid ControlledHeroId { get; set; }
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

        public ClientLogic(
            ICoopClient networkClient,
            INetworkMessageBroker messageBroker)
        {
            NetworkClient = networkClient;
            NetworkMessageBroker = messageBroker;
            State = new MainMenuState(this);
        }

        public void Start()
        {
            Connect();
        }

        public void Stop()
        {
            Disconnect();
        }

        public void Dispose() => State.Dispose();

        public void Connect() => State.Connect();

        public void Disconnect() => State.Disconnect();

        public void StartCharacterCreation() => State.StartCharacterCreation();

        public void LoadSavedData() => State.LoadSavedData();

        public void ExitGame() => State.ExitGame();

        public void EnterMainMenu() => State.EnterMainMenu();

        public void EnterCampaignState() => State.EnterCampaignState();

        public void EnterMissionState() => State.EnterMissionState();

        public void ValidateModules() => State.ValidateModules();
    }
}
