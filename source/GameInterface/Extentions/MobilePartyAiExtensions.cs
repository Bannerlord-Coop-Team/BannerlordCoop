using Common.Extensions;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Extentions
{
    public static class MobilePartyAiExtensions
    {
        private static readonly Func<MobilePartyAi, MobileParty> MobilePartyAi_mobileParty = typeof(MobilePartyAi)
            .GetField("_mobileParty", BindingFlags.NonPublic | BindingFlags.Instance)
            .BuildUntypedGetter<MobilePartyAi, MobileParty>();

        public static MobileParty GetMobileParty(this MobilePartyAi ai)
        {
            return MobilePartyAi_mobileParty(ai);
        }
    }
}
