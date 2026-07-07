using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

using GameInterface.Services.SiegeEngines;
namespace GameInterface.Services.BesiegerCamps;
internal class BesiegerCampRegistry : AutoRegistryBase<BesiegerCamp>
{
    public BesiegerCampRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(BesiegerCamp));

    // Called by SiegeEvent.FinalizeSiegeEvent on every siege-end path, so the camp id is released
    // together with its siege event instead of leaking until the settlement's next siege.
    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.FinalizeSiegeEvent))
    };

    public override void RegisterAllObjects()
    {
        foreach (var siegeEvent in SiegeContainerLookup.ActiveSieges())
        {
            var camp = siegeEvent.BesiegerCamp;
            if (camp == null) continue;

            RegisterExistingObject(siegeEvent.BesiegedSettlement.StringId, camp);
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
