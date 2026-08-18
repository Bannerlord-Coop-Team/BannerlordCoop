using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

/// <summary>
/// Opens the native NavalDLC battle mission while the coop supplier patch is active, then attaches the
/// same coop battle lifecycle used by land and siege missions.
/// </summary>
internal class CoopNavalBattleLauncher : ICoopNavalBattleLauncher
{
    private static readonly ILogger Logger = LogManager.GetLogger<CoopNavalBattleLauncher>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly ICoopBattleBehaviorAttacher behaviorAttacher;

    public CoopNavalBattleLauncher(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        ICoopBattleBehaviorAttacher behaviorAttacher)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.behaviorAttacher = behaviorAttacher;
    }

    public Mission OpenCoopNavalBattle(MissionInitializerRecord rec)
    {
        var mapEvent = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        if (mapEvent == null || !objectManager.TryGetId(mapEvent, out var mapEventId))
        {
            Logger.Error("[BattleSync] Cannot open coop naval battle: no resolvable map event");
            return null;
        }

        var mission = CampaignMission.OpenNavalBattleMission(rec) as Mission;
        if (mission == null)
        {
            Logger.Error("[BattleSync] Native naval battle launcher returned no mission for {MapEventId}", mapEventId);
            return null;
        }

        behaviorAttacher.Attach(mission);
        messageBroker.Publish(mapEvent, new PlayerEnteredBattle(mapEvent));
        Logger.Information("[BattleSync] Opened coop naval battle for {MapEventId} (player side {Side})",
            mapEventId, PartyBase.MainParty.Side);
        return mission;
    }
}
