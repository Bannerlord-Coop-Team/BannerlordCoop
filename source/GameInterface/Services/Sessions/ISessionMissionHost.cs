using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.Sessions;

/// <summary>
/// The mission side of a session, implemented in the Missions.Sessions assembly and resolved per call by
/// <see cref="SessionLauncher"/>, because GameInterface cannot reference the mission stack.
/// </summary>
public interface ISessionMissionHost
{
    /// <summary>This client is the elected host of the session mission.</summary>
    bool IsLocalHost { get; }

    /// <summary>
    /// Engages the spawn gate and builds the controller the mod adds to its mission behaviors: in the
    /// <c>MissionState.OpenNew</c> list, or in <c>OnBeforeMissionBehaviorInitialize</c>. The mission also needs a
    /// <c>MissionCombatantsLogic</c>, and it owns its end condition and its siege engines.
    /// </summary>
    IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId);

    /// <summary>Asks the server to close the session. False unless this client is the host.</summary>
    bool TryRequestClose();
}
