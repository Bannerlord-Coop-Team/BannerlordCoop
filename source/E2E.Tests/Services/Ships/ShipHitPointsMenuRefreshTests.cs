using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.Ships.Patches;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Ships;

/// <summary>Covers the client menu refresh that follows a change to the main party's ship hit points.</summary>
public class ShipHitPointsMenuRefreshTests : IDisposable
{
    private readonly E2ETestEnvironment environment;

    /// <summary>Builds a fresh server and client environment per test.</summary>
    public ShipHitPointsMenuRefreshTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    /// <summary>Tears down the environment built for the test.</summary>
    public void Dispose()
    {
        environment.Dispose();
    }

    /// <summary>A menu context MapScreen hasnt bound yet, as at mission teardown, is skipped instead of throwing.</summary>
    [Fact]
    public void HitPointsChange_MenuWithoutHandler_SkipsRefresh()
    {
        RunWithOwnedShipAndMenu(handler: null);
    }

    /// <summary>A bound port menu still refreshes its options when the main party's ship hit points change.</summary>
    [Fact]
    public void HitPointsChange_BoundMenu_RefreshesOptions()
    {
        var handler = new Mock<IMenuContextHandler>();

        RunWithOwnedShipAndMenu(handler.Object);

        handler.Verify(h => h.OnMenuRefresh(), Times.Once);
    }

    /// <summary>Seeds a client main party that owns a ship and has an open menu, then runs the hit points postfix.</summary>
    private void RunWithOwnedShipAndMenu(IMenuContextHandler handler)
    {
        var client = environment.Clients.First();
        var shipId = environment.CreateRegisteredObject<Ship>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject(shipId, out Ship ship));
            Assert.True(client.ObjectManager.TryGetObject(partyId, out MobileParty party));
            ship._owner = party.Party;
            Campaign.Current.MainParty = party;

            var states = Game.Current.GameStateManager;
            var map = states.CreateState<MapState>();
            states._gameStates.Add(map);
            map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
            map._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
            map._menuContext.Handler = handler;

            ShipHitPointsPatches.HitPointsSetterPostfix(ship);
        });
    }
}
