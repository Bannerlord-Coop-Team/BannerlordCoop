using Common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces
{
    internal interface IMainPartyInterface : IGameAbstraction
    {
        void RemoveMainParty();
    }

    internal class MainPartyInterface : IMainPartyInterface
    {
        public void RemoveMainParty()
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                if(MobileParty.MainParty != null)
                {
                    // TODO see if remove works again
                    MobileParty.MainParty.IsActive = false;
                    MobileParty.MainParty.IsVisible = false;
                }
                
            }, bBlocking: false);
        }
    }
}
