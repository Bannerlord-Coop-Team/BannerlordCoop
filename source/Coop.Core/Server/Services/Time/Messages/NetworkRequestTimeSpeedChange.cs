using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages
{
    [ProtoContract(SkipConstructor = true)]
    public record NetworkRequestTimeSpeedChange : ICommand
    {
        [ProtoMember(1)]
        public TimeControlEnum NewControlMode { get; }

        public NetworkRequestTimeSpeedChange(TimeControlEnum newControlMode)
        {
            NewControlMode = newControlMode;
        }
    }
}
