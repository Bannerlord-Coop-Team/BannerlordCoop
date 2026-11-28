using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using GameInterface.Configuration;
using GameInterface.CoopSessionData;
using GameInterface.Services.BloodFeuds;
using GameInterface.Services.BloodFeuds.Data;
using GameInterface.Services.BloodFeuds.Interfaces;
using GameInterface.Services.BloodFeuds.Messages;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.Players;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.BloodFeuds;

public class ExecutionCampaignBehaviorInterfaceTests : MapEventTestBase
{
    public ExecutionCampaignBehaviorInterfaceTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void PlayerExecutingAnotherPlayerClansMember_StartsTheFeudOnBothClans()
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
            }

            var executionInterface = Server.Resolve<IExecutionCampaignBehaviorInterface>();
            var feuds = Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData.PlayerClanBloodFeuds;

            executionInterface.OnHeroKilled(victim, playerA, KillCharacterAction.KillCharacterActionDetail.Executed);

            var executingSide = feuds[clanAId][clanBId];
            var executedSide = feuds[clanBId][clanAId];
            Assert.Equal((true, 0, 1), (executingSide.HasBloodFeud, executingSide.ExecutionsDoneCount, executingSide.ExecutionsReceivedCount));
            Assert.Equal((true, 1, 0), (executedSide.HasBloodFeud, executedSide.ExecutionsDoneCount, executedSide.ExecutionsReceivedCount));
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
            // The server keeps feuds per player clan, so only clients set vanilla's flag
            Assert.False(Server.GetRegisteredObject<Clan>(feudClanId).HasBloodFeudWithPlayer);
        });

        var client = Clients.First();
        client.PumpGameThread();
        client.Call(() => Assert.True(client.GetRegisteredObject<Clan>(feudClanId).HasBloodFeudWithPlayer));
    }

    [Fact]
    public void JoiningClient_AppliesItsClansFeudsFromTheTransferredSession()
    {
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var otherClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var client = Clients.First();
        var feuds = new Dictionary<string, Dictionary<string, BloodFeudData>>
        {
            [playerClanId] = new() { [feudClanId] = new BloodFeudData { HasBloodFeud = true, ExecutionsDoneCount = 1, ExecutionsReceivedCount = 2 } },
        };

        client.Call(() =>
        {
            var hero = client.GetRegisteredObject<Hero>(heroId);
            var playerClan = client.GetRegisteredObject<Clan>(playerClanId);
            using (new AllowedThread())
            {
                playerClan.InitMembers();
                hero.Clan = playerClan;
            }

            var messageBroker = client.Resolve<IMessageBroker>();
            messageBroker.Publish(this, new InitializeClientBloodFeudData(new BloodFeudPlayerData(feuds, null, null)));
            messageBroker.Publish(this, new PlayerHeroChanged(null, hero));
        }, new[]
        {
            AccessTools.Method(typeof(InteractionsInitializationHandler), "Handle", new[] { typeof(MessagePayload<PlayerHeroChanged>) }),
        });
        client.PumpGameThread();

        client.Call(() =>
        {
            var feudClan = client.GetRegisteredObject<Clan>(feudClanId);
            Assert.Equal((true, 1, 2), (feudClan.HasBloodFeudWithPlayer, feudClan.BloodFeudExecutionsDoneCount, feudClan.BloodFeudExecutionsReceivedCount));
            Assert.False(client.GetRegisteredObject<Clan>(otherClanId).HasBloodFeudWithPlayer);
        });
    }

    [Fact]
    public void LeaderPayingRansom_EndsTheFeudForTheServersBloodMoney()
    {
        var (playerId, _) = CreatePlayerHeroParty("Payer");
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();
        var client = Clients.First();
        var feud = new BloodFeudData { HasBloodFeud = true, ExecutionsDoneCount = 1, ExecutionsReceivedCount = 2 };

        Server.Call(() =>
        {
            var player = Server.GetRegisteredObject<Hero>(playerId);
            var playerClan = Server.GetRegisteredObject<Clan>(playerClanId);
            var feudClan = Server.GetRegisteredObject<Clan>(feudClanId);
            var feudLeader = Server.GetRegisteredObject<Hero>(feudLeaderId);

            using (new AllowedThread())
            {
                playerClan.InitMembers();
                feudClan.InitMembers();
                player.Clan = playerClan;
                playerClan.SetLeader(player);
                feudLeader.Clan = feudClan;
                feudClan.SetLeader(feudLeader);
                player.Gold = 100000;
            }

            Server.Resolve<IPlayerManager>().SetPeer("Payer", client.NetPeer);
            Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData.PlayerClanBloodFeuds[playerClanId] = new() { [feudClanId] = feud };
        });
        Server.NetworkSentMessages.Clear();

        client.Call(() => client.Resolve<INetwork>().SendAll(new NetworkSettleBloodFeudByRansomPayment(feudClanId)));
        Server.PumpGameThread();

        Server.Call(() => Assert.Equal(100000 - 55000, Server.GetRegisteredObject<Hero>(playerId).Gold));
        Assert.Equal((false, 0, 0), (feud.HasBloodFeud, feud.ExecutionsDoneCount, feud.ExecutionsReceivedCount));
        var notice = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkNotifyBloodFeudStateChanged>());
        Assert.Equal((ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail.SettledByRansomPayment, false), (notice.Detail, notice.SettledByAgreement));
    }

    [Fact]
    public void RelationRisingToTheEndLevel_EndsThePlayerClansFeud()
    {
        var (playerId, _) = CreatePlayerHeroParty("Forgiver");
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var player = Server.GetRegisteredObject<Hero>(playerId);
            var playerClan = Server.GetRegisteredObject<Clan>(playerClanId);
            var feudClan = Server.GetRegisteredObject<Clan>(feudClanId);
            var feudLeader = Server.GetRegisteredObject<Hero>(feudLeaderId);

            using (new AllowedThread())
            {
                playerClan.InitMembers();
                feudClan.InitMembers();
                player.Clan = playerClan;
                playerClan.SetLeader(player);
                feudLeader.Clan = feudClan;
                feudClan.SetLeader(feudLeader);
            }

            Server.Resolve<IPlayerManager>().SetPeer("Forgiver", Clients.First().NetPeer);
            var feuds = Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData.PlayerClanBloodFeuds;
            feuds[playerClanId] = new() { [feudClanId] = new BloodFeudData { HasBloodFeud = true } };
            Server.NetworkSentMessages.Clear();

            CharacterRelationManager.SetHeroRelation(player, feudLeader, ExecutionCampaignBehavior.BloodFeudEndRelationLevel);
            // The E2E campaign has no ExecutionCampaignBehavior, so its OnHeroRelationChanged listener is called here
            Server.Resolve<IExecutionCampaignBehaviorInterface>().OnHeroRelationChanged(player, feudLeader);

            Assert.False(feuds[playerClanId][feudClanId].HasBloodFeud);
            var notice = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkNotifyBloodFeudStateChanged>());
            Assert.Equal(ChangeBloodFeudStateAction.ChangeBloodFeudActionDetail.SettledByRelationIncrease, notice.Detail);
        });
    }

    [Fact]
    public void ClanMemberInAFeudedClansPrison_GetsAPendingExecution_ThatEndsWhenTheTownChangesHands()
    {
        var (leaderId, _) = CreatePlayerHeroParty("ClanLeader");
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var memberId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var previousOptions = ModConfigProvider.ModOptions;
            try
            {
                ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { EnableHeroExecutions = true, EnablePlayerClanMemberExecutions = true });

                var playerClan = Server.GetRegisteredObject<Clan>(playerClanId);
                var member = Server.GetRegisteredObject<Hero>(memberId);
                var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
                var town = GameObjectCreator.CreateInitializedObject<Town>();
                var captorClan = GameObjectCreator.CreateInitializedObject<Clan>();
                var newOwnerClan = GameObjectCreator.CreateInitializedObject<Clan>();
                Assert.True(Server.ObjectManager.TryGetId(captorClan, out var captorClanId));

                using (new AllowedThread())
                {
                    playerClan.InitMembers();
                    Server.GetRegisteredObject<Hero>(leaderId).Clan = playerClan;
                    member.Clan = playerClan;
                    settlement.Town = town;
                    settlement.SetSettlementComponent(town);
                    town.OwnerClan = captorClan;
                }
                TakePrisonerAction.Apply(settlement.Party, member);

                Server.Resolve<IPlayerManager>().SetPeer("ClanLeader", Clients.First().NetPeer);
                var data = Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData;
                data.PlayerClanBloodFeuds[playerClanId] = new() { [captorClanId] = new BloodFeudData { HasBloodFeud = true } };
                var executionInterface = Server.Resolve<IExecutionCampaignBehaviorInterface>();
                Server.NetworkSentMessages.Clear();

                executionInterface.UpdateAllPlayerClanMemberExecutionStates();

                var pending = data.PlayerClanPendingSettlementExecutions[playerClanId][memberId];
                long executionDateTicks = CampaignTime.DaysFromNow(ExecutionCampaignBehavior.DaysTillBloodFeudExecutionInSettlement).NumTicks;
                Assert.Equal((captorClanId, executionDateTicks), (pending.CapturerClanId, pending.ExecutionDateTicks));
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkNotifyBloodFeudSettlementExecutionStarted>());
                bool canBeReleased = true;
                executionInterface.CanHeroBeReleased(member, ref canBeReleased);
                Assert.False(canBeReleased);

                using (new AllowedThread())
                {
                    town.OwnerClan = newOwnerClan;
                }
                executionInterface.UpdateAllPlayerClanMemberExecutionStates();

                Assert.Empty(data.PlayerClanPendingSettlementExecutions[playerClanId]);
                Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkNotifyBloodFeudSettlementExecutionCancelled>());
                canBeReleased = true;
                executionInterface.CanHeroBeReleased(member, ref canBeReleased);
                Assert.True(canBeReleased);
            }
            finally
            {
                ModConfigProvider.ModOptions = previousOptions;
            }
        });
    }

    [Fact]
    public void PendingSettlementExecutionPastItsDate_ExecutesTheClanMemberOnTheDailyTick()
    {
        var (leaderId, _) = CreatePlayerHeroParty("ClanLeader");
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var memberId = TestEnvironment.CreateRegisteredObject<Hero>();
        var captorClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var captorLeaderId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var previousOptions = ModConfigProvider.ModOptions;
            try
            {
                ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { EnableHeroExecutions = true, EnablePlayerClanMemberExecutions = true });

                var playerClan = Server.GetRegisteredObject<Clan>(playerClanId);
                var member = Server.GetRegisteredObject<Hero>(memberId);
                var captorClan = Server.GetRegisteredObject<Clan>(captorClanId);
                var captorLeader = Server.GetRegisteredObject<Hero>(captorLeaderId);
                var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
                var town = GameObjectCreator.CreateInitializedObject<Town>();

                using (new AllowedThread())
                {
                    playerClan.InitMembers();
                    captorClan.InitMembers();
                    Server.GetRegisteredObject<Hero>(leaderId).Clan = playerClan;
                    member.Clan = playerClan;
                    captorLeader.Clan = captorClan;
                    captorClan.SetLeader(captorLeader);
                    settlement.Town = town;
                    settlement.SetSettlementComponent(town);
                    town.OwnerClan = captorClan;
                }
                TakePrisonerAction.Apply(settlement.Party, member);

                var data = Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData;
                data.PlayerClanBloodFeuds[playerClanId] = new() { [captorClanId] = new BloodFeudData { HasBloodFeud = true } };
                data.PlayerClanPendingSettlementExecutions[playerClanId] = new()
                {
                    [memberId] = new PendingSettlementExecution(captorClanId, CampaignTime.DaysFromNow(-1).NumTicks),
                };

                Server.Resolve<IExecutionCampaignBehaviorInterface>().DailyTickHero(member);

                Assert.False(member.IsAlive);
            }
            finally
            {
                ModConfigProvider.ModOptions = previousOptions;
            }
        }, new[] { AccessTools.Method(typeof(KillCharacterAction), nameof(KillCharacterAction.CreateObituary)) });
        Server.PumpGameThread();
    }

    [Fact]
    public void ClanPartyCapturingAFeudedClansLord_HoldsThemForExecutionAfterTheBattle()
    {
        var (leaderId, _) = CreatePlayerHeroParty("ClanLeader");
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var lordId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var previousOptions = ModConfigProvider.ModOptions;
            try
            {
                ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { EnableHeroExecutions = true });

                var playerClan = Server.GetRegisteredObject<Clan>(playerClanId);
                var feudClan = Server.GetRegisteredObject<Clan>(feudClanId);
                var lord = Server.GetRegisteredObject<Hero>(lordId);
                var clanParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

                using (new AllowedThread())
                {
                    playerClan.InitMembers();
                    feudClan.InitMembers();
                    Server.GetRegisteredObject<Hero>(leaderId).Clan = playerClan;
                    clanParty.ActualClan = playerClan;
                    clanParty.LeaderHero.Clan = playerClan;
                    lord.Clan = feudClan;
                }
                TakePrisonerAction.Apply(clanParty.Party, lord);

                var data = Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData;
                data.PlayerClanBloodFeuds[playerClanId] = new() { [feudClanId] = new BloodFeudData { HasBloodFeud = true } };
                var executionInterface = Server.Resolve<IExecutionCampaignBehaviorInterface>();

                // The E2E campaign has no ExecutionCampaignBehavior, so its OnHeroTakenPrisoner listener is called here
                executionInterface.OnHeroTakenPrisoner(clanParty.Party, lord);

                Assert.Equal(new[] { lordId }, data.PlayerClanHeroesPendingMapEventEndExecution[playerClanId]);
                bool canBeReleased = true;
                executionInterface.CanHeroBeReleased(lord, ref canBeReleased);
                Assert.False(canBeReleased);
            }
            finally
            {
                ModConfigProvider.ModOptions = previousOptions;
            }
        });
    }

    [Fact]
    public void ClanPartyLeaderMovingToAnotherFeudingPlayerClan_TakesTheirPrisonersPendingExecutionsAlong()
    {
        var (oldLeaderId, _) = CreatePlayerHeroParty("OldClanLeader");
        var (newLeaderId, _) = CreatePlayerHeroParty("NewClanLeader");
        var oldClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var newClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var lordId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var oldClan = Server.GetRegisteredObject<Clan>(oldClanId);
            var newClan = Server.GetRegisteredObject<Clan>(newClanId);
            var feudClan = Server.GetRegisteredObject<Clan>(feudClanId);
            var lord = Server.GetRegisteredObject<Hero>(lordId);
            var clanParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

            using (new AllowedThread())
            {
                oldClan.InitMembers();
                newClan.InitMembers();
                feudClan.InitMembers();
                Server.GetRegisteredObject<Hero>(oldLeaderId).Clan = oldClan;
                Server.GetRegisteredObject<Hero>(newLeaderId).Clan = newClan;
                clanParty.ActualClan = oldClan;
                clanParty.LeaderHero.Clan = oldClan;
                lord.Clan = feudClan;
            }
            TakePrisonerAction.Apply(clanParty.Party, lord);

            var data = Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData;
            data.PlayerClanBloodFeuds[oldClanId] = new() { [feudClanId] = new BloodFeudData { HasBloodFeud = true } };
            data.PlayerClanBloodFeuds[newClanId] = new() { [feudClanId] = new BloodFeudData { HasBloodFeud = true } };
            data.PlayerClanHeroesPendingMapEventEndExecution[oldClanId] = new() { lordId };

            clanParty.LeaderHero.Clan = newClan;

            Assert.Empty(data.PlayerClanHeroesPendingMapEventEndExecution[oldClanId]);
            Assert.Equal(new[] { lordId }, data.PlayerClanHeroesPendingMapEventEndExecution[newClanId]);
        });
    }

    [Fact]
    public void ClanMemberHeldByAFeudedClansParty_CantBeReleasedUntilTheFeudEnds()
    {
        var (leaderId, _) = CreatePlayerHeroParty("ClanLeader");
        var playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var feudClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var memberId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            var previousOptions = ModConfigProvider.ModOptions;
            try
            {
                ModConfigProvider.ModOptions = new ModOptions(new ModOptionsData { EnableHeroExecutions = true, EnablePlayerClanMemberExecutions = true });

                var playerClan = Server.GetRegisteredObject<Clan>(playerClanId);
                var feudClan = Server.GetRegisteredObject<Clan>(feudClanId);
                var member = Server.GetRegisteredObject<Hero>(memberId);
                var feudParty = GameObjectCreator.CreateInitializedObject<MobileParty>();

                using (new AllowedThread())
                {
                    playerClan.InitMembers();
                    feudClan.InitMembers();
                    Server.GetRegisteredObject<Hero>(leaderId).Clan = playerClan;
                    member.Clan = playerClan;
                    feudParty.ActualClan = feudClan;
                    feudParty.LeaderHero.Clan = feudClan;
                }
                TakePrisonerAction.Apply(feudParty.Party, member);

                var feud = new BloodFeudData { HasBloodFeud = true };
                Server.Resolve<ICoopSessionProvider>().CoopSession.BloodFeudPlayerData.PlayerClanBloodFeuds[playerClanId] = new() { [feudClanId] = feud };
                var executionInterface = Server.Resolve<IExecutionCampaignBehaviorInterface>();

                bool canBeReleased = true;
                executionInterface.CanHeroBeReleased(member, ref canBeReleased);
                Assert.False(canBeReleased);

                feud.HasBloodFeud = false;
                canBeReleased = true;
                executionInterface.CanHeroBeReleased(member, ref canBeReleased);
                Assert.True(canBeReleased);
            }
            finally
            {
                ModConfigProvider.ModOptions = previousOptions;
            }
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
