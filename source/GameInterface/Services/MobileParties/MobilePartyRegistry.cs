using Common;
using Common.Util;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using GameInterface.Services.ItemRosters;

namespace GameInterface.Services.MobileParties;

/// <summary>
/// Registry for <see cref="MobileParty"/> objects
/// </summary>
internal class MobilePartyRegistry : IAutoRegistry<MobileParty>
{
    ILogger Logger { get; }
    public MobilePartyRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => new MethodBase[] {
        AccessTools.Constructor(typeof(MobileParty))
    };

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<MobileParty> registry)
    {
        foreach (var party in MobileParty.All)
        {
            if (registry.TryGetValue<MobileParty>(party.StringId, out _)) continue;
            registry.RegisterExistingObject(party.StringId, party);
            if (party.Party != null && party.ItemRoster != null)
            {
                ItemRosterLookup.Set(party.ItemRoster, party.Party);
            }
        }
    }

    public void OnClientCreated(MobileParty obj, string id)
    {
        using (new AllowedThread())
        {
            obj.InitMembers();
            obj.Initialize();
        }

        MBObjectManager.Instance?.RegisterObjectInternalWithoutTypeId(obj, false, out _);

        Campaign.Current?.CampaignObjectManager?.AddMobileParty(obj);

        if (obj.Party != null && obj.ItemRoster != null)
        {
            ItemRosterLookup.Set(obj.ItemRoster, obj.Party);
        }
    }

    public void OnClientDestroyed(MobileParty obj, string id)
    {
    }

    public void OnServerCreated(MobileParty obj, string id)
    {
    }

    public void OnServerDestroyed(MobileParty obj, string id)
    {
    }
}
