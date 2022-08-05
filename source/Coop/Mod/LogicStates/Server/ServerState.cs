using System;

namespace Coop.Mod.LogicStates.Server
{
    public abstract class ServerState : IServerState
    {
        protected IServerLogic _context;
        public ServerState(IServerLogic context)
        {
            _context = context;
        }
        public abstract void StartServer();
        public abstract void StopServer();
    }
}
