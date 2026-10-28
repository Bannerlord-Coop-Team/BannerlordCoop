using GameInterface.Services.Clans;
using GameInterface.Services.Heroes.Extensions;
using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Localization;

namespace GameInterface.Services.MapEvents.PlayerPartyInteractions;

// Not a MarriageBarterable, otherwise MarriageBarterPatch treats the player trade as an NPC marriage barter.
internal class PlayerPartyMarriageBarterable : Barterable
{
    public Hero HeroBeingMarried { get; }
    public Hero Spouse { get; }

    public override int MaxAmount => 1;
    public override string StringID => "marriage_barterable";

    public override TextObject Name
    {
        get
        {
            var name = new TextObject("{=rv6hk8X2}{HERO_BEING_PROPOSED_TO.NAME} to marry {HERO_TO_MARRY.NAME}");
            StringHelpers.SetCharacterProperties("HERO_BEING_PROPOSED_TO", HeroBeingMarried.CharacterObject, name);
            StringHelpers.SetCharacterProperties("HERO_TO_MARRY", Spouse.CharacterObject, name);
            return name;
        }
    }

    public PlayerPartyMarriageBarterable(
        Hero ownerHero,
        PartyBase ownerParty,
        Hero heroBeingMarried,
        Hero spouse) : base(ownerHero, ownerParty)
    {
        HeroBeingMarried = heroBeingMarried;
        Spouse = spouse;
    }

    public override void Apply()
    {
    }

    public override int GetUnitValueForFaction(IFaction faction)
    {
        if (faction == Spouse.Clan || faction == Spouse.MapFaction)
            return 1;

        return -1;
    }

    // Shows the viewing player the hero from the other clan, like vanilla marriage barters.
    private Hero DisplayedHero => HeroBeingMarried.Clan == Hero.MainHero?.Clan ? Spouse : HeroBeingMarried;

    // CharacterCode.CreateFrom(character) leaves the colours unset, which draws the clothes in default gray.
    public override ImageIdentifier GetVisualIdentifier()
    {
        var character = DisplayedHero.CharacterObject;
        var mapFaction = DisplayedHero.MapFaction;

        return new CharacterImageIdentifier(CharacterCode.CreateFrom(
            character.Equipment?.CalculateEquipmentCode(),
            character.GetBodyProperties(character.Equipment),
            character.IsFemale,
            character.IsHero,
            mapFaction?.Color ?? character.Culture.Color,
            mapFaction?.Color2 ?? character.Culture.Color2,
            character.DefaultFormationClass,
            character.Race));
    }

    public override string GetEncyclopediaLink()
        => DisplayedHero.EncyclopediaLink;

    // Every pair is listed on both sides so either player can propose it; the other side's copy hides once offered.
    public static IEnumerable<(Hero, Hero)> GetMarriageCandidates(PartyBase ownerParty, PartyBase otherParty)
    {
        if (!TryGetLeaderClans(ownerParty, otherParty, out var ownerClan, out var otherClan)) yield break;

        foreach (var hero in ownerClan.Heroes)
        {
            foreach (var spouse in otherClan.Heroes)
            {
                if (CanOfferMarriage(ownerParty, otherParty, hero, spouse))
                    yield return (hero, spouse);
            }
        }
    }

    public static bool CanOfferMarriage(PartyBase ownerParty, PartyBase otherParty, Hero hero, Hero spouse)
    {
        if (!TryGetLeaderClans(ownerParty, otherParty, out var ownerClan, out var otherClan)) return false;
        if (hero?.Clan != ownerClan || spouse?.Clan != otherClan) return false;
        if (PlayerPartyPeaceBarterable.AreHostile(
            PlayerPartyPeaceBarterable.GetMapFaction(ownerParty),
            PlayerPartyPeaceBarterable.GetMapFaction(otherParty))) return false;

        // Marriages involving player heroes go through the ProposeMarriage dialog instead.
        if (hero.IsPlayerHero() || spouse.IsPlayerHero()) return false;

        // Hero.MainHero isnt the trading player on the server, so check the party leaders explicitly.
        if (!ContainerProvider.TryResolve<ICoopClanPermissions>(out var permissions) ||
            !permissions.CanManageHero(ownerParty.LeaderHero, hero) ||
            !permissions.CanManageHero(otherParty.LeaderHero, spouse)) return false;

        return Campaign.Current.Models.MarriageModel.IsCoupleSuitableForMarriage(hero, spouse);
    }

    private static bool TryGetLeaderClans(PartyBase ownerParty, PartyBase otherParty, out Clan ownerClan, out Clan otherClan)
    {
        ownerClan = ownerParty?.LeaderHero?.Clan;
        otherClan = otherParty?.LeaderHero?.Clan;

        return ownerClan != null && otherClan != null && ownerClan != otherClan &&
               ownerClan.Leader == ownerParty.LeaderHero && otherClan.Leader == otherParty.LeaderHero;
    }
}
