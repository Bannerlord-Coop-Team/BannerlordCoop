using GameInterface.CoopSessionData;
using GameInterface.Services.NavalDLC;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;

namespace Coop.Naval.CoopSession.Interfaces;

public class MainHeroFigureheadsScope : IMainHeroFigureheadsScope
{
    private readonly ICoopSessionProvider coopSessionProvider;
    private readonly ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface;

    public MainHeroFigureheadsScope(ICoopSessionProvider coopSessionProvider, ISessionNavalPlayerDataInterface sessionNavalPlayerDataInterface)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.sessionNavalPlayerDataInterface = sessionNavalPlayerDataInterface;
    }

    public IDisposable Enter(Hero playerHero)
    {
        var campaign = Campaign.Current;
        var previousFigureheads = campaign.UnlockedFigureheadsByMainHero;
        campaign.UnlockedFigureheadsByMainHero = sessionNavalPlayerDataInterface.GetUnlockedFigureheads(coopSessionProvider.CoopSession.NavalPlayerData, playerHero);

        return new Scope(campaign, previousFigureheads);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Campaign campaign;
        private readonly List<Figurehead> previousFigureheads;

        public Scope(Campaign campaign, List<Figurehead> previousFigureheads)
        {
            this.campaign = campaign;
            this.previousFigureheads = previousFigureheads;
        }

        public void Dispose()
        {
            campaign.UnlockedFigureheadsByMainHero = previousFigureheads;
        }
    }
}
