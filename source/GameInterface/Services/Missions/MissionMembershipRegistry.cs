namespace GameInterface.Services.Missions;

/// <summary>
/// Session-scoped membership for clients that have entered a mission and have not left it yet.
/// </summary>
public interface IMissionMembershipRegistry
{
    bool IsControllerInMission(string controllerId);

    bool IsInstanceOccupied(string instanceId);
}
