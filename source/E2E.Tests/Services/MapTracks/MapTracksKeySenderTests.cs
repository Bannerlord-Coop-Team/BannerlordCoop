using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapTracks.Interfaces;
using GameInterface.Services.MapTracks.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapTracks;

/// <summary>The server sets up track keys only for the sending player's own party.</summary>
public class MapTracksKeySenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => environment.Clients.First();
    private EnvironmentInstance Outsider => environment.Clients.Last();

    private string ownerPartyId;

    public MapTracksKeySenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        SetUp();
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TrackKeys_AreAddedOnlyForTheSendersParty(bool fromOwner)
    {
        Server.SimulateMessage((fromOwner ? Owner : Outsider).NetPeer, new NetworkInitializePlayerTracksKeys(ownerPartyId));
        Server.PumpGameThread();

        // Adding the keys again only succeeds when the request above didn't add them.
        Server.Call(() => Assert.Equal(!fromOwner,
            Server.Resolve<IMapTracksCampaignBehaviorInterface>().AddPlayerPartyKeys(ownerPartyId)));
    }

    private void SetUp()
    {
        Server.Call(() =>
        {
            Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { new MapTracksCampaignBehavior() }));

            var ownerParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var outsiderParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(ownerParty, out ownerPartyId));
            Assert.True(Server.ObjectManager.TryGetId(ownerParty.LeaderHero, out var ownerHeroId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty, out var outsiderPartyId));
            Assert.True(Server.ObjectManager.TryGetId(outsiderParty.LeaderHero, out var outsiderHeroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Owner", ownerHeroId, ownerPartyId, null, null)));
            Assert.True(playerManager.AddPlayer(new Player("Outsider", outsiderHeroId, outsiderPartyId, null, null)));
            playerManager.SetPeer("Owner", Owner.NetPeer);
            playerManager.SetPeer("Outsider", Outsider.NetPeer);
        });
    }
}
