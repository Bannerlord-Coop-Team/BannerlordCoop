using Common.Messaging;
using GameInterface.Services.Heroes.Enum;
using ProtoBuf;

namespace Coop.Core.Server.Services.Time.Messages;

/// <summary>
/// Time speed on server has changed event
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChangeTimeControlMode : IEvent
{
    [ProtoMember(1)]
    public readonly TimeControlEnum NewControlMode;

    public NetworkChangeTimeControlMode(TimeControlEnum newControlMode)
    {
        NewControlMode = newControlMode;
    }
}