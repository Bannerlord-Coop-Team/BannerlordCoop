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
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.Clans;

/// <summary>
/// Registry class that assosiates <see cref="Clan"/> and a <see cref="string"/> id
/// </summary>
internal class ClanRegistry : AutoRegistryBase<Clan>
{
    public ClanRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory, IObjectManager objectManager)
        : base(logger, autoRegistryFactory, objectManager)
    {
    }

    public override IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Clan))
    };

    public override IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public override void RegisterAllObjects()
    {
        var mbObjectManager = Campaign.Current?.CampaignObjectManager;

        if (mbObjectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var clan in mbObjectManager.Clans)
        {
            RegisterExistingObject(clan.StringId, clan);
        }
    }

    public override void OnClientCreated(Clan obj, string id)
    {
        using (new AllowedThread())
        {
            obj.InitMembers();
        }

        MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, false, out _);

        Campaign.Current?.CampaignObjectManager?.AddClan(obj);
    }

    public override void OnClientDestroyed(Clan obj, string id)
    {
    }

    public override void OnServerCreated(Clan obj, string id)
    {
    }

    public override void OnServerDestroyed(Clan obj, string id)
    {
    }
}
