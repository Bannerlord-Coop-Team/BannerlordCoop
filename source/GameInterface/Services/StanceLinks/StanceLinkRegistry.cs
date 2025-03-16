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

namespace GameInterface.Services.StanceLinks;

/// <summary>
/// Registry for <see cref="StanceLink"/> type
/// </summary>
internal class StanceLinkRegistry : RegistryBase<StanceLink>
{
    private const string StanceLinkStringIdPrefix = "CoopStanceLink";
    private static int InstaceCounter = 0;

    public StanceLinkRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        IEnumerable<IFaction> kingdoms = Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>();
        IEnumerable<IFaction> clans = Campaign.Current?.Clans ?? Enumerable.Empty<Clan>();

        var factions = kingdoms.Concat(clans);

        HashSet<StanceLink> visitedStances = new();

        foreach (var faction in factions)
        {
            int counter = 1;
            foreach(var stance in faction.Stances)
            {
                if (visitedStances.Contains(stance)) continue;

                var networkId = $"{nameof(StanceLink)}_{faction.StringId}_{counter++}";
                RegisterExistingObject(networkId, stance);

                visitedStances.Add(stance);
            }
        }
    }

    protected override string GetNewId(StanceLink obj)
    {
        return $"{StanceLinkStringIdPrefix}_{Interlocked.Increment(ref InstaceCounter)}";
    }
}
