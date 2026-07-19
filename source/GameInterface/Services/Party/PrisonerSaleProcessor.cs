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

    public PrisonerSaleProcessor(
        IMessageBroker messageBroker,
        IPlayerManager playerManager,
        IPrisonerSaleValidator prisonerSaleValidator)
    {
        this.messageBroker = messageBroker;
        this.playerManager = playerManager;
        this.prisonerSaleValidator = prisonerSaleValidator;
    }

    public void Sell(PartyBase sellingParty, TroopRoster requestedPrisoners)
    {
        if (sellingParty == null) throw new System.ArgumentNullException(nameof(sellingParty));
        if (requestedPrisoners == null) throw new System.ArgumentNullException(nameof(requestedPrisoners));

        var validatedPrisoners = prisonerSaleValidator.Validate(
            requestedPrisoners,
            sellingParty.PrisonRoster);
        var plan = CreateSalePlan(validatedPrisoners, GetReleasePosition(sellingParty));

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
        CampaignVec2 releasePosition)
    {
        var prisonersForVanillaSale = new TroopRoster();
        var playerReleases = new List<PlayerCaptivityEndedByServer>();

        foreach (var prisoner in validatedPrisoners.GetTroopRoster())
        {
            var hero = prisoner.Character?.HeroObject;
            if (hero != null && playerManager.Contains(hero))
            {
                playerReleases.Add(new PlayerCaptivityEndedByServer(
                    hero,
                    EndCaptivityDetail.Ransom,
                    null,
                    releasePosition));
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

    private static CampaignVec2 GetReleasePosition(PartyBase sellingParty)
    {
        var currentSettlement = sellingParty.MobileParty?.CurrentSettlement;
        return currentSettlement?.GatePosition ?? sellingParty.Position;
    }
}
