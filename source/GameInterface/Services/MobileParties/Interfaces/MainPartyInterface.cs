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
        var party = MobileParty.MainParty;
        if (party == null) return;

        try
        {
            if (party.ActualClan == null) return;
            if (party.LeaderHero == null) return;
            if (party.Party == null) return;

            party.RemoveParty();
        }
        catch (System.Exception ex)
        {
            Common.Logging.LogManager.GetLogger<MainPartyInterface>().Error(ex, "Failed to remove MainParty safely");
        }
    }
}
