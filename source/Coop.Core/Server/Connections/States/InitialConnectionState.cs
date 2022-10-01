using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    public class InitialConnectionState : ConnectionStateBase, IConnectionState
    {
        public InitialConnectionState(IConnectionLogic connectionLogic, IMessageBroker messageBroker) 
            : base(connectionLogic, messageBroker)
        {
        }

        public override void Join()
        {
            ConnectionLogic.State = new JoiningState(ConnectionLogic, MessageBroker);
        }

        public override void Load()
        {
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }
    }
}