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
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Armies;

/// <summary>
/// Registry for <see cref="Army"/> type
/// </summary>
internal class BuildingRegistry : RegistryBase<Building>
{
    private const string BuildingStringIdPrefix = "CoopBuilding";
    private static int BuildingCounter = 0;

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
        return $"{BuildingStringIdPrefix}_{Interlocked.Increment(ref BuildingCounter)}";
    }
}
