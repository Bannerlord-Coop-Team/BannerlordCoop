using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.AnchorPoints.Patches;
using GameInterface.Services.Settlements.Interfaces;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.AnchorPoints;

/// <summary>Covers the client menu refreshes that follow a change to the main party's anchor.</summary>
public class AnchorPointMenuRefreshTests : IDisposable
{
    private static readonly CampaignVec2 PortPosition = new CampaignVec2(new Vec2(30f, 24f), false);

    private readonly E2ETestEnvironment environment;

    /// <summary>Builds a fresh server and client environment per test.</summary>
    public AnchorPointMenuRefreshTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    /// <summary>Tears down the environment built for the test.</summary>
    public void Dispose()
    {
        environment.Dispose();
    }

    /// <summary>An anchor update reaching a menu MapScreen hasnt bound yet is skipped instead of throwing.</summary>
    [Fact]
    public void ArrivalTimeChange_MenuWithoutHandler_SkipsRefresh()
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        RunWithMainPartyMenu(partyId, handler: null, anchor => AnchorPointPatches.ArrivalTimeSetterPostfix(anchor));
    }

    /// <summary>A bound port menu still refreshes when the main party's anchor changes.</summary>
    [Fact]
    public void ArrivalTimeChange_BoundMenu_RefreshesOptions()
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var handler = new Mock<IMenuContextHandler>();

        RunWithMainPartyMenu(partyId, handler.Object, anchor => AnchorPointPatches.ArrivalTimeSetterPostfix(anchor));

        handler.Verify(h => h.OnMenuRefresh(), Times.Once);
    }

    /// <summary>The hourly call fleet refresh skips a menu MapScreen hasnt bound yet instead of throwing.</summary>
    [Fact]
    public void HourlyTick_FleetMovingAndMenuWithoutHandler_SkipsRefresh()
    {
        var partyId = CreatePartyWithFleetOnItsWay();

        RunWithMainPartyMenu(partyId, handler: null, RunHourlyTick);
    }

    /// <summary>The hourly tick still refreshes a bound menu while the fleet is on its way.</summary>
    [Fact]
    public void HourlyTick_FleetMovingAndBoundMenu_RefreshesOptions()
    {
        var partyId = CreatePartyWithFleetOnItsWay();
        var handler = new Mock<IMenuContextHandler>();

        RunWithMainPartyMenu(partyId, handler.Object, RunHourlyTick);

        handler.Verify(h => h.OnMenuRefresh(), Times.Once);
    }

    /// <summary>A port entry the server applies refreshes the client's open menu after the docked anchor reached it.</summary>
    [Fact]
    public void ServerPortEntry_ClientAlreadyAshore_RefreshesMenuWithDockedAnchor()
    {
        var server = environment.Server;
        var client = environment.Clients.First();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var townId = environment.CreateRegisteredObject<Settlement>();
        var hullId = environment.CreateRegisteredObject<ShipHull>();

        // The town, its port and the hull are template data every machine loads the same
        foreach (var instance in environment.Clients.Append(server))
        {
            instance.Call(() =>
            {
                var town = instance.GetRegisteredObject<Settlement>(townId);
                using (new AllowedThread())
                    town.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
                town.SetPortPosition(PortPosition);
                var hull = instance.GetRegisteredObject<ShipHull>(hullId);
                hull.MaxHitPoints = 100;
                hull.AvailableSlots = new Dictionary<string, ShipSlot>().GetReadOnlyDictionary();
            });
        }

        server.Call(() =>
        {
            var party = server.GetRegisteredObject<MobileParty>(partyId);
            ChangeShipOwnerAction.ApplyByProduction(party.Party, new Ship(server.GetRegisteredObject<ShipHull>(hullId)));
            party._isCurrentlyAtSea = true;
        });
        environment.FlushCoalescer();
        client.PumpGameThread();

        var handler = new Mock<IMenuContextHandler>();
        var dockedAtRefresh = new List<bool>();
        client.Call(() =>
        {
            var party = OpenMainPartyMenu(client, partyId, handler.Object);
            // The client started its own encounter, which already put its party ashore
            party._isCurrentlyAtSea = false;
            var town = client.GetRegisteredObject<Settlement>(townId);
            handler.Setup(h => h.OnMenuRefresh())
                .Callback(() => dockedAtRefresh.Add(MobileParty.MainParty.Anchor.IsAtSettlement(town)));
        });

        server.Call(() =>
        {
            var party = server.GetRegisteredObject<MobileParty>(partyId);
            server.Resolve<ISettlementInterface>().PartyEnterSettlement(party, server.GetRegisteredObject<Settlement>(townId));
            Assert.True(party.Anchor.IsAtSettlement(server.GetRegisteredObject<Settlement>(townId)));
        });
        environment.FlushCoalescer();
        client.PumpGameThread();

        Assert.NotEmpty(dockedAtRefresh);
        Assert.True(dockedAtRefresh.Last());
    }

    /// <summary>Creates a party whose fleet the server has sent on its way, replicated to the clients.</summary>
    private string CreatePartyWithFleetOnItsWay()
    {
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        environment.Server.Call(() =>
        {
            Assert.True(environment.Server.ObjectManager.TryGetObject(partyId, out MobileParty party));
            // The harness patches CampaignTime.Now to zero while IsPast reads the real tracker, so only Never stays future
            party.Anchor.ArrivalTime = CampaignTime.Never;
        });
        foreach (var client in environment.Clients)
        {
            client.PumpGameThread();
        }

        return partyId;
    }

    /// <summary>Runs the hourly menu postfix for a main party whose fleet is on its way.</summary>
    private static void RunHourlyTick(AnchorPoint anchor)
    {
        Assert.True(anchor.IsMovingToPoint);

        AnchorPointMenuRefreshPatch.Postfix(Campaign.Current.CurrentMenuContext);
    }

    /// <summary>Makes the party the client's main party with an open menu, then runs the given anchor action.</summary>
    private void RunWithMainPartyMenu(string partyId, IMenuContextHandler handler, Action<AnchorPoint> act)
    {
        var client = environment.Clients.First();

        client.Call(() => act(OpenMainPartyMenu(client, partyId, handler).Anchor));
    }

    /// <summary>Call inside client.Call. Makes the party the client's main party and opens a menu bound to the handler.</summary>
    private static MobileParty OpenMainPartyMenu(EnvironmentInstance client, string partyId, IMenuContextHandler handler)
    {
        Assert.True(client.ObjectManager.TryGetObject(partyId, out MobileParty party));
        Campaign.Current.MainParty = party;

        var states = Game.Current.GameStateManager;
        var map = states.CreateState<MapState>();
        states._gameStates.Add(map);
        map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
        map._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
        map._menuContext.Handler = handler;

        return party;
    }
}
