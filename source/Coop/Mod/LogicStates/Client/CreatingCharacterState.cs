using Common.MessageBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.LogicStates.Client
{
    public class CreatingCharacterState : ClientState
    {
        public CreatingCharacterState(IClientLogic clientContext) : base(clientContext)
        {
        }

        public override void Connect()
        {
            
        }
    }
}
