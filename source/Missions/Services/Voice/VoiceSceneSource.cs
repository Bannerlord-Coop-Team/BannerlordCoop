using GameInterface.Services.Voice;
using Missions.Battles;
using Missions.Taverns;
using Missions.Tournaments;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Voice;

public sealed class VoiceSceneSource : IVoiceSceneSource
{
    public (string Context, bool IsSpectator) GetScene()
    {
        var mission = Mission.Current;
        if (mission == null) return (null, true);
        var battle = mission.GetMissionBehavior<CoopBattleController>();
        var location = mission.GetMissionBehavior<CoopLocationsController>();
        var tournament = mission.GetMissionBehavior<CoopTournamentController>();
        return ResolveScene(battle?.Session, location?.VoiceInstanceId, tournament?.Session,
            tournament?.IsSpectatorAgent(mission.MainAgent) == true);
    }

    internal (string Context, bool IsSpectator) ResolveScene(IBattleSession battle, string location,
        ITournamentMissionSession tournament, bool tournamentSpectator)
    {
        if (battle?.InstanceId != null) return ("battle:" + battle.InstanceId, false);
        if (tournament?.InstanceId != null) return ("scene:" + tournament.InstanceId, tournamentSpectator);
        return location == null ? (null, true) : ("scene:" + location, false);
    }
}
