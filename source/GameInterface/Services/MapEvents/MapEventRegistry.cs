using Common;
using Common.Util;
using GameInterface.Registry.Auto;
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
    private readonly IMapEventInitializationBarrier initializationBarrier;

    public override bool Debug => true;
    public MapEventRegistry(
        ILogger logger,
        IAutoRegistryFactory autoRegistryFactory,
        IObjectManager objectManager,
        IMapEventInitializationBarrier initializationBarrier)
        : base(logger, autoRegistryFactory, objectManager)
    {
        this.initializationBarrier = initializationBarrier;
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
            initializationBarrier.AdoptExisting(mapEvent);
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

        initializationBarrier.RegisterClient(obj);
    }

    public override void OnClientDestroyed(MapEvent obj, string id)
    {
        if (Campaign.Current == null) return;

        // AutoRegistry already marshals this callback to the game thread. Capturing before teardown keeps
        // the encounter fallback available while the barrier removes every external party edge atomically.
        bool localPartyWasInvolved = IsLocalPartyInMapEvent(obj);
        initializationBarrier.DestroyGraph(obj);
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
        initializationBarrier.BeginServer(obj);
    }

    public override void OnServerDestroyed(MapEvent obj, string id)
    {
        initializationBarrier.DestroyGraph(obj);
    }
}
