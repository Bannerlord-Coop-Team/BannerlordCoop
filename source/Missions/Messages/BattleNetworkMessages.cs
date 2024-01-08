using System;
using Common.Messaging;
using Missions.Services.Network;

namespace Missions.Messages
{
    public struct ConnectMessage : IMessage
    {
        public PlayerId player;
        public JoinInfo joinInfo;
    }

    public struct DisconnectMessage : IMessage
    {
        public PlayerId player;
        public EDisconnectReason reason;
    }

    public struct AgentControlledAmountMessage : IMessage
    {
        public uint count;
    }

    public struct ClaimControlMessage : IMessage
    {
        public Guid[] agents;
    }

    public struct NetworkObjectDestroyed : IMessage
    {
        public Guid[] agents;
    }
}
