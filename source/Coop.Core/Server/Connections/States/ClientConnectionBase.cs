using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    public abstract class ClientConnectionBase
    {
        public IMessageBroker MessageBroker;
        public IClientConnectionLogic ClientConnectionLogic;

        public ClientConnectionBase(IClientConnectionLogic clientConnectionLogic, IMessageBroker messageBroker)
        {
            ClientConnectionLogic = clientConnectionLogic;
            MessageBroker = messageBroker;
        }

        public abstract void Join();
        public abstract void Loading();
        public abstract void EnterCampaign();
        public abstract void EnterMission();
    }
}
