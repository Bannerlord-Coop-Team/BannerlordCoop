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

namespace GameInterface.Services.Smithing;
internal class CraftingCampaignBehaviorRegistry : AutoRegistryBase<CraftingCampaignBehavior>
{
    public CraftingCampaignBehaviorRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(CraftingCampaignBehavior))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var craftingCampaignBehavior = Campaign.Current.CampaignBehaviorManager.GetBehavior<CraftingCampaignBehavior>();
        objectManager.AddExisting(craftingCampaignBehavior.StringId, craftingCampaignBehavior);
    }

    public override void OnClientCreated(CraftingCampaignBehavior obj, string id)
    {
    }

    public override void OnClientDestroyed(CraftingCampaignBehavior obj, string id)
    {
    }

    public override void OnServerCreated(CraftingCampaignBehavior obj, string id)
    {
    }

    public override void OnServerDestroyed(CraftingCampaignBehavior obj, string id)
    {
    }
}
