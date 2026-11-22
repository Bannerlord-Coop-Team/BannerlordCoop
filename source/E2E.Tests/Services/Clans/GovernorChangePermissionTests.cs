using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Actions.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Clans;

public class GovernorChangePermissionTests : SyncTestBase
{
    private readonly string clanId;
    private readonly string leaderHeroId;
    private readonly string memberHeroId;
    private readonly string governorId;
    private readonly string settlementId;
    private readonly string townId;
    private readonly EnvironmentInstance leaderClient;
    private readonly EnvironmentInstance memberClient;

    public GovernorChangePermissionTests(ITestOutputHelper output) : base(output)
    {
        clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        leaderHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        memberHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        governorId = TestEnvironment.CreateRegisteredObject<Hero>();
        settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        townId = TestEnvironment.CreateRegisteredObject<Town>();
        leaderClient = Clients.First();
        memberClient = Clients.Skip(1).First();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() => SetUpClanTown(instance));
        }

        RegisterPlayer(leaderClient, "leader", leaderHeroId, clanId);
        // The member only plays a hero in the clan, the clan itself belongs to the leader.
        RegisterPlayer(memberClient, "member", memberHeroId, null);
    }

    [Fact]
    public void ChangeGovernor_FromClanLeader_AppointsGovernor()
    {
        SendFrom(leaderClient, new ChangeGovernor(townId, governorId));

        Server.Call(() => Assert.Same(Get<Hero>(governorId), Get<Town>(townId).Governor));
    }

    [Fact]
    public void ChangeGovernor_FromClanMemberWhoDoesNotLeadIt_IsIgnored()
    {
        SendFrom(memberClient, new ChangeGovernor(townId, governorId));

        Server.Call(() =>
        {
            Assert.Null(Get<Town>(townId).Governor);
            Assert.Null(Get<Hero>(governorId).GovernorOf);
        });
    }

    [Fact]
    public void RemoveGovernor_FromClanLeader_RemovesGovernor()
    {
        AppointGovernorOnServer();

        SendFrom(leaderClient, new RemoveGovernor(governorId));

        Server.Call(() =>
        {
            Assert.Null(Get<Town>(townId).Governor);
            Assert.Null(Get<Hero>(governorId).GovernorOf);
        });
    }

    [Fact]
    public void RemoveGovernor_FromClanMemberWhoDoesNotLeadIt_IsIgnored()
    {
        AppointGovernorOnServer();

        SendFrom(memberClient, new RemoveGovernor(governorId));

        Server.Call(() =>
        {
            Assert.Same(Get<Hero>(governorId), Get<Town>(townId).Governor);
            Assert.Same(Get<Town>(townId), Get<Hero>(governorId).GovernorOf);
        });
    }

    private void SendFrom<T>(EnvironmentInstance sender, T message) where T : Common.Messaging.IMessage
    {
        Server.SimulateMessage(sender.NetPeer, message);
        Server.PumpGameThread();
    }

    private void AppointGovernorOnServer()
    {
        Server.Call(() =>
        {
            using (new AllowedThread())
            {
                Get<Town>(townId).Governor = Get<Hero>(governorId);
            }
        });
    }

    private void RegisterPlayer(EnvironmentInstance client, string controllerId, string heroId, string? playerClanId)
    {
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, heroId, null, playerClanId, null)));
            playerManager.SetPeer(controllerId, client.NetPeer);
        });
    }

    private void SetUpClanTown(EnvironmentInstance instance)
    {
        var clan = instance.GetRegisteredObject<Clan>(clanId);
        var leader = instance.GetRegisteredObject<Hero>(leaderHeroId);
        var settlement = instance.GetRegisteredObject<Settlement>(settlementId);
        var town = instance.GetRegisteredObject<Town>(townId);

        using (new AllowedThread())
        {
            leader.Clan = clan;
            instance.GetRegisteredObject<Hero>(memberHeroId).Clan = clan;
            instance.GetRegisteredObject<Hero>(governorId).Clan = clan;
            // Waiting in the town lets vanilla appoint the governor at once instead of sending them there.
            instance.GetRegisteredObject<Hero>(governorId).StayingInSettlement = settlement;
            clan.SetLeader(leader);

            settlement.Town = town;
            town.Owner = settlement.Party;
            town._ownerClan = clan;
        }
    }

    private T Get<T>(string id) where T : class => Server.GetRegisteredObject<T>(id);
}
