using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    public abstract class ConnectionStateBase : IConnectionState
    {
        public IMessageBroker MessageBroker;
        public IConnectionLogic ConnectionLogic;

        public ConnectionStateBase(IConnectionLogic connectionLogic, IMessageBroker messageBroker)
        {
            ConnectionLogic = connectionLogic;
            MessageBroker = messageBroker;
        }

        public abstract void Join();
        public abstract void Load();
        public abstract void EnterCampaign();
        public abstract void EnterMission();
    }
}
