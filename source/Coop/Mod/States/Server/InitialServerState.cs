
using System;

namespace Coop.Mod.States.Server
{
    public class InitialServerState : ServerState
    {
        public InitialServerState(IServerContext context) : base(context)
        {
        }

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
