using GameInterface.Services.MobileParties.Extensions;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Extentions
{
    internal static class CampaignObjectManagerExtentions
    {
        public static List<MobileParty> GetPlayerMobileParties(this CampaignObjectManager campaignObjectManager)
        {
            return campaignObjectManager._mobileParties.Where(party => party.IsPlayerParty()).ToList();
        }
    }
}
