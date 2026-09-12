using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;

using GameInterface.Services.SiegeEngines;
namespace GameInterface.Services.SiegeEvents;
internal class SiegeEventRegistry : AutoRegistryBase<SiegeEvent>
{
    public SiegeEventRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(SiegeEvent), new Type[] { typeof(Settlement), typeof(MobileParty) })
    };

    // FinalizeSiegeEvent is the funnel every siege-end path calls (last besieger leaving, AI think,
    // faction discontinuation, blockade end, Settlement.AfterLoad), so pinning it replicates every
    // teardown. Settlement.FinalizeSiegeEvent is a different per-side method called by this funnel.
    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(SiegeEvent), nameof(SiegeEvent.FinalizeSiegeEvent))
    };

    public override void RegisterAllObjects()
    {
        foreach (var siegeEvent in SiegeContainerLookup.ActiveSieges())
        {
            RegisterExistingObject(siegeEvent.BesiegedSettlement.StringId, siegeEvent);
        }
    }

    public override void OnClientCreated(SiegeEvent obj, string id)
    {
        // Vanilla only adds to the manager list in SiegeEventManager.StartSiegeEvent, which never runs
        // on the receive path. This callback runs on the network thread and the list is walked by the
        // main-thread campaign tick, so defer the add like MapEventRegistry does.
        GameThread.RunSafe(() =>
        {
            if (Campaign.Current?.SiegeEventManager == null) return;

            using (new AllowedThread())
            {
                Campaign.Current.SiegeEventManager._siegeEvents.Add(obj);
            }
        });
    }

    public override void OnClientDestroyed(SiegeEvent obj, string id)
    {
        GameThread.RunSafe(() =>
        {
            if (Campaign.Current?.SiegeEventManager == null) return;

            using (new AllowedThread())
            {
                Campaign.Current.SiegeEventManager._siegeEvents.Remove(obj);
                // Settlement.SiegeEvent is nulled by its own synced setter; the visual refresh here
                // covers the case where that set applied before this teardown ran.
                obj.BesiegedSettlement?.Party?.SetVisualAsDirty();
            }

            ReleaseChildObjects(obj);
        });
    }

    public override void OnServerCreated(SiegeEvent obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeEvent obj, string id)
    {
        ReleaseChildObjects(obj);
    }

    // The camp has its own destroy pin, but the containers and construction progresses have no
    // destroy funnel of their own — without this every finished siege leaves them registered for the
    // rest of the session. Best effort: references the funnel already cleared just stay unregistered
    // until session end.
    private void ReleaseChildObjects(SiegeEvent obj)
    {
        ReleaseSideObjects(obj.BesiegerCamp?.SiegeEngines);
        ReleaseSideObjects(obj.BesiegedSettlement?.SiegeEngines);
    }

    private void ReleaseSideObjects(SiegeEvent.SiegeEnginesContainer container)
    {
        if (container == null) return;

        foreach (var engine in container.AllSiegeEngines())
        {
            objectManager.Remove(engine);
        }

        foreach (var removed in container.RemovedSiegeEngines)
        {
            if (removed?.SiegeEngine != null) objectManager.Remove(removed.SiegeEngine);
        }

        objectManager.Remove(container);
    }
}
