using Autofac;
using Autofac.Core;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Sessions;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Sessions;

/// <summary>Builds the session controller for a mod's mission and answers the host and close questions about it.</summary>
internal sealed class SessionMissionHost : ISessionMissionHost
{
    private readonly IMissionLifetimeFactory lifetimes;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IControllerIdProvider controllerIds;

    public SessionMissionHost(
        IMissionLifetimeFactory lifetimes,
        IBattleHostRegistry hostRegistry,
        IControllerIdProvider controllerIds)
    {
        this.lifetimes = lifetimes;
        this.hostRegistry = hostRegistry;
        this.controllerIds = controllerIds;
    }

    public bool IsLocalHost
    {
        get
        {
            var sessionId = BattleSpawnGate.ActiveMapEventId;
            return SessionInstanceId.IsSession(sessionId)
                && hostRegistry.TryGet(sessionId, out var assignment)
                && assignment.HostControllerId == controllerIds.ControllerId;
        }
    }

    public IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId)
    {
        if (!SessionInstanceId.IsSession(sessionId))
            return new MissionBehavior[0];

        // Engaged before the mission spawns any agent, so the spawn patch captures the first wave.
        BattleSpawnGate.BeginBattle(sessionId);
        try
        {
            var controller = lifetimes.Create<CoopSessionController>(
                new Parameter[] { new TypedParameter(typeof(SessionMissionContext), new SessionMissionContext(sessionId)) });
            return new MissionBehavior[] { controller };
        }
        catch
        {
            BattleSpawnGate.EndBattle();
            throw;
        }
    }

    public bool TryRequestClose() =>
        Mission.Current?.GetMissionBehavior<CoopSessionController>()?.TryRequestClose() ?? false;
}
