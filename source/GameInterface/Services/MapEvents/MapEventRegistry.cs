using Common;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using SandBox.GauntletUI.Map;
using Serilog;
using System;
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
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                Campaign.Current.MapEventManager.OnMapEventCreated(obj);
            }
        });
    }

    public override void OnClientDestroyed(MapEvent obj, string id)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            // The action is deferred, so the campaign can be torn down (disconnect, save-load) before it runs.
            if (Campaign.Current == null) return;

            using (new AllowedThread())
            {
                // The event's own FinalizeEventAux would short-circuit on IsFinalized before clearing anything,
                // and PartyBase.MapEventSide is not a synced member, so clear each involved party's battle state
                // here directly: nulling MapEventSide makes MobileParty.MapEvent null and SetVisualAsDirty
                // re-marks the map figure, together dropping the party out of its fighting animation. The full
                // vanilla finalize is deliberately not re-run, so the client does not re-apply battle results
                // the server already replicated.

                // Mark the event finalized first. Clearing the last party of a side re-enters
                // RemovePartyInternal, which calls FinalizeEvent; with IsFinalized already true that is a no-op
                // instead of a full client-side finalize. The synced State normally already reads this, but set
                // it explicitly so the cleanup never depends on the State sync landing before this destroy.
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
        });
    }

    public override void OnServerCreated(MapEvent obj, string id)
    {
    }

    public override void OnServerDestroyed(MapEvent obj, string id)
    {
    }
}
