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
internal class CraftingCampaignBehaviorRegistry : IAutoRegistry<CraftingCampaignBehavior>
{
    ILogger Logger { get; }
    public CraftingCampaignBehaviorRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(CraftingCampaignBehavior))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        var craftingCampaignBehavior = Campaign.Current.CampaignBehaviorManager.GetBehavior<CraftingCampaignBehavior>();
        objectManager.AddExisting(craftingCampaignBehavior.StringId, craftingCampaignBehavior);
    }

    public void OnClientCreated(CraftingCampaignBehavior obj, string id)
    {
    }

    public void OnClientDestroyed(CraftingCampaignBehavior obj, string id)
    {
    }

    public void OnServerCreated(CraftingCampaignBehavior obj, string id)
    {
    }

    public void OnServerDestroyed(CraftingCampaignBehavior obj, string id)
    {
    }
}
