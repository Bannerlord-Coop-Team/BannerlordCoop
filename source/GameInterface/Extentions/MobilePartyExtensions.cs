using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Extentions
{
    public static class MobilePartyExtensions
    {
        private static readonly FieldInfo MobileParty_partyComponent = typeof(MobileParty).GetField("_partyComponent", BindingFlags.NonPublic | BindingFlags.Instance);
        internal static void SetPartyComponent(this MobileParty party, PartyComponent component)
        {
            MobileParty_partyComponent.SetValue(party, component);
        }
    }
}
