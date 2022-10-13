using Common.Messaging;
using Coop.Core.Client;
using Coop.Core.Debugging.Logger;

namespace Coop.Core.Server.Connections.States
{
    public interface IConnectionLogic : IConnectionState
    {
        ICoopClient NetworkClient { get; }
        IConnectionState State { get; set; }
    }

    public class ConnectionLogic : IConnectionLogic
    {
        public ILogger Logger { get; }
        public ICoopClient NetworkClient { get; }
        public IConnectionState State { get; set; }

        public ConnectionLogic(IMessageBroker messageBroker)
        {
            State = new InitialConnectionState(this, messageBroker);
        }

        public void ResolveCharacter()
        {
            State.ResolveCharacter();
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
