using GameInterface.Services.Heroes.Patches;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Barters;

internal sealed class BarterPlayerContext : IDisposable
{
    private readonly Game game;
    private readonly Campaign campaign;
    private readonly BasicCharacterObject previousPlayerTroop;
    private readonly MobileParty previousMainParty;
    private readonly Clan previousPlayerFaction;
    private readonly Hero previousResolvedMainHero;

    public BarterPlayerContext(Hero playerHero, MobileParty playerParty)
    {
        game = Game.Current;
        campaign = Campaign.Current;
        previousPlayerTroop = game?.PlayerTroop;
        previousMainParty = campaign?.MainParty;
        previousPlayerFaction = campaign?.PlayerDefaultFaction;
        previousResolvedMainHero = ResolvedMainHeroContext.ResolvedMainHero;

        if (game != null)
            game.PlayerTroop = playerHero?.CharacterObject;
        if (campaign != null)
        {
            if (playerParty != null)
                campaign.MainParty = playerParty;
            if (playerHero?.Clan != null)
                campaign.PlayerDefaultFaction = playerHero.Clan;
        }
        ResolvedMainHeroContext.ResolvedMainHero = playerHero;
    }

    public void Dispose()
    {
        ResolvedMainHeroContext.ResolvedMainHero = previousResolvedMainHero;
        if (campaign != null)
        {
            campaign.PlayerDefaultFaction = previousPlayerFaction;
            campaign.MainParty = previousMainParty;
        }
        if (game != null)
            game.PlayerTroop = previousPlayerTroop;
    }
}
