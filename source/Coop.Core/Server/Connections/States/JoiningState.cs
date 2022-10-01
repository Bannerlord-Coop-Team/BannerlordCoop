using Common.Messaging;

namespace Coop.Core.Server.Connections.States
{
    public class JoiningState : ConnectionStateBase
    {
        public JoiningState(IConnectionLogic connectionLogic, IMessageBroker messageBroker) 
            : base(connectionLogic, messageBroker)
        {
        }

        public override void Join()
        {
        }

        public override void Load()
        {
            ConnectionLogic.State = new LoadingState(ConnectionLogic, MessageBroker);
        }

        public override void EnterCampaign()
        {
        }

        public override void EnterMission()
        {
        }
    }
}
