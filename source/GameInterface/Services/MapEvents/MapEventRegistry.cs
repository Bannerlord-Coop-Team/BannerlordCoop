using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.GuantletMapEventVisuals;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// Registry for <see cref="MapEvent"/> objects
/// </summary>
internal class MapEventRegistry : AutoRegistryBase<MapEvent>
{
    private static readonly FieldInfo MapEventsField =
        AccessTools.Field(typeof(MapEventManager), "_mapEvents");

    private readonly IMapEventInitializationTracker initializationTracker;
    private readonly IMapEventBattleSizeCorrection mapEventBattleSizeCorrection;

    public override bool Debug => true;
    public MapEventRegistry(
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager,
        IMapEventInitializationTracker initializationTracker,
        IMapEventBattleSizeCorrection mapEventBattleSizeCorrection)
        : base(logger, autoRegistryFactory, objectManager)
    {
        this.initializationTracker = initializationTracker;
        this.mapEventBattleSizeCorrection = mapEventBattleSizeCorrection;
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

            // Save-loaded and late-join graphs bypass aggregate hydration, but root teardown must still
            // own every nested registration exactly as it does for a freshly initialized aggregate.
            initializationTracker.RegisterCommittedGraph(mapEvent, MapEventGraph.Enumerate(mapEvent));
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

        // The aggregate initializer adds the event to MapEventManager only after its complete graph is wired.
    }

    public override void OnClientDestroyed(MapEvent obj, string id)
    {
        // Root destruction owns every aggregate-only nested registration. Remove the complete id graph
        // before later queued packets can resolve a component, party, tracker or casualty roster whose
        // root is already gone.
        initializationTracker.DestroyGraph(obj);

        // AutoRegistryHandler already invokes this callback inside its ordered game-thread action. Do
        // not enqueue another action: a following aggregate may legitimately attach the same PartyBase
        // to a new event, and delayed cleanup of this old root must never detach that newer edge.
        if (Campaign.Current == null)
        {
            return;
        }

        // Captured before FinishBattle clears it; used as a last-resort client close when the server close
        // message fails to unwind the native encounter menu before the event disappears underneath it.
        bool localPartyWasInvolved = IsLocalPartyInMapEvent(obj);

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

                // Nulling MapEventSide removes the party from the side, so snapshot before iterating. Only
                // detach the edge when it still belongs to this exact graph; a newer aggregate wins.
                foreach (var mapEventParty in new List<MapEventParty>(side.Parties))
                {
                    var party = mapEventParty?.Party;
                    if (party == null || !ReferenceEquals(party.MapEventSide, side)) continue;

                    party.MapEventSide = null;
                    party.SetVisualAsDirty();
                }
            }

            // Stop the battle icon and sound. The shared lifecycle marker prevents this root fallback
            // and the visual's own destroy packet from notifying UI handlers twice in either order.
            var visual = obj.MapEventVisual;
            GauntletMapEventVisualLifecycle.TryEnd(visual);
            if (visual is SandBox.GauntletUI.Map.GauntletMapEventVisual gauntletVisual)
                mapEventBattleSizeCorrection.Clear(gauntletVisual);

            // Drop only this finalized root. Calling MapEventManager.Tick here would also Update every
            // unrelated raid/non-player event and accidentally run a client simulation step.
            if (MapEventsField.GetValue(Campaign.Current.MapEventManager) is MBList<MapEvent> mapEvents)
                mapEvents.Remove(obj);
        }

        CloseDestroyedMapEventEncounterIfNeeded(id, localPartyWasInvolved);
    }

    private bool IsLocalPartyInMapEvent(MapEvent mapEvent)
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null || mapEvent == null)
            return false;

        if (mainParty.MapEvent == mapEvent)
            return true;

        var encounter = PlayerEncounter.Current;
        var battle = GetPlayerEncounterBattle();
        var encounteredBattle = GetPlayerEncounterEncounteredBattle();
        if (encounter?._mapEvent == mapEvent || battle == mapEvent || encounteredBattle == mapEvent)
            return true;

        var party = mainParty.Party;
        foreach (var side in mapEvent._sides ?? new MapEventSide[0])
        {
            if (side?.Parties == null)
                continue;

            foreach (var mapEventParty in side.Parties)
            {
                if (mapEventParty?.Party == party)
                    return true;
            }
        }

        return false;
    }

    private void CloseDestroyedMapEventEncounterIfNeeded(string mapEventId, bool localPartyWasInvolved)
    {
        if (!localPartyWasInvolved)
        {
            return;
        }

        if (MissionState.Current != null)
        {
            return;
        }

        if (PlayerCaptivity.IsCaptive)
        {
            return;
        }

        if (!HasEncounterMenuToClose())
        {
            return;
        }

        if (MobileParty.MainParty?.Party?.MapEventSide != null)
            MobileParty.MainParty.Party._mapEventSide = null;

        if (PlayerEncounter.Current != null)
        {
            PlayerEncounter.LeaveEncounter = true;
            try
            {
                PlayerEncounter.Finish(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "[PvPEncounterClose] MapEvent destroy fallback PlayerEncounter.Finish failed; forcing PlayerEncounter null anyway. mapEventId={MapEventId}", mapEventId);
            }
            finally
            {
                Campaign.Current.PlayerEncounter = null;
            }
        }

        ForceCloseCurrentEncounterMenu();
    }

    private static bool HasEncounterMenuToClose()
    {
        var mapState = Game.Current?.GameStateManager?.ActiveState as MapState;
        return PlayerEncounter.Current != null ||
               Campaign.Current?.CurrentMenuContext != null ||
               mapState?.AtMenu == true;
    }

    private static MapEvent GetPlayerEncounterBattle()
    {
        try
        {
            return PlayerEncounter.Battle;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private static MapEvent GetPlayerEncounterEncounteredBattle()
    {
        try
        {
            return PlayerEncounter.EncounteredBattle;
        }
        catch (NullReferenceException)
        {
            return null;
        }
    }

    private void ForceCloseCurrentEncounterMenu()
    {
        var campaign = Campaign.Current;
        var mapState = Game.Current?.GameStateManager?.ActiveState as MapState;
        var menuContext = campaign?.CurrentMenuContext;

        try
        {
            GameMenu.ExitToLast();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[PvPEncounterClose] Failed to exit current menu during forced close");
        }

        try
        {
            menuContext?.Destroy();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "[PvPEncounterClose] Failed to destroy current menu context during forced close");
        }

        ExitMapMenuMode(campaign, mapState);

        if (campaign?.MapStateData != null)
            campaign.MapStateData.GameMenuId = null;
    }

    private static void ExitMapMenuMode(Campaign campaign, MapState mapState)
    {
        if (mapState?.AtMenu == true)
            mapState.ExitMenuMode();

        if (mapState != null)
            mapState.GameMenuId = null;
        if (campaign?.MapStateData != null)
            campaign.MapStateData.GameMenuId = null;
    }

    public override void OnServerCreated(MapEvent obj, string id)
    {
    }

    public override void OnServerDestroyed(MapEvent obj, string id)
    {
        initializationTracker.DestroyGraph(obj);
    }
}
