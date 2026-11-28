using Common.Util;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.CoopSessionData;
using GameInterface.Services.BloodFeuds.Data;
using GameInterface.Services.BloodFeuds.Interfaces;
using GameInterface.Services.BloodFeuds.Messages;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BloodFeuds;

public class ExecutionCampaignBehaviorInterfaceTests : MapEventTestBase
{
    public ExecutionCampaignBehaviorInterfaceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PlayerExecutingAnotherPlayerClansMember_StartsTheFeudOnBothClans_AndRansomEndsBoth()
    {
        var (playerAId, _) = CreatePlayerHeroParty("PlayerA");
        var (playerBId, _) = CreatePlayerHeroParty("PlayerB");
        var clanAId = TestEnvironment.CreateRegisteredObject<Clan>();
        var clanBId = TestEnvironment.CreateRegisteredObject<Clan>();
        var victimId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var playerA = Server.GetRegisteredObject<Hero>(playerAId);
            var playerB = Server.GetRegisteredObject<Hero>(playerBId);
            var clanA = Server.GetRegisteredObject<Clan>(clanAId);
            var clanB = Server.GetRegisteredObject<Clan>(clanBId);
            var victim = Server.GetRegisteredObject<Hero>(victimId);

            using (new AllowedThread())
            {
                clanA.InitMembers();
                clanB.InitMembers();
                playerA.Clan = clanA;
                clanA.SetLeader(playerA);
                playerB.Clan = clanB;
                clanB.SetLeader(playerB);
                victim.Clan = clanB;
                playerA.Gold = 100000;
            }

            var executionInterface = Server.Resolve<IExecutionCampaignBehaviorInterface>();
            var feuds = Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData.PlayerClanBloodFeuds;

            executionInterface.OnHeroKilled(victim, playerA, KillCharacterAction.KillCharacterActionDetail.Executed);

            var executingSide = feuds[clanAId][clanBId];
            var executedSide = feuds[clanBId][clanAId];
            Assert.Equal((true, 0, 1), (executingSide.HasBloodFeud, executingSide.ExecutionsDoneCount, executingSide.ExecutionsReceivedCount));
            Assert.Equal((true, 1, 0), (executedSide.HasBloodFeud, executedSide.ExecutionsDoneCount, executedSide.ExecutionsReceivedCount));

            executionInterface.SettleBloodFeudByRansomPayment(playerA, clanB);

            Assert.Equal((false, 0, 0), (executingSide.HasBloodFeud, executingSide.ExecutionsDoneCount, executingSide.ExecutionsReceivedCount));
            Assert.Equal((false, 0, 0), (executedSide.HasBloodFeud, executedSide.ExecutionsDoneCount, executedSide.ExecutionsReceivedCount));
        });
    }

    [Fact]
    public void PlayerChangingClan_IsSentTheNewClansFeuds()
    {
        var (playerId, _) = CreatePlayerHeroParty("Joiner");
        var oldClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var newClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        Server.Call(() =>
        {
            var player = Server.GetRegisteredObject<Hero>(playerId);
            var oldClan = Server.GetRegisteredObject<Clan>(oldClanId);
            var newClan = Server.GetRegisteredObject<Clan>(newClanId);

            using (new AllowedThread())
            {
                oldClan.InitMembers();
                newClan.InitMembers();
                player.Clan = oldClan;
            }

            Server.Resolve<IPlayerManager>().SetPeer("Joiner", Clients.First().NetPeer);
            Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData.PlayerClanBloodFeuds[newClanId] = new()
            {
                [feudClanId] = new BloodFeudData { HasBloodFeud = true },
            };

            player.Clan = newClan;

            var update = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkUpdateBloodFeuds>());
            Assert.True(update.Feuds[feudClanId].HasBloodFeud);
        });
    }

    [Fact]
    public void ClanMemberMovingToAnotherFeudingPlayerClan_TakesTheirPendingSettlementExecutionAlong()
    {
        var (oldLeaderId, _) = CreatePlayerHeroParty("OldClanLeader");
        var (newLeaderId, _) = CreatePlayerHeroParty("NewClanLeader");
        var oldClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var newClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var memberId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var oldClan = Server.GetRegisteredObject<Clan>(oldClanId);
            var newClan = Server.GetRegisteredObject<Clan>(newClanId);
            var member = Server.GetRegisteredObject<Hero>(memberId);
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var town = GameObjectCreator.CreateInitializedObject<Town>();
            var captorClan = GameObjectCreator.CreateInitializedObject<Clan>();
            Assert.True(Server.ObjectManager.TryGetId(captorClan, out var captorClanId));

            using (new AllowedThread())
            {
                oldClan.InitMembers();
                newClan.InitMembers();
                Server.GetRegisteredObject<Hero>(oldLeaderId).Clan = oldClan;
                Server.GetRegisteredObject<Hero>(newLeaderId).Clan = newClan;
                member.Clan = oldClan;
                settlement.Town = town;
                settlement.SetSettlementComponent(town);
                town.OwnerClan = captorClan;
            }
            TakePrisonerAction.Apply(settlement.Party, member);

            var data = Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData;
            data.PlayerClanBloodFeuds[oldClanId] = new() { [captorClanId] = new BloodFeudData { HasBloodFeud = true } };
            data.PlayerClanBloodFeuds[newClanId] = new() { [captorClanId] = new BloodFeudData { HasBloodFeud = true } };
            long executionDateTicks = CampaignTime.DaysFromNow(3).NumTicks;
            data.PlayerClanPendingSettlementExecutions[oldClanId] = new() { [memberId] = new PendingSettlementExecution(captorClanId, executionDateTicks) };

            member.Clan = newClan;

            Assert.Empty(data.PlayerClanPendingSettlementExecutions[oldClanId]);
            var moved = data.PlayerClanPendingSettlementExecutions[newClanId][memberId];
            Assert.Equal((captorClanId, executionDateTicks), (moved.CapturerClanId, moved.ExecutionDateTicks));
        });
    }
}
