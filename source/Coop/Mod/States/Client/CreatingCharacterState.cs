using Common.MessageBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.States.Client
{
    public class CreatingCharacterState : ClientState
    {
        public CreatingCharacterState(IClientContext clientContext) : base(clientContext)
        {
        }

        public override void Connect()
        {
            
        }
    }
}
