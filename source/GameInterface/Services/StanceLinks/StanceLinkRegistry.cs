using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.StanceLinks;

internal class StanceLinkRegistry : AutoRegistryBase<StanceLink>
{
    public StanceLinkRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
    : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => Array.Empty<MethodBase>();

    public override IEnumerable<MethodBase> DestroyMethods => new MethodBase[]
    {
        AccessTools.Method(typeof(FactionManager), nameof(FactionManager.RemoveFactionsFromCampaignWars))
    };

    public override void RegisterAllObjects()
    {
        foreach (var stanceLink in FactionManager.Instance._stances.GetStanceLinks())
        {
            var key = StanceLinkHandler.GetStanceLinkKey(stanceLink.Faction1, stanceLink.Faction2);
            RegisterExistingObject(key, stanceLink);
        }
    }
    public override void OnClientCreated(StanceLink obj, string id)
    {
    }

    public override void OnClientDestroyed(StanceLink obj, string id)
    {
    }

    public override void OnServerCreated(StanceLink obj, string id)
    {
    }

    public override void OnServerDestroyed(StanceLink obj, string id)
    {
    }
}