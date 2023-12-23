using Common;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces;

internal interface IMainPartyInterface : IGameAbstraction
{
    void RemoveMainParty();
}

internal class MainPartyInterface : IMainPartyInterface
{
    public void RemoveMainParty()
    {
        GameLoopRunner.RunOnMainThread(RemoveMainPartyInternal, blocking: false);
    }

    private void RemoveMainPartyInternal()
    {
        if (MobileParty.MainParty?.ActualClan != null)
        {
            MobileParty.MainParty.RemoveParty();
        }
    }
}
