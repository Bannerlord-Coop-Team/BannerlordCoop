using Common;
using Common.Util;
using GameInterface.Registry.Auto;
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
internal class KingdomRegistry : IAutoRegistry<Kingdom>
{
    ILogger Logger { get; }
    public KingdomRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Kingdom))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<Kingdom> registry)
    {
        var objectManager = Campaign.Current?.CampaignObjectManager;

        if (objectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var kingdom in objectManager.Kingdoms)
        {
            registry.RegisterExistingObject(kingdom.StringId, kingdom);
        }
    }

    public void OnClientCreated(Kingdom obj, string id)
    {
        using(new AllowedThread())
        {
            obj._activePolicies = new MBList<PolicyObject>();
            obj._armies = new MBList<Army>();
            obj.InitializeCachedLists();
            obj.EncyclopediaText = TextObject.Empty;
            obj.EncyclopediaTitle = TextObject.Empty;
            obj.EncyclopediaRulerTitle = TextObject.Empty;
            obj._midPointCalculated = false;
            obj._distanceToClosestNonAllyFortificationCacheDirty = true;
            obj._isEliminated = false;
            obj.NotAttackableByPlayerUntilTime = CampaignTime.Zero;
            obj.LastArmyCreationDay = (int)CampaignTime.Now.ToDays;
        }

        MBObjectManager.Instance?.RegisterPresumedObject(obj);

        Campaign.Current?.CampaignObjectManager?.AddKingdom(obj);
    }

    public void OnClientDestroyed(Kingdom obj, string id)
    {
    }

    public void OnServerCreated(Kingdom obj, string id)
    {
    }

    public void OnServerDestroyed(Kingdom obj, string id)
    {
    }
}
