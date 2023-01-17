using Common.Logging;
using Coop.Core.Server.Connections.States;
using Serilog;

namespace Coop.Core.Server.Connections
{
    public interface IConnectionLogic : IConnectionState
    {
        IConnectionState State { get; set; }
    }

    public class ConnectionLogic : IConnectionLogic
    {
        private readonly ILogger Logger = LogManager.GetLogger<ConnectionLogic>();
        public IConnectionState State 
        {
            get { return _state; }
            set
            {
                Logger.Debug("Client is changing to {state} State", value);

                _state = value;
            }
        }
        private IConnectionState _state;

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
