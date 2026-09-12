using Common.Commands;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Hideouts.Commands;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Hideouts;

public class HideoutTeleportCommandTests : MapEventTestBase
{
    private const string CommandName = "coop.debug.hideout.teleport_party";

    public HideoutTeleportCommandTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartyId_WithIdenticalHeroNames_MovesOnlyTheSelectedPartyOnEveryClient(bool selectSecond)
    {
        var first = PrepareHero("first", "Lady Mira", Position(100, 100));
        var second = PrepareHero("second", "Lady Mira", Position(0, 0));
        var selected = selectSecond ? second : first;
        var other = selectSecond ? first : second;
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), "first");
        TestEnvironment.ConnectRegisteredPlayer(Clients.Last(), "second");
        var entrance = selectSecond ? Position(1, 1) : Position(102, 101);
        var otherPosition = selectSecond ? Position(100, 100) : Position(0, 0);
        string destinationId = null;

        Server.Call(() =>
        {
            var western = CreateHideout("Western hideout", Position(1, 1));
            var eastern = CreateHideout("Eastern hideout", Position(102, 101));
            var destination = selectSecond ? western : eastern;
            Assert.True(Server.ObjectManager.TryGetId(destination, out destinationId));
            Assert.False(destination.Hideout.IsSpotted);
            var town = GameObjectCreator.CreateInitializedObject<Settlement>();
            town.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
            town.GatePosition = Position(100, 100);
            Campaign.Current.CampaignObjectManager.Settlements =
                new MBList<Settlement>(Campaign.Current.CampaignObjectManager.Settlements) { town };
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(selected.partyId, out var party));
            party.SetMoveGoToPoint(Position(200, 200), MobileParty.NavigationType.Default);
            Campaign.Current.MainParty = null;

            Assert.IsType<HideoutTeleportCommand>(Server.Resolve<IHideoutTeleportCommand>());
            var result = Execute(selected.partyId);

            Assert.True(result.Succeeded, result.Output);
            Assert.Contains(selectSecond ? "Western hideout" : "Eastern hideout", result.Output);
            Assert.Contains(selected.partyId, result.Output);
            Assert.Null(Campaign.Current.MainParty);
        });

        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(selected.partyId, out var party));
                Assert.Equal(entrance, party.Position);
                Assert.Equal(MoveModeType.Hold, party.PartyMoveMode);
                Assert.Null(party.CurrentSettlement);
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(other.partyId, out var otherParty));
                Assert.Equal(otherPosition, otherParty.Position);
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(destinationId, out var destination));
                Assert.True(destination.Hideout.IsSpotted);
                destination.Party.UpdateVisibilityAndInspected(party.Position, 10f);
                Assert.True(destination.IsVisible);
            });
    }

    [Theory]
    [InlineData("empty", true)]
    [InlineData("empty", false)]
    [InlineData("underpopulated", true)]
    [InlineData("underpopulated", false)]
    [InlineData("no_troops", true)]
    [InlineData("no_troops", false)]
    [InlineData("wounded", true)]
    [InlineData("wounded", false)]
    [InlineData("inactive_defenders", true)]
    [InlineData("inactive_defenders", false)]
    [InlineData("cooldown", true)]
    [InlineData("cooldown", false)]
    public void UnavailableHideout_IsSkippedOrFailsWithoutMoving(string scenario, bool hasAvailableHideout)
    {
        var selected = PrepareHero("selected", "Lady Mira", Position(0, 0));
        Server.Call(() =>
        {
            var unavailable = CreateHideout("Unavailable hideout", Position(1, 1), scenario != "empty");
            if (scenario == "underpopulated") unavailable.Parties.First().CurrentSettlement = null;
            // IsPast uses the campaign clock; the test harness replaces CampaignTime.Now with zero.
            if (scenario == "cooldown") unavailable.Hideout._nextPossibleAttackTime = CampaignTime.DaysFromNow(1);
            foreach (var defender in unavailable.Parties)
            {
                if (scenario == "no_troops") defender.MemberRoster.Clear();
                if (scenario == "wounded")
                    defender.MemberRoster.WoundNumberOfNonHeroTroopsRandomly(defender.MemberRoster.TotalHealthyCount);
                if (scenario == "inactive_defenders") defender.IsActive = false;
            }
            var available = hasAvailableHideout ? CreateHideout("Available hideout", Position(10, 10)) : null;
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(selected.partyId, out var party));
            Campaign.Current.MainParty = null;

            var result = Execute(selected.partyId);

            Assert.Equal(hasAvailableHideout, result.Succeeded);
            Assert.False(unavailable.Hideout.IsSpotted);
            Assert.Null(Campaign.Current.MainParty);
            if (hasAvailableHideout)
            {
                Assert.Contains("Available hideout", result.Output);
                Assert.Equal(available.GatePosition, party.Position);
                Assert.True(available.Hideout.IsSpotted);
            }
            else
            {
                Assert.Equal("hideout_not_found", result.ErrorCode);
                Assert.Equal(Position(0, 0), party.Position);
                Assert.Empty(Server.InternalMessages.GetMessages<UpdatePartyBehavior>());
            }
        });
    }

    [Theory]
    [InlineData("missing", "party_not_found")]
    [InlineData("hero_id", "party_not_found")]
    [InlineData("inactive", "party_unavailable")]
    [InlineData("in_settlement", "party_busy")]
    [InlineData("no_hideout", "hideout_not_found")]
    public void UnavailableTarget_DoesNotRequestMovement(string scenario, string errorCode)
    {
        var selected = PrepareHero("selected", "Lady Mira", Position(100, 100));
        Server.Call(() =>
        {
            if (scenario != "no_hideout")
                CreateHideout("Near hideout", Position(102, 101));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(selected.partyId, out var party));
            if (scenario == "inactive") party.IsActive = false;
            if (scenario == "in_settlement")
                party.SetCurrentSettlementDirectly(Campaign.Current.CampaignObjectManager.Settlements.First());

            var partyId = scenario == "missing" ? "UnknownParty" :
                scenario == "hero_id" ? selected.heroId : selected.partyId;
            var result = Execute(partyId);

            Assert.False(result.Succeeded);
            Assert.Equal(errorCode, result.ErrorCode);
            Assert.Equal(Position(100, 100), party.Position);
            Assert.Empty(Server.InternalMessages.GetMessages<UpdatePartyBehavior>());
        });
    }

    [Fact]
    public void CommandRegistry_RejectsClientExecutionAndMissingPartyId()
    {
        Clients.First().Call(() =>
        {
            var registry = Clients.First().Resolve<ICoopCommandRegistry>();
            var result = registry.ProcessCommand(CommandName,
                new CoopCommandArgsFactory().FromValues(new[] { "UnknownParty" }));
            Assert.False(result.Succeeded);
            Assert.Equal("command_wrong_side", result.ErrorCode);
        });
        Server.Call(() =>
        {
            var result = Execute();
            Assert.False(result.Succeeded);
            Assert.Equal("invalid_arguments", result.ErrorCode);
        });
    }

    private (string heroId, string partyId) PrepareHero(string controllerId, string name, CampaignVec2 position)
    {
        var ids = CreatePlayerHeroParty(controllerId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ids.heroId, out var hero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ids.partyId, out var party));
            hero.SetName(new TextObject(name), new TextObject(name));
            hero.PartyBelongedTo = party;
            party.IsActive = true;
        });
        foreach (var instance in Clients.Append(Server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ids.partyId, out var party));
                party.Position = position;
            });
        return ids;
    }

    private static Settlement CreateHideout(string name, CampaignVec2 entrance, bool populated = true)
    {
        var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
        settlement.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Hideout>());
        settlement._name = new TextObject(name);
        settlement._position = entrance;
        settlement.GatePosition = entrance;
        settlement.Hideout._nextPossibleAttackTime = new CampaignTime(-1);
        Campaign.Current.CampaignObjectManager.Settlements =
            new MBList<Settlement>(Campaign.Current.CampaignObjectManager.Settlements) { settlement };
        if (populated)
        {
            var culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            var bandit = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            bandit.Culture = culture;
            culture.BanditBandit = bandit;
            for (var index = 0; index < Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt; index++)
            {
                var clan = GameObjectCreator.CreateInitializedObject<Clan>();
                clan.Culture = culture;
                var party = BanditPartyComponent.CreateBanditParty($"TeleportBandit{Guid.NewGuid()}", clan,
                    settlement.Hideout, false, null, entrance);
                party.CurrentSettlement = settlement;
                party.MemberRoster.AddToCounts(bandit, 1);
            }
            Assert.True(settlement.Hideout.IsInfested);
        }
        return settlement;
    }

    private CoopCommandResult Execute(params string[] partyIds) => Server.Resolve<ICoopCommandRegistry>()
        .ProcessCommand(CommandName, new CoopCommandArgsFactory().FromValues(partyIds));

    private static CampaignVec2 Position(float x, float y) => new(new Vec2(x, y), true);
}
