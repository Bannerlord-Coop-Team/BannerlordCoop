using Common.Messaging;
using GameInterface.Services.Time.Enum;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Handlers
{
    [ProtoContract(SkipConstructor = true)]
    public struct NetworkTimeSpeedChanged : INetworkEvent
    {
        [ProtoMember(1)]
        public TimeControlEnum NewControlMode { get; }

        public NetworkTimeSpeedChanged(TimeControlEnum newControlMode)
        {
            NewControlMode = newControlMode;
        }
    }
}