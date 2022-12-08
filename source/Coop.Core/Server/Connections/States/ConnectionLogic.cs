using Common.Messaging;
using Coop.Core.Debugging.Logger;

namespace Coop.Core.Server.Connections.States
{
    public interface IConnectionLogic : IConnectionState
    {
        IConnectionState State { get; set; }
    }

    public class ConnectionLogic : IConnectionLogic
    {
        public ILogger Logger { get; }
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
