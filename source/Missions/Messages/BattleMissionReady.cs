using Common.Messaging;

namespace Missions.Messages;

/// <summary>
/// Local (broker-only) event: this client's battle mission has FINISHED LOADING — it is MISSION-READY in the
/// BR-010 sense. Published by <see cref="Battles.CoopBattleController.AfterStart"/>: the native
/// <c>MissionState.FinishMissionLoading</c> fans <c>Mission.AfterStart()</c> out to the mission behaviors only
/// once <c>Mission.IsLoadingFinished</c> turned true, so this fires exactly when the loading screen's work is
/// done. <see cref="Battles.BattleHostHandler"/> turns it into the server-side host election request, making
/// the server's per-battle order the mission-ready order (BR-013), not the entry order.
/// </summary>
public record BattleMissionReady : IEvent
{
    /// <summary>The battle's instance id — the map event's object-manager id.</summary>
    public string MapEventId { get; }

    public BattleMissionReady(string mapEventId)
    {
        MapEventId = mapEventId;
    }
}
