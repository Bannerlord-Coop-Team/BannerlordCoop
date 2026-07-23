using Common.Messaging;
using Common.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
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
            EnsureRuntimeCollections(obj);
            obj._distanceToClosestNonAllyFortificationCacheDirty = true;
            obj._isEliminated = false;
            obj.NotAttackableByPlayerUntilTime = CampaignTime.Zero;
        }

        MBObjectManager.Instance?.RegisterPresumedObject(obj);

        Campaign.Current?.CampaignObjectManager?.AddKingdom(obj);
    }

    public override void OnClientDestroyed(Kingdom obj, string id)
    {
    }

    internal static void EnsureRuntimeCollections(Kingdom obj)
    {
        if (obj == null) return;

        // Network-created kingdoms skip the native constructor, but the kingdom management UI
        // enumerates these collections as soon as the state opens. Keep existing collections intact
        // because sync may already have populated them before this safety pass runs.
        obj._activePolicies ??= new MBList<PolicyObject>();
        obj._armies ??= new MBList<Army>();
        obj._clans ??= new MBList<Clan>();
        obj._unresolvedDecisions ??= new MBList<KingdomDecision>();
        obj._factionsAtWarWith ??= new MBList<IFaction>();
        obj._alliedKingdoms ??= new MBList<Kingdom>();
        obj._fiefsCache ??= new MBList<Town>();
        obj._townsCache ??= new MBList<Town>();
        obj._settlementsCache ??= new MBList<Settlement>();
        obj._villagesCache ??= new MBList<Village>();
        obj._heroesCache ??= new MBList<Hero>();
        obj._aliveLordsCache ??= new MBList<Hero>();
        obj._deadLordsCache ??= new MBList<Hero>();
        obj._warPartyComponentsCache ??= new MBList<WarPartyComponent>();
        obj.EncyclopediaText ??= TextObject.GetEmpty();
        obj.EncyclopediaTitle ??= TextObject.GetEmpty();
        obj.EncyclopediaRulerTitle ??= TextObject.GetEmpty();
    }

    public override void OnServerCreated(Kingdom obj, string id)
    {
    }

    public override void OnServerDestroyed(Kingdom obj, string id)
    {
        MessageBroker.Instance.Publish(this, new CaravansKingdomDestroyed(obj));
    }
}
