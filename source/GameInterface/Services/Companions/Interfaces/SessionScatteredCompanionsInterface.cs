using GameInterface.CoopSessionData;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Companions.Interfaces;

public interface ISessionScatteredCompanionsInterface : IGameAbstraction
{
    void AddScatteredCompanion(string playerHeroId, string companionHeroId, CampaignTime time);
    void RemoveScatteredCompanion(string companionHeroId);
    List<string> GetScatteredCompanions(string playerHeroId);
}

internal class SessionScatteredCompanionsInterface : ISessionScatteredCompanionsInterface
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private Dictionary<string, Dictionary<string, long>> PlayerScatteredCompanions => coopSessionProvider.CoopSession.HeroMeetingData.PlayerScatteredCompanions;

    public SessionScatteredCompanionsInterface(ICoopSessionProvider coopSessionProvider)
    {
        this.coopSessionProvider = coopSessionProvider;
    }

    public void AddScatteredCompanion(string playerHeroId, string companionHeroId, CampaignTime time)
    {
        if (!PlayerScatteredCompanions.TryGetValue(playerHeroId, out var scatteredCompanions))
        {
            scatteredCompanions = new();
            PlayerScatteredCompanions[playerHeroId] = scatteredCompanions;
        }

        scatteredCompanions[companionHeroId] = time.NumTicks;
    }

    public void RemoveScatteredCompanion(string companionHeroId)
    {
        foreach (var scatteredCompanions in PlayerScatteredCompanions.Values)
        {
            scatteredCompanions.Remove(companionHeroId);
        }
    }

    public List<string> GetScatteredCompanions(string playerHeroId)
    {
        if (!PlayerScatteredCompanions.TryGetValue(playerHeroId, out var scatteredCompanions)) return new List<string>();

        return new List<string>(scatteredCompanions.Keys);
    }
}
