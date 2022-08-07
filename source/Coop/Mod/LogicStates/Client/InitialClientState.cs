using System;

namespace Coop.Mod.LogicStates.Client
{
    internal class InitialClientState : ClientState
    {
        public InitialClientState(IClientLogic clientContext) : base(clientContext)
        {
        }

        public override void Connect()
        {
            throw new NotImplementedException();
        }
    }
}
