using Coop.Tests.Stubs;
using GameInterface.Services.MapTracks.Interfaces;
using GameInterface.Services.ObjectManager;
using Moq;
using TaleWorlds.CampaignSystem.Party;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapTracks;

/// <summary>
/// Covers what a player rejoining does to the server's record of the tracks they have already spotted.
/// </summary>
public class MapTracksReconnectInitializationTests
{
    private const string PlayerPartyId = "party1";

    private readonly Mock<IObjectManager> objectManager = new();
    private readonly MapTracksCampaignBehaviorInterface mapTracksInterface;

    public MapTracksReconnectInitializationTests()
    {
        // playerManager is only reached through GetPlayerParties, which none of these paths touch
        mapTracksInterface = new MapTracksCampaignBehaviorInterface(
            objectManager.Object,
            new StubMessageBroker(),
            playerManager: null);
    }

    [Fact]
    public void AddPlayerPartyKeys_OnFirstJoin_CreatesTheRecord()
    {
        Assert.True(mapTracksInterface.AddPlayerPartyKeys(PlayerPartyId));
    }

    /// <summary>
    /// The reconnect case. Reporting false is the server saying it kept the player's existing spotted
    /// set, which is what stops the following detection pass re-awarding xp for all of it.
    /// </summary>
    [Fact]
    public void AddPlayerPartyKeys_OnRejoin_KeepsTheExistingRecord()
    {
        mapTracksInterface.AddPlayerPartyKeys(PlayerPartyId);

        Assert.False(mapTracksInterface.AddPlayerPartyKeys(PlayerPartyId));
    }

    /// <summary>A reconnect loop must not keep resetting the record either.</summary>
    [Fact]
    public void AddPlayerPartyKeys_OnRepeatedRejoins_KeepsTheExistingRecord()
    {
        mapTracksInterface.AddPlayerPartyKeys(PlayerPartyId);

        Assert.False(mapTracksInterface.AddPlayerPartyKeys(PlayerPartyId));
        Assert.False(mapTracksInterface.AddPlayerPartyKeys(PlayerPartyId));
    }

    /// <summary>Separate players keep separate records; one joining must not adopt another's.</summary>
    [Fact]
    public void AddPlayerPartyKeys_ForADifferentPlayer_CreatesItsOwnRecord()
    {
        mapTracksInterface.AddPlayerPartyKeys(PlayerPartyId);

        Assert.True(mapTracksInterface.AddPlayerPartyKeys("party2"));
    }

    /// <summary>
    /// The join handler sends whatever this returns straight to the peer, so an unresolvable party has
    /// to come back as an empty set rather than null.
    /// </summary>
    [Fact]
    public void InitializePlayerVisibleTracks_WhenPartyDoesNotResolve_ReturnsEmpty()
    {
        var unresolvedId = (string)null;
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(It.IsAny<MobileParty>(), out unresolvedId))
            .Returns(false);

        var visibleTracks = mapTracksInterface.InitializePlayerVisibleTracks(behavior: null, playerParty: null);

        Assert.NotNull(visibleTracks);
        Assert.Empty(visibleTracks);
    }

    /// <summary>
    /// A party the server has no record for yet resolves, but has nothing to resend. It must not fall
    /// through to detection against a behavior it was never given.
    /// </summary>
    [Fact]
    public void InitializePlayerVisibleTracks_WhenPlayerHasNoRecord_ReturnsEmpty()
    {
        var resolvedId = PlayerPartyId;
        objectManager
            .Setup(manager => manager.TryGetIdWithLogging(It.IsAny<MobileParty>(), out resolvedId))
            .Returns(true);

        var visibleTracks = mapTracksInterface.InitializePlayerVisibleTracks(behavior: null, playerParty: null);

        Assert.NotNull(visibleTracks);
        Assert.Empty(visibleTracks);
    }
}
