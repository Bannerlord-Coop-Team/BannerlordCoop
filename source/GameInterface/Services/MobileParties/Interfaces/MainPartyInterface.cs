using Common;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MobileParties.Interfaces;

/// <summary>
/// Removes the server's own main party, since the dedicated server plays no party on the map.
/// </summary>
internal interface IMainPartyInterface : IGameAbstraction
{
    void RemoveMainParty();
}

/// <inheritdoc cref="IMainPartyInterface"/>
internal class MainPartyInterface : IMainPartyInterface
{
    // Queues the removal on the game thread
    public void RemoveMainParty()
    {
        GameThread.Run(RemoveMainPartyInternal);
    }

    // Removes the main party if it is still a live clan party, naval views handle its missing anchor
    private void RemoveMainPartyInternal()
    {
        if (MobileParty.MainParty?.ActualClan != null)
        {
            MobileParty.MainParty.RemoveParty();
        }
    }
}
