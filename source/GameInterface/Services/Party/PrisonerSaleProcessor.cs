using Common.Messaging;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party;

internal interface IPrisonerSaleProcessor
{
    void Sell(PartyBase sellingParty, TroopRoster requestedPrisoners);
}

internal readonly struct PrisonerSalePlan
{
    public readonly TroopRoster PrisonersForVanillaSale;
    public readonly IReadOnlyList<PlayerCaptivityEndedByServer> PlayerReleases;

    public PrisonerSalePlan(
        TroopRoster prisonersForVanillaSale,
        IReadOnlyList<PlayerCaptivityEndedByServer> playerReleases)
    {
        PrisonersForVanillaSale = prisonersForVanillaSale;
        PlayerReleases = playerReleases;
    }
}

/// <summary>
/// Applies an authoritative prisoner sale while releasing co-op player heroes through the full
/// player-captivity path that restores their parked parties.
/// </summary>
internal class PrisonerSaleProcessor : IPrisonerSaleProcessor
{
    private readonly IMessageBroker messageBroker;
    private readonly IPlayerManager playerManager;
    private readonly IPrisonerSaleValidator prisonerSaleValidator;
    private readonly IPlayerRansomReleaseSettlementProvider releaseSettlementProvider;

    public PrisonerSaleProcessor(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IPrisonerSaleValidator prisonerSaleValidator,
        IPlayerRansomReleaseSettlementProvider releaseSettlementProvider)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.prisonerSaleValidator = prisonerSaleValidator;
        this.releaseSettlementProvider = releaseSettlementProvider;
    }

    public void Sell(PartyBase sellingParty, TroopRoster requestedPrisoners)
    {
        if (sellingParty == null) throw new System.ArgumentNullException(nameof(sellingParty));
        if (requestedPrisoners == null) throw new System.ArgumentNullException(nameof(requestedPrisoners));

        var validatedPrisoners = prisonerSaleValidator.Validate(
            requestedPrisoners,
            sellingParty.PrisonRoster);
        var plan = CreateSalePlan(validatedPrisoners, sellingParty);

        if (plan.PrisonersForVanillaSale.Count > 0)
        {
            SellPrisonersAction.ApplyForSelectedPrisoners(
                sellingParty,
                null,
                plan.PrisonersForVanillaSale);
        }

        foreach (var release in plan.PlayerReleases)
        {
            messageBroker.Publish(this, release);
        }
    }

    internal PrisonerSalePlan CreateSalePlan(
        TroopRoster validatedPrisoners,
        PartyBase sellingParty)
    {
        var prisonersForVanillaSale = new TroopRoster();
        var playerReleases = new List<PlayerCaptivityEndedByServer>();

        foreach (var prisoner in validatedPrisoners.GetTroopRoster())
        {
            var hero = prisoner.Character?.HeroObject;
            if (hero != null && playerManager.Contains(hero))
            {
                var releaseSettlement = releaseSettlementProvider.GetReleaseSettlement(sellingParty, hero);
                playerReleases.Add(new PlayerCaptivityEndedByServer(
                    hero,
                    EndCaptivityDetail.Ransom,
                    null,
                    releaseSettlement.GatePosition));
                continue;
            }

            prisonersForVanillaSale.AddToCounts(
                prisoner.Character,
                prisoner.Number,
                false,
                prisoner.WoundedNumber,
                prisoner.Xp,
                true);
        }

        return new PrisonerSalePlan(prisonersForVanillaSale, playerReleases);
    }
}
