using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.BesiegerCamps;

// Not sure where this goes..
internal class SiegeStrategyRegistry : RegistryBase<SiegeStrategy>
{
    public SiegeStrategyRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {
        foreach (var strategy in SiegeStrategy.All)
        {
            RegisterNewObject(strategy, out var _);
        }

    }

    protected override string GetNewId(SiegeStrategy obj)
    {
        return Guid.NewGuid().ToString();
    }
}
