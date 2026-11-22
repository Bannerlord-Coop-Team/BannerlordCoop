using E2E.Tests.Environment;
using GameInterface.Services.Issues.Framework.Gating;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class IssuesCampaignBehaviorSettlementEnteredPatchTests : IDisposable
{
    private readonly QuestScenario scenario;

    private E2ETestEnvironment TestEnvironment { get; }

    public IssuesCampaignBehaviorSettlementEnteredPatchTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
        scenario = new QuestScenario(TestEnvironment);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ServerSkipsAPlayerPartyEnteringASettlement()
    {
        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(scenario.OwnerPartyId, out var playerParty));
            Assert.False(IssuesCampaignBehaviorSettlementEnteredPatch.Prefix(playerParty));
        });
    }

    [Fact]
    public void ServerRunsAnAiPartyOrALoneHeroEnteringASettlement()
    {
        var aiPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        scenario.Server.Call(() =>
        {
            Assert.True(scenario.Server.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));
            Assert.True(IssuesCampaignBehaviorSettlementEnteredPatch.Prefix(aiParty));
            Assert.True(IssuesCampaignBehaviorSettlementEnteredPatch.Prefix(null));
        });
    }

    [Fact]
    public void ClientNeverRunsASettlementEntry()
    {
        var aiPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        scenario.ClientA.Call(() =>
        {
            Assert.True(scenario.ClientA.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));
            Assert.False(IssuesCampaignBehaviorSettlementEnteredPatch.Prefix(aiParty));
        });
    }
}
