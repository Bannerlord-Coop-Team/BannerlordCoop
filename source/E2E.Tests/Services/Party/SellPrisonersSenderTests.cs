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

/// <summary>The server only sells the prisoners of the sending player's own party.</summary>
public class SellPrisonersSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;

    public SellPrisonersSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SellPrisoners_AppliesOnlyForTheSendersOwnParty(bool fromOwner)
    {
        var owner = environment.Clients.First();
        var outsider = environment.Clients.Last();
        string ownerPartyId = RegisterPlayerParty(owner, "Owner");
        RegisterPlayerParty(outsider, "Outsider");

        SellPrisoners sale = default;
        Server.Call(() =>
        {
            var party = Server.GetRegisteredObject<MobileParty>(ownerPartyId);
            var rosterData = Server.Resolve<ITroopRosterInterface>().PackTroopRosterData(TroopRoster.CreateDummyTroopRoster());
            sale = new SellPrisoners(Server.GetHandle(party.Party), rosterData);
        });
        Server.NetworkSentMessages.Clear();

        Server.SimulateMessage((fromOwner ? owner : outsider).NetPeer, sale);
        Server.PumpGameThread();

        // A completed sale refreshes the seller's backstreet menu
        Assert.Equal(fromOwner ? 1 : 0, Server.NetworkSentMessages.GetMessages<RefreshGameMenu>().Count());
    }

    private string RegisterPlayerParty(EnvironmentInstance client, string controllerId)
    {
        string partyId = null;
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, partyId, null, null)));
            playerManager.SetPeer(controllerId, client.NetPeer);
        });
        return partyId;
    }
}
