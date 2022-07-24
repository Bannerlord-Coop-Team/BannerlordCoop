using Network.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Mission.Messages
{
    public struct ConnectMessage
    {
        public PlayerId player;
        public JoinInfo joinInfo;
    }

    public struct DisconnectMessage
    {
        public PlayerId player;
        public EDisconnectReason reason;
    }

    public struct AgentControlledAmountMessage
    {
        public uint count;
    }

    public struct ClaimControlMessage
    {
        public Guid[] agents;
    }

    public struct NetworkObjectDestroyed
    {
        public Guid[] agents;
    }
}
