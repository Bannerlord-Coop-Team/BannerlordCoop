using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    /// <summary>
    /// Setup for a given ConnectionState
    /// </summary>
    public abstract class ConnectionStateBase : IConnectionState
    {
        public IMessageBroker MessageBroker;
        public IConnectionLogic ConnectionLogic;

        public ConnectionStateBase(IConnectionLogic connectionLogic, IMessageBroker messageBroker)
        {
            ConnectionLogic = connectionLogic;
            MessageBroker = messageBroker;
        }

        public abstract void ResolveCharacter();
        public abstract void CreateCharacter();
        public abstract void TransferCharacter();
        public abstract void Load();
        public abstract void EnterCampaign();
        public abstract void EnterMission();
    }
}
