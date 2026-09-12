using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;

namespace GameInterface.Services.Locations;

internal interface ISettlementHeroSpawnPool
{
    IReadOnlyCollection<Hero> GetAmbientCandidates(Settlement settlement);
}

/// <summary>
/// Builds the co-op equivalent of vanilla's settlement hero candidate list across every registered player.
/// Controlled player heroes and companions travelling in their parties use the mission mesh instead.
/// </summary>
internal class SettlementHeroSpawnPool : ISettlementHeroSpawnPool
{
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;

    public SettlementHeroSpawnPool(IObjectManager objectManager, IPlayerManager playerManager)
    {
        this.objectManager = objectManager;
        this.playerManager = playerManager;
    }

    public IReadOnlyCollection<Hero> GetAmbientCandidates(Settlement settlement)
    {
        var candidates = new HashSet<Hero>();
        if (settlement == null) return candidates;

        Add(candidates, settlement.HeroesWithoutParty);
        AddFactionLeaderAndSpouse(candidates, settlement);
        AddPlayerClanAndPartyHeroes(candidates);
        AddPrisoners(candidates, settlement);

        foreach (var party in settlement.Parties ?? (IReadOnlyList<MobileParty>)Array.Empty<MobileParty>())
        {
            if (party?.LeaderHero != null)
                candidates.Add(party.LeaderHero);
        }

        AddExistingRosterHeroes(candidates, settlement.LocationComplex);

        candidates.RemoveWhere(hero => !ShouldUseAmbientRoster(hero));
        return candidates;
    }

    private static void AddFactionLeaderAndSpouse(HashSet<Hero> candidates, Settlement settlement)
    {
        Hero leader = null;
        if (settlement.MapFaction?.IsKingdomFaction == true)
            leader = ((Kingdom)settlement.MapFaction).Leader;
        else
            leader = settlement.OwnerClan?.Leader;

        if (leader != null) candidates.Add(leader);
        if (leader?.Spouse != null) candidates.Add(leader.Spouse);
    }

    private void AddPlayerClanAndPartyHeroes(HashSet<Hero> candidates)
    {
        foreach (var player in playerManager.Players)
        {
            if (!objectManager.TryGetObject(player.HeroId, out Hero playerHero) || playerHero == null) continue;

            Add(candidates, playerHero.Clan?.AliveLords);
            try
            {
                Add(candidates, playerHero.CompanionsInParty);
            }
            catch (Exception)
            {
                // An incompletely synchronized player party has no companion projection yet.
            }
        }
    }

    private static void AddPrisoners(HashSet<Hero> candidates, Settlement settlement)
    {
        try
        {
            foreach (var character in settlement.SettlementComponent?.GetPrisonerHeroes()
                ?? Enumerable.Empty<CharacterObject>())
            {
                if (character?.HeroObject != null)
                    candidates.Add(character.HeroObject);
            }
        }
        catch (Exception)
        {
            // Prisoner enumeration depends on the concrete settlement component.
        }
    }

    private static void AddExistingRosterHeroes(HashSet<Hero> candidates, LocationComplex locationComplex)
    {
        if (locationComplex == null) return;

        foreach (var location in locationComplex.GetListOfLocations())
        {
            foreach (var locationCharacter in location.GetCharacterList() ?? Enumerable.Empty<LocationCharacter>())
            {
                Hero hero = locationCharacter?.Character?.HeroObject;
                if (hero != null) candidates.Add(hero);
            }
        }
    }

    private bool ShouldUseAmbientRoster(Hero hero)
    {
        if (hero == null || playerManager.Contains(hero)) return false;

        MobileParty party = hero.PartyBelongedTo;
        return party == null || !playerManager.Contains(party);
    }

    private static void Add(HashSet<Hero> candidates, IEnumerable<Hero> heroes)
    {
        if (heroes == null) return;
        foreach (var hero in heroes)
            if (hero != null) candidates.Add(hero);
    }
}
