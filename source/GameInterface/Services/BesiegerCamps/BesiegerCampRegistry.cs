using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace GameInterface.Services.BesiegerCamps;
internal class BesiegerCampRegistry : AutoRegistryBase<BesiegerCamp>
{
    public BesiegerCampRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(BesiegerCamp));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var siegeEvents = Campaign.Current?.SiegeEventManager?.SiegeEvents;
        if (siegeEvents == null)
        {
            Logger.Error("Unable to register BesiegerCamps because SiegeEvents are not available");
            return;
        }

        foreach (var camp in siegeEvents.Select(s => s?.BesiegerCamp).Where(c => c != null))
        {
            RegisterExistingObject(camp.SiegeEvent.BesiegedSettlement.StringId, camp);
        }
    }

    public override void OnClientCreated(BesiegerCamp obj, string id)
    {
        AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._besiegerParties)).SetValue(obj, new MBList<MobileParty>());
    }

    public override void OnClientDestroyed(BesiegerCamp obj, string id)
    {
    }

    public override void OnServerCreated(BesiegerCamp obj, string id)
    {
    }

    public override void OnServerDestroyed(BesiegerCamp obj, string id)
    {
    }
}
