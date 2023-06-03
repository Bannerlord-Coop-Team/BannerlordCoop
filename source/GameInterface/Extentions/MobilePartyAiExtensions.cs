using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Extentions
{
    public static class MobilePartyAiExtensions
    {
        private static readonly FieldInfo MobilePartyAi_mobileParty = typeof(MobilePartyAi).GetField("_mobileParty", BindingFlags.NonPublic | BindingFlags.Instance);

        public static MobileParty GetMobileParty(this MobilePartyAi ai)
        {
            return MobilePartyAi_mobileParty.GetValue(ai) as MobileParty;
        }
    }
}
