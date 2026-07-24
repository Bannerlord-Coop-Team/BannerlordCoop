using Common.Messaging;

namespace Missions.Messages;

/// <summary>[Server, local] A controller entered after server membership was updated.</summary>
public readonly struct MissionMemberEntered : IEvent
{
    public readonly string ControllerId;
    public readonly string InstanceId;
    public readonly bool IsFirstMember;

    public MissionMemberEntered(string controllerId, string instanceId, bool isFirstMember)
    {
        ControllerId = controllerId;
        InstanceId = instanceId;
        IsFirstMember = isFirstMember;
    }
}
