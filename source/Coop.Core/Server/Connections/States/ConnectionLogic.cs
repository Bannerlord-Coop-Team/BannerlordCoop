﻿using Common.Logging;
using Serilog;

namespace Coop.Core.Server.Connections.States
{
    public interface IConnectionLogic : IConnectionState
    {
        IConnectionState State { get; set; }
    }

    public class ConnectionLogic : IConnectionLogic
    {
        private readonly ILogger Logger = LogManager.GetLogger<ConnectionLogic>();
        public IConnectionState State { get; set; }

        public ConnectionLogic()
        {
            State = new InitialConnectionState(this);
        }

        public void ResolveCharacter()
        {
            State.ResolveCharacter();
        }

        public void CreateCharacter()
        {
            State.CreateCharacter();
        }

        public void TransferCharacter()
        {
            State.TransferCharacter();
        }

        public void Load()
        {
            State.Load();
        }

        public void EnterCampaign()
        {
            State.EnterCampaign();
        }

        public void EnterMission()
        {
            State.EnterMission();
        }
    }
}
