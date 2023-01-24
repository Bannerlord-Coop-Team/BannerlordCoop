using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces
{
<<<<<<< HEAD
    internal interface IMainPartyInterface : IGameAbstraction
    {
        void RemoveMainParty();
    }

=======
>>>>>>> NetworkEvent-refactor
    internal class MainPartyInterface : IMainPartyInterface
    {
        public void RemoveMainParty()
        {
            MobileParty.MainParty?.RemoveParty();
        }
    }
}
