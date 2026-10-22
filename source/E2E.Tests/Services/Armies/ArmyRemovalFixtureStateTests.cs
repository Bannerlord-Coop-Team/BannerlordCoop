#if DEBUG
using Common.Commands;
using E2E.Tests.Environment;
using GameInterface.Services.Armies.Commands;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyRemovalFixtureStateTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public ArmyRemovalFixtureStateTests(ITestOutputHelper output) => environment = new E2ETestEnvironment(output);
    public void Dispose() => environment.Dispose();

    [Fact]
    public void State_ObservesMembershipAttachmentsAndDestructionOnBothRecipients()
    {
        var armyId = environment.CreateRegisteredObject<Army>();
        var memberId = environment.CreateRegisteredObject<MobileParty>();
        string leaderId = null;
        environment.Server.Call(() =>
        {
            var army = environment.Server.GetRegisteredObject<Army>(armyId);
            Assert.True(environment.Server.ObjectManager.TryGetId(army.LeaderParty, out leaderId));
            var member = environment.Server.GetRegisteredObject<MobileParty>(memberId);
            member.Army = army;
            army.AddPartyToMergedParties(member);
        });
        foreach (var instance in environment.Clients.Append(environment.Server))
        {
            instance.Call(() =>
            {
                var command = new ArmyRemovalFixtureStateCommand(instance.ObjectManager);
                var args = new CoopCommandArgsFactory().FromValues(new[] { armyId, leaderId, memberId });
                var result = command.ProcessCommand(args);
                Assert.True(result.Succeeded);
                var state = JObject.Parse(result.Output.Substring("LIVE_TEST_JSON=".Length));
                Assert.True(state.Value<bool>("armyExists"));
                Assert.Equal(leaderId, state.Value<string>("leaderId"));
                Assert.Equal(leaderId, state["parties"]![1]!.Value<string>("attachedToId"));
                Assert.Contains(memberId, state["parties"]![0]!["attachedPartyIds"]!.Values<string>());
            });
        }
        environment.Server.Call(() => environment.Server.GetRegisteredObject<MobileParty>(leaderId).Army = null);
        foreach (var instance in environment.Clients.Append(environment.Server))
        {
            instance.Call(() =>
            {
                var result = new ArmyRemovalFixtureStateCommand(instance.ObjectManager).ProcessCommand(
                    new CoopCommandArgsFactory().FromValues(new[] { armyId, leaderId, memberId }));
                Assert.True(result.Succeeded);
                var state = JObject.Parse(result.Output.Substring("LIVE_TEST_JSON=".Length));
                Assert.False(state.Value<bool>("armyExists"));
                Assert.Empty(state["armyPartyIds"]!);
                foreach (var party in state["parties"]!)
                {
                    Assert.True(party.Value<bool>("exists"));
                    Assert.Null(party.Value<string>("armyId"));
                    Assert.Null(party.Value<string>("attachedToId"));
                    Assert.Empty(party["attachedPartyIds"]!);
                }
            });
        }
    }
}
#endif
