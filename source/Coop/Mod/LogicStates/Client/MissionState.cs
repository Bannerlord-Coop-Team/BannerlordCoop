using System;

namespace Coop.Mod.LogicStates.Client
{
    public class MissionState : ClientState
    {
        public MissionState(IClientLogic clientContext) : base(clientContext)
        {
        }

        public override void Connect()
        {
            throw new NotImplementedException();
        }
    }
}
