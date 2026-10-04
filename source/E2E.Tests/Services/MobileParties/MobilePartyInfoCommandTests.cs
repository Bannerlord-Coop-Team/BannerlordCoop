using Common.Commands;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.MobileParties.Commands;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

public class MobilePartyInfoCommandTests : MapEventTestBase
{
    public MobilePartyInfoCommandTests(ITestOutputHelper output) : base(output) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Info_ReportsExactArmyNavigationAndBattleSideIdentities(bool attached)
    {
        var battle = CreateServerMapEvent();
        var memberId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        string? armyId = null;
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(battle.AttackerPartyId);
            var member = Server.GetRegisteredObject<MobileParty>(memberId);
            member.Party.SetCustomName(new TextObject("Diagnostic army member"));
            var army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), leader, Army.ArmyTypes.Raider);
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
            member.Army = army;
            member.SetMoveEscortParty(leader, MobileParty.NavigationType.Default, false);
            member.Party.MapEventSide = Server.GetRegisteredObject<MapEvent>(battle.MapEventId).AttackerSide;
            if (attached) army.AddPartyToMergedParties(member);
            member.Ai.Tick(0.1f);
            TestEnvironment.FlushCoalescer();
        }, MapEventDisabledMethods);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var member = instance.GetRegisteredObject<MobileParty>(memberId);
                member.Party.SetCustomName(new TextObject("Diagnostic army member"));
                member.Ai.Tick(0.1f);
                var args = new CoopCommandArgsFactory().FromValues(new[] { memberId });
                var result = new MobilePartyDebugCommand.InfoCoopCommand().ProcessCommand(args);
                Assert.True(result.Succeeded, result.Output);
                var state = JObject.Parse(result.Output.Split("LIVE_TEST_JSON=").Last());
                Assert.Equal(memberId, state.Value<string>("partyId"));
                Assert.Equal(armyId, state.Value<string>("armyId"));
                Assert.Equal(attached ? battle.AttackerPartyId : null, state.Value<string>("attachedToId"));
                Assert.Equal("EscortParty", state.Value<string>("defaultBehavior"));
                Assert.Equal(battle.AttackerPartyId, state.Value<string>("targetPartyId"));
                Assert.Equal("Default", state.Value<string>("desiredAiNavigationType"));
                Assert.True(state.Value<bool>("mapEventSidePresent"));
                Assert.Equal(battle.MapEventId, state.Value<string>("mapEventId"));
                Assert.Equal("Attacker", state.Value<string>("missionSide"));
            }, MapEventDisabledMethods);
        }

        Server.Call(() => Server.GetRegisteredObject<MobileParty>(memberId).Party.MapEventSide = null,
            MapEventDisabledMethods);
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var args = new CoopCommandArgsFactory().FromValues(new[] { memberId });
                var result = new MobilePartyDebugCommand.InfoCoopCommand().ProcessCommand(args);
                Assert.True(result.Succeeded, result.Output);
                var state = JObject.Parse(result.Output.Split("LIVE_TEST_JSON=").Last());
                Assert.False(state.Value<bool>("mapEventSidePresent"));
                Assert.Null(state.Value<string>("mapEventId"));
                Assert.Null(state.Value<string>("missionSide"));
                Assert.Equal(armyId, state.Value<string>("armyId"));
                Assert.Equal(battle.AttackerPartyId, state.Value<string>("targetPartyId"));
            }, MapEventDisabledMethods);
        }
    }

    [Fact]
    public void Info_PreservesStringIdLookupAndRejectsMissingParty()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                party.Party.SetCustomName(new TextObject("Diagnostic party"));
                var factory = new CoopCommandArgsFactory();
                var command = new MobilePartyDebugCommand.InfoCoopCommand();
                var result = command.ProcessCommand(factory.FromValues(new[] { party.StringId }));
                Assert.True(result.Succeeded, result.Output);
                Assert.Contains("Fields:", result.Output);
                var state = JObject.Parse(result.Output.Split("LIVE_TEST_JSON=").Last());
                Assert.Equal(partyId, state.Value<string>("partyId"));
                Assert.False(command.ProcessCommand(factory.FromValues(new[] { "missing-diagnostic-party" })).Succeeded);
            });
        }
    }
}
