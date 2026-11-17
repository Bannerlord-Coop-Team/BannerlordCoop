using Autofac;
using Autofac.Core;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Sessions;
using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;

namespace Missions.Sessions;

/// <summary>Builds the session controller for a mod's mission and answers the host and close questions about it.</summary>
internal sealed class SessionMissionHost : ISessionMissionHost
{
    private readonly IMissionLifetimeFactory lifetimes;
    private readonly IBattleHostRegistry hostRegistry;

    public SessionMissionHost(IMissionLifetimeFactory lifetimes, IBattleHostRegistry hostRegistry)
    {
        this.lifetimes = lifetimes;
        this.hostRegistry = hostRegistry;
    }

    public bool IsLocalHost
    {
        get
        {
            var sessionId = BattleSpawnGate.ActiveMapEventId;
            return SessionInstanceId.IsSession(sessionId) && hostRegistry.IsHost(sessionId);
        }
    }

    public IReadOnlyList<MissionBehavior> CreateMissionBehaviors(string sessionId)
    {
        if (!SessionInstanceId.IsSession(sessionId))
            return Array.Empty<MissionBehavior>();

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
