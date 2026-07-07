using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEvent"/> objects
/// </summary>
internal class MapEventRegistry : AutoRegistryBase<MapEvent>
{
    public override bool Debug => true;
    public MapEventRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(MapEvent));

    // Watch FinalizeEventAux, not FinishBattle: every way a battle ends on the server funnels through
    // FinalizeEventAux (FinishBattle -> FinalizeEventAux, FinalizeEvent -> FinalizeEventAux, and the
    // finalize-on-request handler calls it directly), whereas FinishBattle is a tiny wrapper the JIT
    // inlines into MapEvent.Update, so a postfix on it never runs and the destroy is never replicated.
    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(MapEvent), nameof(MapEvent.FinishBattle)),
        AccessTools.Method(typeof(MapEvent), nameof(MapEvent.FinalizeEvent)),
        AccessTools.Method(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))
    };

    public override void RegisterAllObjects()
    {
        foreach (var mapEvent in Campaign.Current.MapEventManager.MapEvents)
        {
            if (mapEvent.StringId == null) continue;

            RegisterExistingObject(mapEvent.StringId, mapEvent);
        }
    }

    public override void OnClientCreated(MapEvent obj, string id)
    {
        using (new AllowedThread())
        {
            obj.StringId = id;
            obj._sides = new MapEventSide[2];
            obj.WonRounds = new MBList<BattleSideEnum>();
        }

        // OnMapEventCreated adds to MapEventManager._mapEvents, the list the main-thread map tick
        // (MapEventManager.Tick) walks every frame. This callback runs on the network thread, so defer
        // the add to the main thread — matching OnClientDestroyed — so it can't race that iteration and
        // leave a torn/null slot the tick dereferences.
        GameThread.RunSafe(() =>
        {
            using (new AllowedThread())
            {
                Campaign.Current.MapEventManager.OnMapEventCreated(obj);
            }
        });
    }

    public override void OnClientDestroyed(MapEvent obj, string id)
    {
        GameThread.Run(() =>
        {
            // Captured before FinishBattle clears it. Also gates the reward-snapshot capture below; the PvP
            // encounter close itself is driven server-side via NetworkClosePvpEncounter, not from this teardown.
            bool localPartyWasInvolved = MobileParty.MainParty?.MapEvent == obj;
            // The action is deferred, so the campaign can be torn down (disconnect, save-load) before it runs.
            if (Campaign.Current == null) return;

            using (new AllowedThread())
            {
                // Clear each involved party's battle state directly: the event's own FinalizeEventAux
                // short-circuits on IsFinalized, and PartyBase.MapEventSide isn't synced. Nulling MapEventSide
                // makes MobileParty.MapEvent null and SetVisualAsDirty re-marks the figure, dropping it out of
                // the fighting animation. The full vanilla finalize is deliberately not re-run — the server
                // already replicated the battle results.

                // Snapshot MainParty's reward figures before nulling any side removes it from obj's party lists —
                // GetBattleRewardsPrefix falls back to this if this client's scoreboard reads them after teardown.
                if (localPartyWasInvolved)
                {
                    var mainPartyEventParty = obj.FindMapEventParty(PartyBase.MainParty);

                    if (mainPartyEventParty != null
                        && ContainerProvider.TryResolve<IMainPartyBattleRewardsCache>(out var rewardsCache))
                    {
                        rewardsCache.Capture(obj, mainPartyEventParty, obj.GetPlayerBattleContributionRate());
                    }
                }

                // Mark finalized first so clearing a side's last party (which re-enters FinalizeEvent via
                // RemovePartyInternal) no-ops on IsFinalized instead of depending on the State sync arriving first.
                obj.State = MapEventState.WaitingRemoval;

                foreach (var side in obj._sides)
                {
                    if (side == null) continue;

                    // Nulling MapEventSide removes the party from the side, so snapshot before iterating.
                    foreach (var mapEventParty in new List<MapEventParty>(side.Parties))
                    {
                        var party = mapEventParty?.Party;
                        if (party == null) continue;

                        party.MapEventSide = null;
                        party.SetVisualAsDirty();
                    }
                }

                // Stop the battle icon and sound. The visual is also torn down through its own registry, and
                // OnMapEventEnd is idempotent, so this is just a belt-and-suspenders stop on this client.
                obj.MapEventVisual?.OnMapEventEnd();

                // Drop the finalized event from the manager's tick list.
                Campaign.Current.MapEventManager.Tick();
            }

            Logger.Debug("[MapEvent] {Who}: OnClientDestroyed {Id}: involved={Involved} mainPartyMapEvent={Me} menu={Menu}",
                Hero.MainHero?.Name?.ToString() ?? "?",
                id,
                localPartyWasInvolved,
                MobileParty.MainParty?.MapEvent != null,
                Campaign.Current?.CurrentMenuContext?.GameMenu?.StringId ?? "<none>");
        });
    }

    public override void OnServerCreated(MapEvent obj, string id)
    {
    }

    public override void OnServerDestroyed(MapEvent obj, string id)
    {
    }
}
