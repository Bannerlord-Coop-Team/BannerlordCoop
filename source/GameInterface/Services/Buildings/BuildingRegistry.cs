using GameInterface.Services.Registry;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="Army"/> type
/// </summary>
internal class BuildingRegistry : RegistryBase<Building>
{
    public BuildingRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach(Settlement settlement in Campaign.Current.Settlements)
        {
            if(settlement.Town == null) continue;

            foreach(Building building in settlement.Town.Buildings)
            {
                RegisterNewObject(building, out var _);
            }
        }
    }

    protected override string GetNewId(Building obj)
    {
        return Guid.NewGuid().ToString();
    }
}
