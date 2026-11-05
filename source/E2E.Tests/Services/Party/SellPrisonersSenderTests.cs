using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.GameMenus.Messages;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Interfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

public class SellPrisonersSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;

    public SellPrisonersSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    // The server sells only the prisoners of the sender's own party; a completed sale refreshes its backstreet menu.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SellPrisoners_AppliesOnlyForTheSendersOwnParty(bool ownParty)
    {
        var client = environment.Clients.First();
        SellPrisoners sale = default;
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var otherParty = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("Seller", heroId, partyId, null, null)));
            playerManager.SetPeer("Seller", client.NetPeer);

            var rosterData = Server.Resolve<ITroopRosterInterface>().PackTroopRosterData(TroopRoster.CreateDummyTroopRoster());
            sale = new SellPrisoners(Server.GetHandle((ownParty ? party : otherParty).Party), rosterData);
        });
        Server.NetworkSentMessages.Clear();

        Server.SimulateMessage(client.NetPeer, sale);
        Server.PumpGameThread();

        Assert.Equal(ownParty ? 1 : 0, Server.NetworkSentMessages.GetMessages<RefreshGameMenu>().Count());
    }
}
