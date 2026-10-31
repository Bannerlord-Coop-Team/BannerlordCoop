using Common;
using Common.Util;
using TaleWorlds.CampaignSystem.Naval;
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
        GameThread.Run(RemoveMainPartyInternal);
    }

    private void RemoveMainPartyInternal()
    {
        if (MobileParty.MainParty?.ActualClan != null)
        {
            MobileParty.MainParty.RemoveParty();

            // The server's naval map views still read MainParty.Anchor every frame after RemoveParty nulls it.
            // The party is no longer registered, so the placeholder anchor stays local to the server.
            using (new AllowedThread())
            {
                MobileParty.MainParty.SetAnchor(new AnchorPoint(MobileParty.MainParty));
            }
        }
    }
}
