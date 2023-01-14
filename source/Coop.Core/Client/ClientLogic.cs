﻿using Common.Logging;
using Common.Messaging;
using Coop.Core.Client.States;
using Coop.Core.Communication.PacketHandlers;
using Serilog;

namespace Coop.Core.Client
{
    /// <summary>
    /// Top level client-side state machine logic orchestrator
    /// </summary>
    public class ClientLogic : IClientLogic
    {
        private readonly ILogger Logger = LogManager.GetLogger<EventPacketHandler>();
        public ICoopClient NetworkClient { get; }
        public IClientState State 
        {
            get { return _state; }
            set 
            {
                _state?.Dispose();
                _state = value;
            } 
        }

        private IClientState _state;

        public ClientLogic(
            ICoopClient networkClient, 
            IMessageBroker messageBroker)
        {
            NetworkClient = networkClient;
            State = new MainMenuState(this, messageBroker);
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
