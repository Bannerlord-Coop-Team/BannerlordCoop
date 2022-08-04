using Common.MessageBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.States.Client
{
    public class MissionState : ClientState
    {
        public MissionState(IClientContext clientContext) : base(clientContext)
        {
        }

        public override void Connect()
        {
            throw new NotImplementedException();
        }
    }
}
