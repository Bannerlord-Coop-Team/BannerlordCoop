using Common;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Interfaces;

internal interface IArtisanProductAuthority
{
    bool TryEnter(Hero giver, out IDisposable scope, Hero requiredPlayer = null);
}

internal sealed class ArtisanProductAuthority : IArtisanProductAuthority
{
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IPlayerManager players;
    private readonly IObjectManager objects;
    private readonly IArtisanProductTraits traits;

    public ArtisanProductAuthority(IIssueOwnershipRegistry ownership, IPlayerManager players, IObjectManager objects,
        IArtisanProductTraits traits)
    {
        this.ownership = ownership;
        this.players = players;
        this.objects = objects;
        this.traits = traits;
    }

    public bool TryEnter(Hero giver, out IDisposable scope, Hero requiredPlayer = null)
    {
        scope = null;
        if (ModInformation.IsClient || giver == null) return false;
        if (!ownership.TryGetOwnerControllerId(giver, out var controller)) return false;
        if (!players.TryGetPlayer(controller, out var player)) return false;
        if (!objects.TryGetObjectWithLogging<Hero>(player.HeroId, out var hero)) return false;
        if (requiredPlayer != null && hero != requiredPlayer) return false;
        if (!objects.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) return false;
        scope = new OwnerScope(hero, party, traits.Enter(hero));
        return true;
    }

    private sealed class OwnerScope : IDisposable
    {
        private readonly MainHeroSubstitutionScope player;
        private readonly IssueFinalizeAuthorityGuard finalization;
        private readonly IDisposable traits;

        public OwnerScope(Hero hero, MobileParty party, IDisposable traits)
        {
            player = new MainHeroSubstitutionScope(hero, party);
            finalization = new IssueFinalizeAuthorityGuard();
            this.traits = traits;
        }

        public void Dispose()
        {
            try
            {
                traits.Dispose();
            }
            finally
            {
                finalization.Dispose();
                player.Dispose();
            }
        }
    }
}
