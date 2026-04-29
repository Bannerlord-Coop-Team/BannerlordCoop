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
internal class ClanRegistry : IAutoRegistry<Clan>
{
    ILogger Logger { get; }
    public ClanRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(Clan))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IObjectManager objectManager)
    {
        var mbObjectManager = Campaign.Current?.CampaignObjectManager;

        if (mbObjectManager == null)
        {
            Logger.Error("Unable to register objects when CampaignObjectManager is null");
            return;
        }

        foreach (var clan in mbObjectManager.Clans)
        {
            objectManager.AddExisting(clan.StringId, clan);
        }
    }

    public void OnClientCreated(Clan obj, string id)
    {
        using (new AllowedThread())
        {
            obj.InitMembers();
        }

        MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, false, out _);

        Campaign.Current?.CampaignObjectManager?.AddClan(obj);
    }

    public void OnClientDestroyed(Clan obj, string id)
    {
    }

    public void OnServerCreated(Clan obj, string id)
    {
    }

    public void OnServerDestroyed(Clan obj, string id)
    {
    }
}
