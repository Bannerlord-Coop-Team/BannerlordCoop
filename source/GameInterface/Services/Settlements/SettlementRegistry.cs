using Common;
using GameInterface.Services.Registry;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements;

/// <summary>
/// 
/// </summary>
internal interface ISettlementRegistry : IRegistry<Settlement>
{
    void AddSettlement(Settlement settlement);
    void ContainsSettlement(Settlement settlement);
    void ContainsVillage(Village village);
    void RegisterAllSettlements();

}

internal class SettlementRegistry : RegistryBase<Settlement>, ISettlementRegistry
{
    public void AddSettlement(Settlement settlement)
    {
        throw new NotImplementedException();
    }

    public void ContainsSettlement(Settlement settlement)
    {
        throw new NotImplementedException();
    }

    public void ContainsVillage(Village village)
    {
        throw new NotImplementedException();
    }

    public void GetSettlement(Settlement settlement)
    {
        throw new NotImplementedException();
    }

    public void RegisterAllSettlements()
    {
        throw new NotImplementedException();
    }

    public override bool RegisterNewObject(Settlement obj, out string id)
    {
        throw new NotImplementedException();
    }
}
