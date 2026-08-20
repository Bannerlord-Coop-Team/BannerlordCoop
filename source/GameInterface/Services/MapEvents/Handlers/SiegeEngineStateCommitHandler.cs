using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// [Server] Applies the mission host's final siege engine states through the vanilla write-back with
/// patches live, so the HP writes and broken-engine removals replicate through the container sync.
/// </summary>
internal class SiegeEngineStateCommitHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineStateCommitHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IBattleHostRegistry hostRegistry;

    public SiegeEngineStateCommitHandler(IMessageBroker messageBroker, IObjectManager objectManager, IBattleHostRegistry hostRegistry)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.hostRegistry = hostRegistry;
        messageBroker.Subscribe<NetworkSiegeEngineStatesReport>(HandleReport);
    }

    private void HandleReport(MessagePayload<NetworkSiegeEngineStatesReport> payload)
    {
        if (ModInformation.IsClient) return;

        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            // BR-102: the write-back is a host-authority report — refuse one stamped by a stale hosting
            // generation (a former host's report in flight across a migration), so it cannot clobber the
            // state the current host will report.
            if (hostRegistry.TryGet(obj.MapEventId, out var assignment) && obj.HostEpoch != assignment.Epoch)
            {
                Logger.Information("Refused siege engine state report for {MapEventId}: stale host epoch {Stale} (current {Current})",
                    obj.MapEventId, obj.HostEpoch, assignment.Epoch);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging<MapEvent>(obj.MapEventId, out var mapEvent)) return;

            var siegeEvent = mapEvent.MapEventSettlement?.SiegeEvent;
            if (siegeEvent == null)
            {
                // The siege ended with the battle (capture or broken siege); there is nothing to write back to.
                return;
            }

            var attackerWeapons = SiegeEngineStateConverter.ToMissionWeapons(obj.AttackerEngines);
            var defenderWeapons = SiegeEngineStateConverter.ToMissionWeapons(obj.DefenderEngines);
            if (mapEvent.IsSiegeAmbush)
            {
                SetAmbushEngineStatesForSide(siegeEvent, siegeEvent.BesiegerCamp, attackerWeapons);
                SetAmbushEngineStatesForSide(siegeEvent, siegeEvent.BesiegedSettlement, defenderWeapons);
            }
            else
            {
                siegeEvent.SetSiegeEngineStatesAfterSiegeMission(attackerWeapons, defenderWeapons);
            }
        });
    }

    internal static void SetAmbushEngineStatesForSide(
        SiegeEvent siegeEvent,
        ISiegeEventSide side,
        IEnumerable<IMissionSiegeWeapon> missionSiegeEngineData)
    {
        var missionWeapons = missionSiegeEngineData?.ToList();
        if (missionWeapons == null || missionWeapons.Count == 0)
            return;

        int missionWeaponIndex = missionWeapons.Count - 1;
        for (int engineIndex = side.SiegeEngines.DeployedSiegeEngines.Count - 1; engineIndex >= 0; engineIndex--)
        {
            var engine = side.SiegeEngines.DeployedSiegeEngines[engineIndex];
            if (!engine.IsActive)
                continue;

            var missionWeapon = missionWeapons[missionWeaponIndex--];
            if (missionWeapon.Health > 0f)
                engine.SetHitpoints(missionWeapon.Health);
            else
                siegeEvent.BreakSiegeEngine(side, engine.SiegeEngine);
        }
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkSiegeEngineStatesReport>(HandleReport);
    }
}
