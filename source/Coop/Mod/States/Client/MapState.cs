using Common.MessageBroker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.States.Client
{
    internal class MapState : ClientState
    {
        public MapState(IClientContext clientContext) : base(clientContext)
        {
        }

        public override void Connect()
        {
            throw new NotImplementedException();
        }
    }
}
