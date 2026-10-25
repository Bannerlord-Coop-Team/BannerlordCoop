using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.NavalDLC;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Rafts.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Rafts;

/// <summary>
/// The server ending a player's raft at sea: the raft state ends everywhere and the cached fleet comes back without the
/// coast anchor cached at the stranding.
/// </summary>
public class PlayerRaftFleetInterfaceTests : SyncTestBase
{
    // Passes the test output to the environment.
    public PlayerRaftFleetInterfaceTests(ITestOutputHelper output) : base(output)
    {
    }

    // The naval scenario stager takes a rafted player off its raft at sea before the battle (R41b P4).
    [Fact]
    public void LeaveRaftAtSea_EndsTheRaftAndTakesTheCachedFleetUnanchored()
    {
        var (heroId, partyId) = CreateRaftedPlayer();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Server.Resolve<IPlayerRaftFleetInterface>().LeaveRaftAtSea(party);

            Assert.False(party.IsInRaftState);
            Assert.False(CachedFleets().ContainsKey(heroId));
            Assert.False(party.Anchor.IsValid);
            Assert.Equal(new Vec2(11f, 21f), party.Anchor.GetLastUsedDisembarkPosition().ToVec2());
        });
        foreach (var client in Clients)
        {
            client.PumpGameThread();
            client.Call(() => Assert.False(client.GetRegisteredObject<MobileParty>(partyId).IsInRaftState));
        }
    }

    // Only a rafted party has a raft to leave, so another party keeps the fleet cached for its player.
    [Fact]
    public void LeaveRaftAtSea_LeavesAPartyThatIsNotOnARaftAlone()
    {
        var (heroId, partyId) = CreateRaftedPlayer();

        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.IsInRaftState = false;
            Server.Resolve<IPlayerRaftFleetInterface>().LeaveRaftAtSea(party);

            Assert.True(CachedFleets().ContainsKey(heroId));
        });
    }

    // A registered player's party on a raft, with an empty fleet cached at a stranding whose anchor was on the coast.
    private (string heroId, string partyId) CreateRaftedPlayer()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player("rafted", heroId, partyId, "MyClanId", "MyCharacterObjectId")));
            Server.GetRegisteredObject<MobileParty>(partyId).IsInRaftState = true;
            CachedFleets()[heroId] = new PlayerCachedFleetData(new List<CachedShipData>(), 10f, 20f, true, 11f, 21f, true);
        });
        return (heroId, partyId);
    }

    // The server session's fleets cached per player hero id.
    private Dictionary<string, PlayerCachedFleetData> CachedFleets() =>
        Server.Resolve<ICoopSessionProvider>().CoopSession.NavalPlayerData.PlayerCachedFleets;
}
