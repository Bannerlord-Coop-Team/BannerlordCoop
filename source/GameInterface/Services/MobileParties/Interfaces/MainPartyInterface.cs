using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces
{
    internal class MainPartyInterface : IMainPartyInterface
    {
        public void RemoveMainParty()
        {
            MobileParty.MainParty?.RemoveParty();
        }
    }
}
