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
        IEnumerable<Kingdom> kingdoms = Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>();

        foreach (var kingdom in kingdoms)
        {
            foreach(StanceLink stance in kingdom._stances)
            {
                RegisterNewObject(stance, out var _);
            }
        }

        IEnumerable<Clan> clans = Campaign.Current?.Clans ?? Enumerable.Empty<Clan>();

        foreach (var clan in clans)
        {
            foreach (StanceLink stance in clan._stances)
            {
                RegisterNewObject(stance, out var _);
            }
        }
    }

    protected override string GetNewId(StanceLink obj)
    {
        return $"{StanceLinkStringIdPrefix}_{Interlocked.Increment(ref InstaceCounter)}";
    }
}
