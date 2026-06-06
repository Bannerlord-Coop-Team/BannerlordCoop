using Common;
using Common.Util;
using GameInterface.Registry;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Localization;

namespace GameInterface.Services.Settlements;
internal class SettlementRegistry : AutoRegistryBase<Settlement>
{
    public SettlementRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Settlement), new Type[] { typeof(TextObject), typeof(LocationComplex), typeof(PartyTemplateObject)}),
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var settlement in Settlement.All.OrderBy(obj => obj.Id))
        {
            RegisterExistingObject(settlement.StringId, settlement);
        }
    }

    public override void OnClientCreated(Settlement obj, string id)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                obj.InitSettlement();
            }
        });
    }

    public override void OnClientDestroyed(Settlement obj, string id)
    {
    }

    public override void OnServerCreated(Settlement obj, string id)
    {
    }

    public override void OnServerDestroyed(Settlement obj, string id)
    {
    }
}
