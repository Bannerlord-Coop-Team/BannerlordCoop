using Common.Messaging;
using Common.Logging;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Party;

internal interface IPrisonerSaleProcessor
{
    PrisonerSalePlan Prepare(PartyBase sellingParty, TroopRoster requestedPrisoners);
    void ApplyCore(PartyBase sellingParty, PrisonerSalePlan plan);
    void PublishPostCommit(PrisonerSalePlan plan);
    void Sell(PartyBase sellingParty, TroopRoster requestedPrisoners);
}

internal readonly struct PrisonerSalePlan
{
    public readonly PartyBase SellingParty;
    public readonly TroopRoster PrisonersForVanillaSale;
    public readonly TroopRoster HeroPrisonersForPostCommitSale;
    public readonly IReadOnlyList<PlayerCaptivityEndedByServer> PlayerReleases;

    public PrisonerSalePlan(
        PartyBase sellingParty,
        TroopRoster prisonersForVanillaSale,
        TroopRoster heroPrisonersForPostCommitSale,
        IReadOnlyList<PlayerCaptivityEndedByServer> playerReleases)
    {
        SellingParty = sellingParty;
        PrisonersForVanillaSale = prisonersForVanillaSale;
        HeroPrisonersForPostCommitSale = heroPrisonersForPostCommitSale;
        PlayerReleases = playerReleases;
    }
}

/// <summary>
/// Applies an authoritative prisoner sale while releasing co-op player heroes through the full
/// player-captivity path that restores their parked parties.
/// </summary>
internal class PrisonerSaleProcessor : IPrisonerSaleProcessor
{
    private static readonly ILogger Logger =
        LogManager.GetLogger<PrisonerSaleProcessor>();
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
        var plan = Prepare(sellingParty, requestedPrisoners);
        ApplyCore(sellingParty, plan);
        PublishPostCommit(plan);
    }

    public PrisonerSalePlan Prepare(
        PartyBase sellingParty,
        TroopRoster requestedPrisoners)
    {
        if (sellingParty == null) throw new System.ArgumentNullException(nameof(sellingParty));
        if (requestedPrisoners == null) throw new System.ArgumentNullException(nameof(requestedPrisoners));

        var validatedPrisoners = prisonerSaleValidator.Validate(
            requestedPrisoners, sellingParty.PrisonRoster);
        return CreateSalePlan(validatedPrisoners, sellingParty);
    }

    public void ApplyCore(PartyBase sellingParty, PrisonerSalePlan plan)
    {
        if (sellingParty == null) throw new System.ArgumentNullException(nameof(sellingParty));
        if (plan.PrisonersForVanillaSale.Count > 0)
        {
            SellPrisonersAction.ApplyForSelectedPrisoners(
                sellingParty,
                null,
                plan.PrisonersForVanillaSale);
        }
    }

    public void PublishPostCommit(PrisonerSalePlan plan)
    {
        // Native hero sales change captivity state and publish campaign events. Those effects cannot be
        // undone by restoring a roster, so heroes deliberately run only after the reversible regular core
        // is final. Isolate each hero so one bad listener cannot suppress unrelated releases.
        foreach (var prisoner in plan.HeroPrisonersForPostCommitSale.GetTroopRoster())
        {
            var oneHero = new TroopRoster();
            oneHero.AddToCounts(
                prisoner.Character,
                prisoner.Number,
                false,
                prisoner.WoundedNumber,
                prisoner.Xp,
                true);
            try
            {
                SellPrisonersAction.ApplyForSelectedPrisoners(
                    plan.SellingParty, null, oneHero);
            }
            catch (Exception exception)
            {
                // Do not retry a partially completed native hero action: that could pay or publish twice.
                Logger.Error(
                    exception,
                    "Post-commit AI hero prisoner sale failed for {CharacterId}",
                    prisoner.Character?.StringId ?? "unknown-hero");
            }
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
        var heroPrisonersForPostCommitSale = new TroopRoster();
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

            if (hero != null)
            {
                heroPrisonersForPostCommitSale.AddToCounts(
                    prisoner.Character,
                    prisoner.Number,
                    false,
                    prisoner.WoundedNumber,
                    prisoner.Xp,
                    true);
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

        return new PrisonerSalePlan(
            sellingParty,
            prisonersForVanillaSale,
            heroPrisonersForPostCommitSale,
            playerReleases);
    }
}
