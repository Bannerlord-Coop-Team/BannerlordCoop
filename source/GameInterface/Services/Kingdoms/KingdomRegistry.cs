using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Kingdoms;

/// <summary>
/// Registry for <see cref="Kingdom"/> type
/// </summary>
internal class KingdomRegistry : AutoRegistryBase<Kingdom>
{
    public KingdomRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Kingdom))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var campaignObjectManager = Campaign.Current?.CampaignObjectManager;

        if (campaignObjectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var kingdom in campaignObjectManager.Kingdoms)
        {
            RegisterExistingObject(kingdom.StringId, kingdom);
        }
    }

    public override void OnClientCreated(Kingdom obj, string id)
    {
        using(new AllowedThread())
        {
            obj._activePolicies = new MBList<PolicyObject>();
            obj._armies = new MBList<Army>();
            obj.InitializeCachedLists();
            obj.EncyclopediaText = TextObject.GetEmpty();
            obj.EncyclopediaTitle = TextObject.GetEmpty();
            obj.EncyclopediaRulerTitle = TextObject.GetEmpty();
            obj._distanceToClosestNonAllyFortificationCacheDirty = true;
            obj._isEliminated = false;
            obj.NotAttackableByPlayerUntilTime = CampaignTime.Zero;
            obj.LastArmyCreationDay = (int)CampaignTime.Now.ToDays;
        }

        MBObjectManager.Instance?.RegisterPresumedObject(obj);

        Campaign.Current?.CampaignObjectManager?.AddKingdom(obj);
    }

    public override void OnClientDestroyed(Kingdom obj, string id)
    {
    }

    public override void OnServerCreated(Kingdom obj, string id)
    {
    }

    public override void OnServerDestroyed(Kingdom obj, string id)
    {
    }
}
