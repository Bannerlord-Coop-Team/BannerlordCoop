using Common.Messaging;
using GameInterface.Services.Time.Enum;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages
{
    [ProtoContract]
    public readonly struct NetworkRequestTimeSpeedChange : INetworkEvent
    {
        [ProtoMember(1)]
        public TimeControlEnum NewControlMode { get; }

        public NetworkRequestTimeSpeedChange(TimeControlEnum newControlMode)
        {
            NewControlMode = newControlMode;
        }
    }
}
