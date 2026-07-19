using Common.Messaging;
using Common.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Party;
using GameInterface.Services.Party.Patches;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using System;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class PrisonerSaleProcessorTests
{
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IPlayerManager> playerManager = new();
    private readonly FakePlayerRansomReleaseSettlementProvider releaseSettlementProvider = new();
    private readonly IPrisonerSaleValidator prisonerSaleValidator = new PassthroughPrisonerSaleValidator();

    [Fact]
    public void Sell_PlayerPrisonerInSettlement_PublishesRansomReleaseAtSafeSettlementGate()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var playerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        var requestedPrisoners = Roster(Element(playerCharacter));
        var sellingParty = MobilePartyAtSettlement(Position(10f, 20f));
        var safeSettlement = ObjectHelper.SkipConstructor<Settlement>();
        var safeGatePosition = Position(72.5f, 18.25f);
        safeSettlement.GatePosition = safeGatePosition;
        sellingParty.PrisonRoster = Roster(Element(playerCharacter));
        playerCharacter.HeroObject = playerHero;
        playerManager.Setup(p => p.Contains(playerHero)).Returns(true);
        releaseSettlementProvider.ExpectedSellingParty = sellingParty;
        releaseSettlementProvider.ExpectedPlayerHero = playerHero;
        releaseSettlementProvider.ReleaseSettlement = safeSettlement;

        PlayerCaptivityEndedByServer release = default;
        messageBroker
            .Setup(b => b.Publish(It.IsAny<object>(), It.IsAny<PlayerCaptivityEndedByServer>()))
            .Callback<object, PlayerCaptivityEndedByServer>((_, message) => release = message);
        var processor = CreateProcessor();

        processor.Sell(sellingParty, requestedPrisoners);

        Assert.Same(playerHero, release.PrisonerHero);
        Assert.Equal(EndCaptivityDetail.Ransom, release.Detail);
        Assert.Null(release.Facilitator);
        Assert.True(release.HasReleasePosition);
        AssertPosition(safeGatePosition, release.ReleasePosition);
        Assert.Equal(1, releaseSettlementProvider.CallCount);
        messageBroker.Verify(
            b => b.Publish(It.IsAny<object>(), It.IsAny<PlayerCaptivityEndedByServer>()),
            Times.Once);
    }

    [Fact]
    public void CreateSalePlan_MixedPrisoners_ReleasesPlayerAndLeavesOthersForVanillaSale()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var playerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        var regularCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        var prisoners = Roster(
            Element(playerCharacter),
            Element(regularCharacter, 4, 2));
        playerCharacter.HeroObject = playerHero;
        var sellingParty = ObjectHelper.SkipConstructor<PartyBase>();
        var releaseSettlement = ObjectHelper.SkipConstructor<Settlement>();
        var releasePosition = Position(4f, 8f);
        releaseSettlement.GatePosition = releasePosition;
        playerManager.Setup(p => p.Contains(playerHero)).Returns(true);
        releaseSettlementProvider.ExpectedSellingParty = sellingParty;
        releaseSettlementProvider.ExpectedPlayerHero = playerHero;
        releaseSettlementProvider.ReleaseSettlement = releaseSettlement;
        var processor = CreateProcessor();

        var plan = processor.CreateSalePlan(prisoners, sellingParty);

        var release = Assert.Single(plan.PlayerReleases);
        Assert.Same(playerHero, release.PrisonerHero);
        Assert.Equal(EndCaptivityDetail.Ransom, release.Detail);
        AssertPosition(releasePosition, release.ReleasePosition);
        Assert.Equal(0, plan.PrisonersForVanillaSale.GetTroopCount(playerCharacter));
        Assert.Equal(4, plan.PrisonersForVanillaSale.GetTroopCount(regularCharacter));
        Assert.Equal(2, plan.PrisonersForVanillaSale.GetElementCopyAtIndex(
            plan.PrisonersForVanillaSale.FindIndexOfTroop(regularCharacter)).WoundedNumber);
    }

    [Fact]
    public void PrisonerRansomValue_PlayerHero_ReturnsZero()
    {
        var playerHero = ObjectHelper.SkipConstructor<Hero>();
        var playerCharacter = ObjectHelper.SkipConstructor<CharacterObject>();
        playerCharacter.HeroObject = playerHero;
        var objectManager = new Mock<IObjectManager>();
        objectManager
            .Setup(o => o.TryGetObjectWithLogging<Hero>("player-hero", out playerHero))
            .Returns(true);
        var registeredPlayers = new PlayerManager(
            Mock.Of<Serilog.ILogger>(),
            objectManager.Object,
            new ControllerIdProvider());
        registeredPlayers.AddPlayer(new Player(
            "controller-1",
            "player-hero",
            string.Empty,
            string.Empty,
            string.Empty));

        try
        {
            int result = 100;

            var runOriginal = RansomPlayerValuePatch.PrisonerRansomValuePrefix(
                ref result,
                playerCharacter);

            Assert.False(runOriginal);
            Assert.Equal(0, result);
        }
        finally
        {
            GetPlayerObjects().Remove(playerHero);
        }
    }

    private PrisonerSaleProcessor CreateProcessor() =>
        new(
            messageBroker.Object,
            playerManager.Object,
            prisonerSaleValidator,
            releaseSettlementProvider);

    private static PartyBase MobilePartyAtSettlement(CampaignVec2 gatePosition)
    {
        var settlement = ObjectHelper.SkipConstructor<Settlement>();
        settlement.GatePosition = gatePosition;
        var mobileParty = ObjectHelper.SkipConstructor<MobileParty>();
        var party = ObjectHelper.SkipConstructor<PartyBase>();
        mobileParty.Party = party;
        mobileParty._currentSettlement = settlement;
        party.MobileParty = mobileParty;
        return party;
    }

    private static TroopRoster Roster(params TroopRosterElement[] elements)
    {
        var roster = new TroopRoster();
        foreach (var element in elements)
        {
            roster.AddToCounts(
                element.Character,
                element.Number,
                false,
                element.WoundedNumber,
                element.Xp,
                true);
        }
        return roster;
    }

    private static TroopRosterElement Element(
        CharacterObject character,
        int number = 1,
        int woundedNumber = 0) =>
        new(character)
        {
            Number = number,
            WoundedNumber = woundedNumber,
        };

    private static CampaignVec2 Position(float x, float y) =>
        new(new Vec2(x, y), true);

    private static void AssertPosition(CampaignVec2 expected, CampaignVec2 actual)
    {
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.IsOnLand, actual.IsOnLand);
    }

    private static ConditionalWeakTable<object, ControlledObjectInfo> GetPlayerObjects() =>
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;

    private sealed class PassthroughPrisonerSaleValidator : IPrisonerSaleValidator
    {
        public TroopRoster Validate(TroopRoster requestedRoster, TroopRoster availableRoster) =>
            requestedRoster;
    }

    private sealed class FakePlayerRansomReleaseSettlementProvider : IPlayerRansomReleaseSettlementProvider
    {
        public PartyBase ExpectedSellingParty { get; set; } = null!;

        public Hero ExpectedPlayerHero { get; set; } = null!;

        public Settlement ReleaseSettlement { get; set; } = null!;

        public int CallCount { get; private set; }

        public Settlement GetReleaseSettlement(PartyBase sellingParty, Hero playerHero)
        {
            Assert.Same(ExpectedSellingParty, sellingParty);
            Assert.Same(ExpectedPlayerHero, playerHero);
            CallCount++;
            return ReleaseSettlement;
        }
    }
}
