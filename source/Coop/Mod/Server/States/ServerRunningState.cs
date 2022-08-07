using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.LogicStates.Server
{
    public class ServerRunningState : ServerState
    {
        public ServerRunningState(IServerLogic logic) : base(logic) { }

        public override void StartServer()
        {
            throw new NotImplementedException();
        }

        public override void StopServer()
        {
            throw new NotImplementedException();
        }
    }
}
