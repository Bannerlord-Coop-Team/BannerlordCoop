using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

/// <summary>The server removes a party from an army only on the request of that party's player or the army leader's.</summary>
public class ArmyRequestSenderTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Member => environment.Clients.First();
    private EnvironmentInstance Outsider => environment.Clients.Last();

    private readonly string armyId;
    private string leaderPartyId;
    private readonly string memberPartyId;

    public ArmyRequestSenderTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        armyId = environment.CreateRegisteredObject<Army>();
        memberPartyId = environment.CreateRegisteredObject<MobileParty>();
        var outsiderPartyId = environment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var army = Get<Army>(armyId);
            var member = Get<MobileParty>(memberPartyId);
            member.Army = army;
            member.AttachedTo = army.LeaderParty;
            Assert.True(Server.ObjectManager.TryGetId(army.LeaderParty, out string leaderId));
            leaderPartyId = leaderId;

            Register("Member", Get<MobileParty>(memberPartyId), Member);
            Register("Outsider", Get<MobileParty>(outsiderPartyId), Outsider);
        });
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartyIsRemovedFromTheArmyOnlyOnItsOwnPlayersRequest(bool fromMember)
    {
        Send(fromMember ? Member : Outsider, new NetworkRemovePartyInArmy(armyId, memberPartyId, string.Empty));

        Server.Call(() => Assert.Equal(fromMember, Get<MobileParty>(memberPartyId).Army == null));
    }

    private void Register(string controllerId, MobileParty party, EnvironmentInstance client)
    {
        Assert.True(Server.ObjectManager.TryGetId(party, out var partyId));
        Assert.True(Server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
        var playerManager = Server.Resolve<IPlayerManager>();
        Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, partyId, null, null)));
        playerManager.SetPeer(controllerId, client.NetPeer);
    }

    private void Send<T>(EnvironmentInstance sender, T message) where T : Common.Messaging.IMessage
    {
        Server.SimulateMessage(sender.NetPeer, message);
        Server.PumpGameThread();
    }

    private T Get<T>(string id) where T : class => Server.GetRegisteredObject<T>(id);
}
