using Common;
using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using System.Linq;
using TaleWorlds.Core;
using System.Collections;
using System.Threading;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="Army"/> type
/// </summary>
internal class ArmyRegistry : RegistryBase<Army>
{
    private const string ArmyStringIdPrefix = "CoopArmy";
    private static int ArmyCounter = 0;

    public ArmyRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        IEnumerable<Kingdom> kingdoms = Campaign.Current?.Kingdoms ?? Enumerable.Empty<Kingdom>();

        List<Army> armies = new List<Army>();

        foreach (var kingdom in kingdoms)
        {
            armies.AddRange(kingdom.Armies);
        }

        foreach (var army in armies)
        {
            RegisterNewObject(army, out var _);
        }
    }

    protected override string GetNewId(Army obj)
    {
        return $"{ArmyStringIdPrefix}_{Interlocked.Increment(ref ArmyCounter)}";
    }
}
