using Common;
using GameInterface.Registry;
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
using TaleWorlds.Library;

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

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var siegeEvents = Campaign.Current?.SiegeEventManager?.SiegeEvents;
        if (siegeEvents == null)
        {
            Logger.Error("Unable to register {Type} when SiegeEvents is null", nameof(SiegeEvent));
            return;
        }

        foreach (var instance in siegeEvents)
        {
            if (instance == null) continue;

            var settlementId = instance.BesiegedSettlement?.StringId;
            if (settlementId == null) continue;

            RegisterExistingObject(settlementId, instance);
        }
    }

    public override void OnClientCreated(SiegeEvent obj, string id)
    {

    }

    public override void OnClientDestroyed(SiegeEvent obj, string id)
    {
    }

    public override void OnServerCreated(SiegeEvent obj, string id)
    {
    }

    public override void OnServerDestroyed(SiegeEvent obj, string id)
    {
    }
}
