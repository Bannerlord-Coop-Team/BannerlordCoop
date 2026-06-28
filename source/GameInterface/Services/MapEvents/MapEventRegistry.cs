using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
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

    // Map event sides, parties, components, visuals, and troop trackers derive their ids from the
    // owning map event. Register map events first so those registries never observe an unregistered parent.
    public override int RegistrationPriority => 100;

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
        RegisterMapEvents(Campaign.Current.MapEventManager.MapEvents);
    }

    /// <summary>
    /// Registers map events that existed before co-op lifetime patches were enabled. Vanilla map events do not
    /// assign <see cref="MapEvent.StringId"/> in their constructor, so the registry owns assigning a persistent
    /// network identity when an event first enters the co-op object graph. Runtime events use the same
    /// <c>Created_N</c> identity path through <see cref="AutoRegistryHandler{T}"/>.
    /// </summary>
    internal void RegisterMapEvents(IEnumerable<MapEvent> mapEvents)
    {
        if (mapEvents == null) return;

        var snapshot = mapEvents.Where(mapEvent => mapEvent != null).ToList();

        // Register every persisted identity first. This seeds the MapEvent counter above all existing
        // numeric suffixes before a new identity is allocated, preventing a Created_N collision.
        foreach (var mapEvent in snapshot)
        {
            if (string.IsNullOrEmpty(mapEvent.StringId)) continue;

            RegisterMapEvent(mapEvent.StringId, mapEvent);
        }

        foreach (var mapEvent in snapshot)
        {
            if (!string.IsNullOrEmpty(mapEvent.StringId)) continue;

            string id = $"Created_{objectManager.GetUniqueTypeId(mapEvent)}";
            mapEvent.StringId = id;

            if (!string.Equals(mapEvent.StringId, id, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Failed to assign network id {id} to MapEvent");
            }

            RegisterMapEvent(id, mapEvent);
            Logger.Information("Assigned network id {MapEventId} to preexisting MapEvent", id);
        }
    }

    private void RegisterMapEvent(string id, MapEvent mapEvent)
    {
        if (!RegisterExistingObject(id, mapEvent))
        {
            throw new InvalidOperationException($"Failed to register MapEvent with network id {id}");
        }
    }

    internal static string GetNetworkId(MapEvent mapEvent)
    {
        if (string.IsNullOrEmpty(mapEvent.StringId))
        {
            throw new InvalidOperationException(
                "MapEvent must be registered before registering its dependent objects");
        }

        return mapEvent.StringId;
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
        GameThread.Run(() =>
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
            // Captured before FinishBattle clears it; kept for the debug log only — the PvP encounter close is now
            // driven server-side via NetworkClosePvpEncounter, not from this teardown.
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
