using Common.Messaging;

namespace Missions.Messages;

/// <summary>Game-thread notification after the accepted assignment is stored in the host registry.</summary>
public readonly struct BattleHostAssignmentApplied : IEvent
{
    public readonly NetworkBattleHostAssigned Assignment;

    public BattleHostAssignmentApplied(NetworkBattleHostAssigned assignment)
    {
        Assignment = assignment;
    }
}
