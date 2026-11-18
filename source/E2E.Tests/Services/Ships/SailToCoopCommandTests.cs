using Common;
using Common.Commands;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Party.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

/// <summary>
/// coop.debug.naval.sail_to gives a player's party at sea the order a map click on a port gives, on every machine.
/// </summary>
public class SailToCoopCommandTests : MapEventTestBase
{
    private const string ControllerId = "sailing-owner";

    private static readonly CampaignVec2 SeaPosition = new CampaignVec2(new Vec2(20f, 20f), false);
    private static readonly CampaignVec2 PortPosition = new CampaignVec2(new Vec2(30f, 24f), false);

    /// <summary>Passes the test output to the environment.</summary>
    public SailToCoopCommandTests(ITestOutputHelper output) : base(output) { }

    // The port order, its IsTargetingPort flag and naval navigation reach every machine, the client that owns the party included
    [Fact]
    public void SailTo_OrdersThePartyIntoThePortOnEveryMachine()
    {
        var (_, partyId) = CreatePlayerHeroParty(ControllerId);
        Clients.First().Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), ControllerId);
        var portId = TestEnvironment.CreateRegisteredObject<Settlement>();

        // The port position is template data every machine loads the same
        foreach (var instance in Clients.Append(Server))
            instance.Call(() => instance.GetRegisteredObject<Settlement>(portId).SetPortPosition(PortPosition));

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.IsActive = true;
            party._isCurrentlyAtSea = true;
            party._position = SeaPosition;
        });
        TestEnvironment.FlushCoalescer();

        var sailTo = ResolveCommand<PartySailCommands.SailToCoopCommand>(Server);
        var args = new CoopCommandArgsFactory().FromValues(new[] { ControllerId, portId });
        CoopCommandResult result = null!;
        Server.Call(() => result = RunWithNaval(() => sailTo.ProcessCommand(args)));
        TestEnvironment.FlushCoalescer();

        Assert.True(result.Succeeded, result.Output);
        foreach (var instance in Clients.Append(Server))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Equal(AiBehavior.GoToSettlement, party.DefaultBehavior);
                Assert.Same(instance.GetRegisteredObject<Settlement>(portId), party.TargetSettlement);
                Assert.True(party.IsTargetingPort);
                Assert.Equal(MobileParty.NavigationType.Naval, party.DesiredAiNavigationType);
            });
        }
    }

    // Resolves a registered debug command from the instance container so tests do not depend on its constructor shape.
    private static T ResolveCommand<T>(EnvironmentInstance instance) where T : ICoopCommand =>
        instance.Resolve<IEnumerable<ICoopCommand>>().OfType<T>().Single();

    // sail_to refuses to run without NavalDLC, which the test process doesn't load.
    private static CoopCommandResult RunWithNaval(Func<CoopCommandResult> run)
    {
        bool originalNavalEnabled = ModInformation.IsNavalEnabled;
        try
        {
            ModInformation.IsNavalEnabled = true;
            return run();
        }
        finally
        {
            ModInformation.IsNavalEnabled = originalNavalEnabled;
        }
    }
}
