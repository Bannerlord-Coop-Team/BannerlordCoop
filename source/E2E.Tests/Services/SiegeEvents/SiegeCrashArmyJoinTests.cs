using Common.Messaging;
using Common.Network.Messages;
using Common.Util;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using GameInterface.Services.Players;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
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
public class SiegeCrashArmyJoinTests : MapEventTestBase
{
    private static IReadOnlyList<MethodBase> SiegeCreationDisabledMethods => new[]
    {
        AccessTools.Method(typeof(MobileParty), nameof(MobileParty.OnPartyJoinedSiegeInternal)),
        AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)),
        AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)),
    };

    public SiegeCrashArmyJoinTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void ArmyLeaderCrashesMidSiege_MembersLeaveTheSiegeAndCanJoinANewArmy()
    {
        var army = CreatePlayerLedSiegeArmy();

        DisconnectPlayer(Clients.First(), "SiegeArmyLeader");

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(army.LeaderId, out var leader));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(army.FollowerId, out var follower));

                Assert.Null(leader.BesiegerCamp);
                Assert.Same(leader, follower.AttachedTo);
                // An attached party left in the camp its leader left can neither move nor join another army
                Assert.Null(follower.BesiegerCamp);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Army>(army.ArmyId, out var oldArmy));
            DisbandArmyAction.ApplyByUnknownReason(oldArmy);
        }, new[] { AccessTools.Method(typeof(PartyBase), nameof(PartyBase.UpdateVisibilityAndInspected)) });

        Server.Call(() =>
        {
            // The state DefaultArmyManagementCalculationModel.CheckPartyEligibility refuses a party for
            foreach (var partyId in new[] { army.FollowerId, army.OutsideSiegeMemberId })
            {
                Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Null(party.Army);
                Assert.Null(party.AttachedTo);
                Assert.Null(party.MapEvent);
                Assert.Null(party.SiegeEvent);
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
        var campId = SetupSiege(leaderId!, followerId, playerMemberId);

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

    private string SetupSiege(params string[] attackerPartyIds)
    {
        var siegeEventId = TestEnvironment.CreateRegisteredObject<SiegeEvent>(SiegeCreationDisabledMethods);
        ConfigureSiegeTeardownModels();
        string? campId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<SiegeEvent>(siegeEventId, out var siegeEvent));
            Assert.True(Server.ObjectManager.TryGetId(siegeEvent.BesiegerCamp, out campId));
        });
        Assert.NotNull(campId);

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<BesiegerCamp>(campId!, out var camp));
                camp._besiegerParties.Clear();

                foreach (var partyId in attackerPartyIds)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                    if (party.Ai == null)
                    {
                        using (new AllowedThread())
                        {
                            party.Ai = new MobilePartyAi(party);
                        }
                    }

                    party._besiegerCamp = camp;
                    camp._besiegerParties.Add(party);
                }

                camp._leaderParty = camp._besiegerParties[0];
                camp._faction = camp._leaderParty.MapFaction;
            });
        }

        return campId!;
    }

    private void ConfigureSiegeTeardownModels()
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var models = Campaign.Current.Models.GetGameModels().ToList();
                models.Add(new DefaultEncounterModel());
                models.Add(new DefaultPartyImpairmentModel());
                models.Add(new DefaultPartyNavigationModel());
                var gameModels = new GameModels(models);

                instance.GameInstance.Game._gameModelManagers[typeof(GameModels)] = gameModels;
                Campaign.Current._gameModels = gameModels;
            });
        }
    }

    private void DisconnectPlayer(EnvironmentInstance disconnectedClient, string controllerId)
    {
        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().SetPeer(controllerId, disconnectedClient.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(
                this,
                new PlayerDisconnected(disconnectedClient.NetPeer, default));
        });
    }

    private readonly record struct ArmyContext(
        string ArmyId,
        string CampId,
        string LeaderId,
        string FollowerId,
        string OutsideSiegeMemberId,
        string PlayerMemberId);
}
