using Common;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.Core;

namespace GameInterface.Services.SiegeEvents;
internal class CraftingOrderRegistry : AutoRegistryBase<CraftingOrder>
{
    public CraftingOrderRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(CraftingOrder));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var townCraftingOrders in Campaign.Current.GetCampaignBehavior<CraftingCampaignBehavior>()._craftingOrders)
        {
            foreach (var craftingOrder in townCraftingOrders.Value.Slots)
            {
                if (objectManager.AddNewObject(craftingOrder, out var _) == false)
                {
                    Logger.Error($"Unable to register {nameof(CraftingOrder)}");
                }
            }
        }
    }

    public override void OnClientCreated(CraftingOrder obj, string id)
    {
    }

    public override void OnClientDestroyed(CraftingOrder obj, string id)
    {
    }

    public override void OnServerCreated(CraftingOrder obj, string id)
    {
    }

    public override void OnServerDestroyed(CraftingOrder obj, string id)
    {
    }
}
