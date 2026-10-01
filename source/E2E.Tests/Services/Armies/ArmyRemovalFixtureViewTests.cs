#if DEBUG
using Common.Commands;
using E2E.Tests.Environment;
using TaleWorlds.CampaignSystem;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

public class ArmyRemovalFixtureViewTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    public ArmyRemovalFixtureViewTests(ITestOutputHelper output) => environment = new E2ETestEnvironment(output);
    public void Dispose() => environment.Dispose();

    [Fact]
    public void MissingViewSubject_RejectsWithoutChangingTheRegisteredArmy()
    {
        var armyId = environment.CreateRegisteredObject<Army>();
        foreach (var instance in environment.Clients)
        {
            instance.Call(() =>
            {
                var army = instance.GetRegisteredObject<Army>(armyId);
                var leader = army.LeaderParty;
                var result = instance.Resolve<ICoopCommandRegistry>().ProcessCommand(
                    "coop.debug.army.removal_fixture_view",
                    new CoopCommandArgsFactory().FromValues(new[] { "show", "missing-army-view-party" }));
                Assert.False(result.Succeeded);
                Assert.Equal("command_failed", result.ErrorCode);
                Assert.Equal("The named lord party is missing.", result.Output);
                Assert.Same(army, instance.GetRegisteredObject<Army>(armyId));
                Assert.Same(army, leader.Army);
                Assert.Contains(leader, army.Parties);
            });
        }
    }
}
#endif
