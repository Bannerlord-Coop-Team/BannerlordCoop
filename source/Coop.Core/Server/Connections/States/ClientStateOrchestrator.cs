using Common.Messaging;
using System;

namespace Coop.Core.Server.Connections.States
{
    internal class ClientStateOrchestrator : ClientConnectionBase
    {
        public ClientStateOrchestrator(IClientConnectionLogic clientConnectionLogic, IMessageBroker messageBroker) : base(clientConnectionLogic, messageBroker)
        {
        }

        public override void EnterCampaign()
        {
            throw new NotImplementedException();
        }

        public override void EnterMission()
        {
            throw new NotImplementedException();
        }

        public override void Join()
        {
            throw new NotImplementedException();
        }

        public override void Loading()
        {
            throw new NotImplementedException();
        }
    }
}
