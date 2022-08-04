using System;

namespace Coop.Mod.States.Server
{
    public abstract class ServerState : IServerState
    {
        protected IServerContext _context;
        public ServerState(IServerContext context)
        {
            _context = context;
        }
        public abstract void StartServer();
        public abstract void StopServer();
    }
}
