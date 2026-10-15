using Common.Commands;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Settlements.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Settlements;

public class SettlementListNotablesCommandTests : IDisposable
{
    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public SettlementListNotablesCommandTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void Describe_ListsOnlyNotablesOfTheSettlementWithIdAndName()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var otherSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        var commonerId = TestEnvironment.CreateRegisteredObject<Hero>();
        var otherNotableId = TestEnvironment.CreateRegisteredObject<Hero>();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Client.ObjectManager.TryGetObject<Settlement>(otherSettlementId, out var otherSettlement));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(commonerId, out var commoner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(otherNotableId, out var otherNotable));

            using (new AllowedThread())
            {
                settlement._name = new TextObject("Test Town");
                notable.SetName(new TextObject("Test Notable"), new TextObject("Test"));
                notable.Occupation = Occupation.GangLeader;
                notable.StayingInSettlement = settlement;
                commoner.StayingInSettlement = settlement;
                otherNotable.Occupation = Occupation.Merchant;
                otherNotable.StayingInSettlement = otherSettlement;
            }

            var command = new SettlementListNotablesCoopCommand(Client.ObjectManager);

            var output = command.Describe(settlement);

            Assert.Contains("1 notables", output);
            Assert.Contains($"ID: '{notableId}', Name: 'Test Notable', Occupation: GangLeader, Issue: none", output);
            Assert.DoesNotContain(commonerId, output);
            Assert.DoesNotContain(otherNotableId, output);
        });
    }

    [Fact]
    public void Command_IsClientSideWithNoArguments()
    {
        var command = new SettlementListNotablesCoopCommand(Client.ObjectManager);

        Assert.Equal(CoopCommandSide.Client, command.Side);
        Assert.Equal("coop.debug.settlements", command.Prefix);
        Assert.Equal("list_notables", command.Name);
        Assert.Empty(command.ExpectedArgs);
    }
}
