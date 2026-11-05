using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Villages.Messages;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Villages;

/// <summary>The server applies villager and caravan conversation results only for the sending player's own party.</summary>
public class VillagerConversationSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Owner => environment.Clients.First();
    private EnvironmentInstance Outsider => environment.Clients.Last();

    private string ownerHeroId, ownerPartyId, outsiderHeroId, outsiderPartyId;

    public VillagerConversationSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        Server.Call(() =>
        {
            (ownerHeroId, ownerPartyId) = RegisterPlayer("Owner", Owner);
            (outsiderHeroId, outsiderPartyId) = RegisterPlayer("Outsider", Outsider);
        });
    }

    public void Dispose() => environment.Dispose();

    // Taking the villagers prisoner destroys their party, so a refused request leaves it on the map.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void VillagersTakenPrisoner_IsAppliedOnlyForTheSendersOwnParty(bool ownParty)
    {
        string villagerPartyId = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(GameObjectCreator.CreateInitializedObject<MobileParty>(), out villagerPartyId)));

        // The outsider names its own hero; the party is its own, or the owner's from a stale screen.
        Server.SimulateMessage(Outsider.NetPeer, new NetworkVillagersTookPrisonerOnConsequence(
            outsiderHeroId, ownParty ? outsiderPartyId : ownerPartyId, villagerPartyId, Array.Empty<ItemRosterElement>()));
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(!ownParty,
            Server.ObjectManager.TryGetObject<MobileParty>(villagerPartyId, out var villagers) && villagers.IsActive));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CaravanTakenPrisoner_IsAppliedOnlyForTheSendersOwnParty(bool ownParty)
    {
        string caravanPartyId = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(GameObjectCreator.CreateInitializedObject<MobileParty>(), out caravanPartyId)));

        Server.SimulateMessage(Outsider.NetPeer, new NetworkCaravanTookPrisonerOnConsequence(
            outsiderHeroId, ownParty ? outsiderPartyId : ownerPartyId, caravanPartyId, false, Array.Empty<ItemRosterElement>()));
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(!ownParty,
            Server.ObjectManager.TryGetObject<MobileParty>(caravanPartyId, out var caravan) && caravan.IsActive));
    }

    private (string heroId, string partyId) RegisterPlayer(string controllerId, EnvironmentInstance client)
    {
        var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
        Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
        Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
        var playerManager = Server.Resolve<IPlayerManager>();
        Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, partyId, null, null)));
        playerManager.SetPeer(controllerId, client.NetPeer);
        return (heroId, partyId);
    }
}
