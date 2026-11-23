using Common.Util;
using E2E.Tests.Environment;
using GameInterface.Services.MobileParties.Extensions;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MobileParties;

/// <summary>Covers the client menu refresh that follows a forced sea state change on the main party.</summary>
public class ForcedPositionMenuRefreshTests : IDisposable
{
    private static readonly Vec2 ForcedPosition = new Vec2(40f, 50f);

    private readonly E2ETestEnvironment environment;

    /// <summary>Builds a fresh server and client environment per test.</summary>
    public ForcedPositionMenuRefreshTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    /// <summary>Tears down the environment built for the test.</summary>
    public void Dispose()
    {
        environment.Dispose();
    }

    /// <summary>A forced sea state change still applies when MapScreen hasnt bound the open menu yet.</summary>
    [Fact]
    public void ForcedSeaStateChange_MenuWithoutHandler_AppliesWithoutRefresh()
    {
        RunForcedSeaStateChange(handler: null);
    }

    /// <summary>A bound settlement menu still refreshes its options when the sea state is forced.</summary>
    [Fact]
    public void ForcedSeaStateChange_BoundMenu_RefreshesOptions()
    {
        var handler = new Mock<IMenuContextHandler>();

        RunForcedSeaStateChange(handler.Object);

        handler.Verify(h => h.OnMenuRefresh(), Times.AtLeastOnce);
    }

    /// <summary>Seeds a client main party with an open menu, forces it to sea and checks the state applied.</summary>
    private void RunForcedSeaStateChange(IMenuContextHandler handler)
    {
        var client = environment.Clients.First();
        var partyId = environment.CreateRegisteredObject<MobileParty>();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject(partyId, out MobileParty party));
            Campaign.Current.MainParty = party;
            Assert.False(party.IsCurrentlyAtSea);

            var states = Game.Current.GameStateManager;
            var map = states.CreateState<MapState>();
            states._gameStates.Add(map);
            map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
            map._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
            map._menuContext.Handler = handler;

            party.ApplyForcedPosition(new CampaignVec2(ForcedPosition, isOnLand: false), isCurrentlyAtSea: true);

            Assert.True(party.IsCurrentlyAtSea);
            Assert.Equal(ForcedPosition, party.Position.ToVec2());
        });
    }
}
