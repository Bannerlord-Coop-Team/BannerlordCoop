using GameInterface.Services.Registry;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="Army"/> type
/// </summary>
internal class BuildingRegistry : RegistryBase<Building>
{
    private const string BuildingIdPrefix = "CoopBuilding";
    private static int InstanceCounter = 0;

    public BuildingRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach(Settlement settlement in Campaign.Current.Settlements)
        {
            if(settlement.Town == null) continue;

            foreach(Building building in settlement.Town.Buildings)
            {
                if (RegisterNewObject(building, out var _) == false)
                {
                    Logger.Error($"Unable to register {building}");
                }
            }
        }
    }

    protected override string GetNewId(Building obj)
    {
        return $"{BuildingIdPrefix}_{Interlocked.Increment(ref InstanceCounter)}";
    }
}
