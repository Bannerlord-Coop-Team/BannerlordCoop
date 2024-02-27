using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Extentions
{
    public static class SettlementComponentExtensions
    {
        static PropertyInfo GoldProperty = typeof(SettlementComponent).GetProperty(nameof(SettlementComponent.Gold));
        public static void SetGold(this SettlementComponent obj, int value) => GoldProperty.SetValue(obj, value);
        static PropertyInfo OwnerProperty = typeof(SettlementComponent).GetProperty(nameof(SettlementComponent.Owner));
        public static void SetOwner(this SettlementComponent component, PartyBase value) => OwnerProperty.SetValue(component, value);

    }
}
