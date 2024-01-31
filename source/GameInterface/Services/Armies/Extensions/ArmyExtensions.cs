using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Armies.Extensions
{
    internal static class ArmyExtensions
    {
        private static Action<Army, MobileParty> Army_OnAddPartyInternal = typeof(Army).GetMethod("OnAddPartyInternal", BindingFlags.NonPublic | BindingFlags.Instance)
            .BuildDelegate<Action<Army, MobileParty>>();
        internal static void OnAddPartyInternal(this MobileParty mobileParty, Army army)
        {
            Army_OnAddPartyInternal(army,mobileParty);
        }
    }
}