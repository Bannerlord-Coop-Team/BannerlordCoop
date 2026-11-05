using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Messages;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Villages;

public class VillagerConversationSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;

    public VillagerConversationSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    // Taking the villagers prisoner destroys their party, so a request naming another party (stale screen) leaves it on the map.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VillagersTakenPrisoner_IsAppliedOnlyForTheSendersOwnParty(bool ownParty)
    {
        var client = environment.Clients.First();
        string heroId = null, partyId = null, otherPartyId = null, villagerPartyId = null;
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            Assert.True(Server.ObjectManager.TryGetId(GameObjectCreator.CreateInitializedObject<MobileParty>(), out otherPartyId));
            Assert.True(Server.ObjectManager.TryGetId(GameObjectCreator.CreateInitializedObject<MobileParty>(), out villagerPartyId));
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Player", heroId, partyId, null, null)));
            playerManager.SetPeer("Player", client.NetPeer);
        });

        Server.SimulateMessage(client.NetPeer, new NetworkVillagersTookPrisonerOnConsequence(
            heroId, ownParty ? partyId : otherPartyId, villagerPartyId, Array.Empty<ItemRosterElement>()));
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(!ownParty,
            Server.ObjectManager.TryGetObject<MobileParty>(villagerPartyId, out var villagers) && villagers.IsActive));
    }
}
