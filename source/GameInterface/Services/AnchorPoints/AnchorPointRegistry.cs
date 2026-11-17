using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;

namespace GameInterface.Services.AnchorPoints;

internal class AnchorPointRegistry : AutoRegistryBase<AnchorPoint>
{
    public AnchorPointRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(AnchorPoint));

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        foreach (var mobileParty in Campaign.Current.CampaignObjectManager.MobileParties)
        {
            RegisterExistingObject(mobileParty.StringId, mobileParty.Anchor);
        }
    }

    public override void OnClientCreated(AnchorPoint obj, string id)
    {
    }

    public override void OnClientDestroyed(AnchorPoint obj, string id)
    {
    }

    public override void OnServerCreated(AnchorPoint obj, string id)
    {
    }

    public override void OnServerDestroyed(AnchorPoint obj, string id)
    {
    }
}
