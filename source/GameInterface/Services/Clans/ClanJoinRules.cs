using Common.Messaging;
using GameInterface.Services.Banners.Messages;
using GameInterface.Services.Clans.Data;
using GameInterface.Services.Players;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
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
        // TODO: Apply asset and family rules on the server before changing the hero's clan.
        joiningHero.Clan = targetClan;
        if (joiningHero.PartyBelongedTo != null)
            joiningHero.PartyBelongedTo.ActualClan = targetClan;

        messageBroker.Publish(this, new PlayerBannerChanged(targetClan));
    }

    private void AddWarning(List<TextObject> warnings, string textId, int count)
    {
        if (count > 0)
            warnings.Add(GameTexts.FindText(textId).SetTextVariable("COUNT", count));
    }
}
