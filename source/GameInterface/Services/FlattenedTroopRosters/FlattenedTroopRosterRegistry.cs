using GameInterface.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.FlattenedTroopRosters;

/// <summary>
/// Registry for <see cref="FlattenedTroopRoster"/> type
/// </summary>
internal class FlattenedTroopRosterRegistry : RegistryBase<FlattenedTroopRoster>
{
    private const string FlattenedIdPrefix = "CoopFlattened";
    private static int InstanceCounter = 0;

    public FlattenedTroopRosterRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {

    }

    protected override string GetNewId(FlattenedTroopRoster obj)
    {
        return $"{FlattenedIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
