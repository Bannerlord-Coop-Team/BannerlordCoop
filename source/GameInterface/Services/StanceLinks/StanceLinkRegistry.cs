using Common;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using System.Linq;
using TaleWorlds.Core;
using System.Collections;
using System.Threading;
using GameInterface.Registry;
using Common.Util;
using GameInterface.Registry.Auto;
using HarmonyLib;
using Serilog;
using System.Reflection;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.StanceLinks;

/// <summary>
/// Registry for <see cref="StanceLink"/> type
/// </summary>
internal class StanceLinkRegistry : IAutoRegistry<StanceLink>
{
    ILogger Logger { get; }
    public StanceLinkRegistry(ILogger logger, IAutoRegistryFactory autoRegistryFactory)
    {
        Logger = logger;

        autoRegistryFactory.RegisterType(this);
    }

    public IEnumerable<MethodBase> Constructors => AccessTools.GetDeclaredConstructors(typeof(StanceLink));

    public IEnumerable<MethodBase> DestroyMethods => Array.Empty<MethodBase>();

    public void RegisterAllObjects(IRegistry<StanceLink> registry)
    {
        IEnumerable<IFaction> kingdoms = Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>();
        IEnumerable<IFaction> clans = Campaign.Current?.Clans ?? Enumerable.Empty<Clan>();

        var factions = kingdoms.Concat(clans);

        HashSet<StanceLink> visitedStances = new();

        foreach (var faction in factions)
        {
            int counter = 1;
            foreach (var stance in faction.Stances)
            {
                if (visitedStances.Contains(stance)) continue;

                var networkId = $"{nameof(StanceLink)}_{faction.StringId}_{counter++}";
                registry.RegisterExistingObject(networkId, stance);

                visitedStances.Add(stance);
            }
        }
    }

    public void OnClientCreated(StanceLink obj, string id)
    {
    }

    public void OnClientDestroyed(StanceLink obj, string id)
    {
    }

    public void OnServerCreated(StanceLink obj, string id)
    {
    }

    public void OnServerDestroyed(StanceLink obj, string id)
    {
    }
}
