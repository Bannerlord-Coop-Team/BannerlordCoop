using Common.Network;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Party.Data;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Data;
using GameInterface.Services.Villages.Interfaces;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Party;

/// <summary>
/// The volunteers screen after forcing a village: the server's set aside pool is the whole left side.
/// </summary>
public class PartyScreenCommitForceTransferTests : IDisposable
{
    private const int PoolSize = 4;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.Single();

    public PartyScreenCommitForceTransferTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output, numClients: 1);
    }

    // A double Done sends the same commit twice, the pool is consumed by the first one.
    [Fact]
    public void ForceTransferCommit_GivesThePoolOnce_AndRejectsTheSecondDone()
    {
        string mainHeroId = null, partyId = null, troopId = null, requestId = null;
        Server.Call(() =>
        {
            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var troop = GameObjectCreator.CreateInitializedObject<CharacterObject>();
            if (party.LeaderHero.PartyBelongedTo == null) party.LeaderHero.PartyBelongedTo = party;
            Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out mainHeroId));
            Assert.True(Server.ObjectManager.TryGetId(party, out partyId));
            Assert.True(Server.ObjectManager.TryGetId(troop, out troopId));

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("ForceTransferControllerId", mainHeroId, partyId, string.Empty, string.Empty)));
            playerManager.SetPeer("ForceTransferControllerId", Client.NetPeer);
            requestId = Server.Resolve<IVillageHostileActionInterface>()
                .AuthorizeForceTransfer(VillageHostileAction.ForceVolunteers, partyId, "village", null, troopId, PoolSize)
                .RequestId;
        });
        TestEnvironment.FlushCoalescer();

        var take = PartyScreenCommits.Transfer(
            PartyScreenLogic.PartyRosterSide.Left, PartyScreenLogic.TroopType.Member, Server.GetHandle<CharacterObject>(troopId), PoolSize);
        var message = PartyScreenCommits.Create(
            mainHeroId,
            Helpers.PartyScreenHelper.PartyScreenMode.Loot,
            PartyScreenDoneKind.Default,
            new[] { take },
            forceTransferId: requestId);

        Send(message);
        Assert.Equal(PoolSize, ServerTroopCount(partyId, troopId));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<SendInformationMessage>());

        Send(message);
        Assert.Equal(PoolSize, ServerTroopCount(partyId, troopId));
        Assert.Contains("no recruits left", Assert.Single(Server.NetworkSentMessages.GetMessages<SendInformationMessage>()).Text);
    }

    private int ServerTroopCount(string partyId, string troopId)
    {
        int count = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            count = party.MemberRoster.GetTroopCount(troop);
        });
        return count;
    }

    private void Send(GameInterface.Services.Party.Messages.NetworkCommitPartyScreen message)
    {
        Client.Call(() => Client.Resolve<INetwork>().SendAll(message));
        TestEnvironment.FlushCoalescer();
    }

    public void Dispose() => TestEnvironment.Dispose();
}
