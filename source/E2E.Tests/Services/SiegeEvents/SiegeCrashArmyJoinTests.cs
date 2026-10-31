using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.SiegeEvents;

/// <summary>
/// #4110: a client crash mid-siege parks its party on the server, the army it leads
/// must not be left split between the parked leader and the siege camp
/// </summary>
public class SiegeCrashArmyJoinTests : SiegeTestBase
{
    public SiegeCrashArmyJoinTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ArmyLeaderCrashesMidSiege_AiMembersLeaveTheSiegeAndCanJoinANewArmy()
    {
        var army = CreatePlayerLedSiegeArmy();

        DisconnectPlayer(Clients.First(), "SiegeArmyLeader");
        Server.PumpGameThread();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(army.LeaderId, out var leader));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(army.FollowerId, out var follower));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(army.OutsideSiegeMemberId, out var outsideSiegeMember));

                Assert.Null(leader.BesiegerCamp);
                Assert.Null(follower.BesiegerCamp);
                Assert.Null(follower.Army);
                Assert.Null(follower.AttachedTo);
                Assert.Null(outsideSiegeMember.Army);
                Assert.DoesNotContain(follower, leader.AttachedParties);
            });
        }

        // Another player calls them to a new army, the model then only refuses them for their empty rosters
        var recruiter = Clients.Last();
        var recruiterPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        recruiter.Call(() =>
        {
            Assert.True(recruiter.ObjectManager.TryGetObject<MobileParty>(recruiterPartyId, out var recruiterParty));
            Campaign.Current.MainParty = recruiterParty;
            var model = new DefaultArmyManagementCalculationModel();

            foreach (var partyId in new[] { army.FollowerId, army.OutsideSiegeMemberId })
            {
                Assert.True(recruiter.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                model.CheckPartyEligibility(party, out var explanation);
                // The size check comes after every army and fighting refusal
                Assert.StartsWith("{=SVJlOYCB}", explanation.Value);
            }
        });

        Server.PumpGameThread();
    }

    [Fact]
    public void ArmyLeaderCrashesMidSiege_OnlinePlayerMemberKeepsItsSiegeRole()
    {
        var army = CreatePlayerLedSiegeArmy();

        DisconnectPlayer(Clients.First(), "SiegeArmyLeader");

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(army.LeaderId, out var leader));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(army.PlayerMemberId, out var playerMember));
                Assert.True(instance.ObjectManager.TryGetObject<BesiegerCamp>(army.CampId, out var camp));

                Assert.Null(leader.BesiegerCamp);
                Assert.Same(camp, playerMember.BesiegerCamp);
                Assert.Contains(playerMember, camp._besiegerParties);
                Assert.NotNull(playerMember.Army);
                Assert.Same(leader, playerMember.Army.LeaderParty);
            });
        }

        Server.PumpGameThread();
    }

    private ArmyContext CreatePlayerLedSiegeArmy()
    {
        var armyId = TestEnvironment.CreateRegisteredObject<Army>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var followerId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var outsideSiegeMemberId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var (_, playerMemberId) = CreatePlayerHeroParty("SiegeArmyPlayerMember");
        string? leaderId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Army>(armyId, out var army));
            Assert.True(Server.ObjectManager.TryGetId(army.LeaderParty, out leaderId));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(followerId, out var follower));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(outsideSiegeMemberId, out var outsideSiegeMember));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerMemberId, out var playerMember));

            follower.Army = army;
            follower.AttachedTo = army.LeaderParty;
            outsideSiegeMember.Army = army;
            playerMember.Army = army;
            playerMember.AttachedTo = army.LeaderParty;
            Server.Resolve<IPlayerManager>().SetPeer("SiegeArmyPlayerMember", Clients.Last().NetPeer);
        });
        Assert.NotNull(leaderId);

        RegisterAsPlayerParty("SiegeArmyLeader", heroId, leaderId!);
        var campId = SetupSiege(leaderId!, followerId, playerMemberId).CampId;

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(leaderId!, out var leader));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(followerId, out var follower));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(outsideSiegeMemberId, out var outsideSiegeMember));

                Assert.Same(leader.Army, follower.Army);
                Assert.Same(leader.Army, outsideSiegeMember.Army);
                Assert.Same(leader, follower.AttachedTo);
                Assert.NotNull(follower.BesiegerCamp);
                Assert.Null(outsideSiegeMember.BesiegerCamp);
            });
        }

        return new ArmyContext(armyId, campId, leaderId!, followerId, outsideSiegeMemberId, playerMemberId);
    }

    private readonly record struct ArmyContext(
        string ArmyId,
        string CampId,
        string LeaderId,
        string FollowerId,
        string OutsideSiegeMemberId,
        string PlayerMemberId);
}
