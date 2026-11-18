using Common;
using Common.Commands;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Party.Commands;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

/// <summary>
/// The coop.debug.naval sail commands give a player's party the same port orders gameplay gives, on every machine.
/// </summary>
public class PartySailCommandsTests : MapEventTestBase
{
    private const string ControllerId = "sailing-owner";

    private static readonly CampaignVec2 SeaPosition = new CampaignVec2(new Vec2(20f, 20f), false);
    private static readonly CampaignVec2 PortPosition = new CampaignVec2(new Vec2(30f, 24f), false);

    /// <summary>Passes the test output to the environment.</summary>
    public PartySailCommandsTests(ITestOutputHelper output) : base(output) { }

    // The port order, its IsTargetingPort flag and naval navigation reach every machine, the client that owns the party included
    [Fact]
    public void SailTo_OrdersThePartyIntoThePortOnEveryMachine()
    {
        var (partyId, portId) = CreatePlayerPartyAndPort();

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

    // The port is read before the leave clears CurrentSettlement, otherwise the party would leave without setting sail
    [Fact]
    public void SetSail_LeavesThePortAtSeaOnEveryMachine()
    {
        var (partyId, portId) = CreatePlayerPartyAndPort();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var port = instance.GetRegisteredObject<Settlement>(portId);
                using (new AllowedThread())
                {
                    port.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
                    instance.GetRegisteredObject<MobileParty>(partyId).CurrentSettlement = port;
                }
            });
        }
        Server.Call(() =>
        {
            UseFleetNavigation();
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.IsActive = true;
            party.Anchor.Settlement = Server.GetRegisteredObject<Settlement>(portId);
        });
        TestEnvironment.FlushCoalescer();

        var setSail = ResolveCommand<PartySailCommands.SetSailCoopCommand>(Server);
        var args = new CoopCommandArgsFactory().FromValues(new[] { ControllerId });
        CoopCommandResult result = null!;
        Server.Call(() => result = RunWithNaval(() => setSail.ProcessCommand(args)));
        TestEnvironment.FlushCoalescer();

        Assert.True(result.Succeeded, result.Output);
        foreach (var instance in Clients.Append(Server))
        {
            instance.PumpGameThread();
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Null(party.CurrentSettlement);
                Assert.True(party.IsCurrentlyAtSea);
                Assert.Equal(PortPosition.ToVec2(), party.Position.ToVec2());
                Assert.Equal(AiBehavior.Hold, party.DefaultBehavior);
            });
        }
    }

    // Registers the first client's player party and a port every machine knows the position of.
    private (string partyId, string portId) CreatePlayerPartyAndPort()
    {
        var (_, partyId) = CreatePlayerHeroParty(ControllerId);
        Clients.First().Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Clients.First(), ControllerId);
        var portId = TestEnvironment.CreateRegisteredObject<Settlement>();

        // The port position is template data every machine loads the same
        foreach (var instance in Clients.Append(Server))
            instance.Call(() => instance.GetRegisteredObject<Settlement>(portId).SetPortPosition(PortPosition));

        return (partyId, portId);
    }

    // Call inside Server.Call. The headless party has no ships, so let the server's navigation model treat it as having a fleet.
    private void UseFleetNavigation()
    {
        var models = Campaign.Current.Models.GetGameModels().ToList();
        models.Add(new FleetNavigationModel());
        var gameModels = new GameModels(models);
        Server.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
        Campaign.Current._gameModels = gameModels;
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

    private sealed class FleetNavigationModel : DefaultPartyNavigationModel
    {
        public override bool HasNavalNavigationCapability(MobileParty mobileParty) => true;
    }
}
