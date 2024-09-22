using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Services.BesiegerCamps;
internal class BeseigerCampRegistry : RegistryBase<BesiegerCamp>
{
    public BeseigerCampRegistry(IRegistryCollection collection) : base(collection) { }

    public override void RegisterAll()
    {

    }

    protected override string GetNewId(BesiegerCamp obj)
    {
        return Guid.NewGuid().ToString();
    }
}
