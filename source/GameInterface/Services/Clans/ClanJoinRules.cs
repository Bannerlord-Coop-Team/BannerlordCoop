using Common.Messaging;
using GameInterface.Services.Banners.Messages;
using GameInterface.Services.Clans.Data;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Clans;

public interface IClanJoinRules : IGameAbstraction
{
    bool CanOfferServices(Hero joiningHero, Clan targetClan);
    ClanJoinUnavailableReason GetUnavailableReason(Hero joiningHero, Hero targetHero);
    IReadOnlyList<TextObject> GetWarnings(Hero joiningHero, Clan targetClan);
    void Apply(Hero joiningHero, Clan targetClan);
}

public class ClanJoinRules : IClanJoinRules
{
    private readonly IPlayerManager playerManager;
    private readonly IMessageBroker messageBroker;

    public ClanJoinRules(IPlayerManager playerManager, IMessageBroker messageBroker)
    {
        this.playerManager = playerManager;
        this.messageBroker = messageBroker;
    }

    public bool CanOfferServices(Hero joiningHero, Clan targetClan)
    {
        var clan = joiningHero?.Clan;
        return clan != null && targetClan != null && clan != targetClan &&
            (clan.Leader != joiningHero || !HasOtherPlayers(joiningHero));
    }

    public ClanJoinUnavailableReason GetUnavailableReason(Hero joiningHero, Hero targetHero)
    {
        var clan = joiningHero?.Clan;
        var targetClan = targetHero?.Clan;
        if (clan == null || targetClan == null) return ClanJoinUnavailableReason.MissingClan;
        if (clan == targetClan) return ClanJoinUnavailableReason.SameClan;
        if (HasOtherPlayers(joiningHero)) return ClanJoinUnavailableReason.OtherPlayersInClan;
        if (targetClan.Leader != targetHero) return ClanJoinUnavailableReason.TargetIsNotClanLeader;
        if (clan.Kingdom?.RulingClan == clan) return ClanJoinUnavailableReason.RulesKingdom;
        if (clan.IsUnderMercenaryService) return ClanJoinUnavailableReason.Mercenary;
        if (clan.Kingdom != null) return ClanJoinUnavailableReason.Vassal;
        if (clan.Fiefs.Count > 0) return ClanJoinUnavailableReason.OwnsFiefs;
        if (clan.FactionsAtWarWith.Any(faction => !targetClan.IsAtWarWith(faction)))
            return ClanJoinUnavailableReason.IncompatibleWars;

        return ClanJoinUnavailableReason.None;
    }

    private bool HasOtherPlayers(Hero joiningHero)
    {
        return joiningHero.Clan.Heroes.Any(hero => hero != joiningHero && playerManager.Contains(hero));
    }

    public IReadOnlyList<TextObject> GetWarnings(Hero joiningHero, Clan targetClan)
    {
        var warnings = new List<TextObject>();
        var clan = joiningHero.Clan;
        AddWarning(warnings, "str_coop_clan_join_companions", clan.Companions.Count);
        AddWarning(warnings, "str_coop_clan_join_workshops", clan.Heroes.Sum(hero => hero.OwnedWorkshops.Count));
        AddWarning(warnings, "str_coop_clan_join_caravans", clan.Heroes.Sum(hero => hero.OwnedCaravans.Count));
        AddWarning(warnings, "str_coop_clan_join_parties", clan.WarPartyComponents.Count(party => !playerManager.Contains(party.MobileParty)));
        AddWarning(warnings, "str_coop_clan_join_alleys", clan.Heroes.Sum(hero => hero.OwnedAlleys.Count));
        AddWarning(warnings, "str_coop_clan_join_supporters", clan.SupporterNotables.Count);
        return warnings;
    }

    public void Apply(Hero joiningHero, Clan targetClan)
    {
        var sourceClan = joiningHero.Clan;
        if (sourceClan == targetClan) return;

        var newLeader = targetClan.Leader;
        var heroes = sourceClan.Heroes.ToArray();
        var parties = sourceClan.WarPartyComponents.ToArray();

        TransferFamilyMembers(sourceClan, targetClan, joiningHero);
        TransferCompanions(sourceClan, targetClan);
        TransferWorkshops(heroes, newLeader);
        TransferCaravans(heroes, newLeader);
        TransferParties(parties, targetClan);
        TransferAlleys(heroes, newLeader);
        TransferSupporters(sourceClan, targetClan);

        joiningHero.Clan = targetClan;
        if (joiningHero.PartyBelongedTo != null)
        {
            joiningHero.PartyBelongedTo.ActualClan = targetClan;
        }

        messageBroker.Publish(this, new PlayerBannerChanged(targetClan));
    }

    private void AddWarning(List<TextObject> warnings, string textId, int count)
    {
        if (count > 0)
            warnings.Add(GameTexts.FindText(textId).SetTextVariable("COUNT", count));
    }

    private void TransferFamilyMembers(Clan sourceClan, Clan targetClan, Hero joiningHero)
    {
        foreach (var familyMember in sourceClan.AliveLords.ToArray())
        {
            if (familyMember == joiningHero) continue;

            familyMember.Clan = targetClan;
        }
    }

    private void TransferCompanions(Clan sourceClan, Clan targetClan)
    {
        foreach (var companion in sourceClan.Companions.ToArray())
        {
            companion.CompanionOf = targetClan;
        }
    }

    private void TransferWorkshops(Hero[] heroes, Hero newLeader)
    {
        foreach (var workshop in heroes.SelectMany(hero => hero.OwnedWorkshops).ToArray())
        {
            ChangeOwnerOfWorkshopAction.ApplyInternal(workshop, newLeader, workshop.WorkshopType, workshop.Capital, 0);
            // TODO: Transfer warehouse player data
        }
    }

    private void TransferCaravans(Hero[] heroes, Hero newLeader)
    {
        foreach (var caravan in heroes.SelectMany(hero => hero.OwnedCaravans).ToArray())
        {
            CaravanPartyComponent.TransferCaravanOwnership(caravan.MobileParty, newLeader, caravan.HomeSettlement);
        }
    }

    private void TransferParties(WarPartyComponent[] parties, Clan targetClan)
    {
        foreach (var party in parties)
        {
            party.MobileParty.ActualClan = targetClan;
        }
    }

    private void TransferAlleys(Hero[] heroes, Hero newLeader)
    {
        foreach (var alley in heroes.SelectMany(hero => hero.OwnedAlleys).ToArray())
        {
            alley.SetOwner(newLeader);
        }
    }

    private void TransferSupporters(Clan sourceClan, Clan targetClan)
    {
        foreach (var supporter in sourceClan.SupporterNotables.ToArray())
        {
            supporter.SupporterOf = targetClan;
        }
    }
}
