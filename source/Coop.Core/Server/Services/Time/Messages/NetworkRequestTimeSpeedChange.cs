using Common.Logging.Attributes;
using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// Request time speed change command from a client
/// </summary>
[ProtoContract(SkipConstructor = true)]
[BatchLogMessage]
public record NetworkRequestTimeSpeedChange : ICommand
{
    [ProtoMember(1)]
    public TimeControlEnum NewControlMode { get; }

    public NetworkRequestTimeSpeedChange(TimeControlEnum newControlMode)
    {
        NewControlMode = newControlMode;
    }
}
