using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents.Messages;
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

    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(MapEvent), nameof(MapEvent.FinishBattle)),

        // Abandoning a not-yet-fought encounter (e.g. the attacker leaves) tears the event down through
        // FinalizeEvent, not FinishBattle. Track it too so the destroy replicates to every client; otherwise a
        // party that joined the event (but did not initiate the leave) is left referencing a finalized "ghost"
        // map event and stays locked in its encounter menu. FinishBattle goes straight to FinalizeEventAux without
        // calling FinalizeEvent, so normal battle ends still destroy exactly once.
        AccessTools.Method(typeof(MapEvent), nameof(MapEvent.FinalizeEvent)),
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

            using (new AllowedThread())
            {
                obj.Component?.FinishComponent();
                obj.FinishBattle();

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
