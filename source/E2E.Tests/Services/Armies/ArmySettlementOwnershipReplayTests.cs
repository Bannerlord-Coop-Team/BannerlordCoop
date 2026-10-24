using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Util;
using GameInterface.Services.Settlements.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

/// <summary>Ownership replay must notify clients without rerunning authoritative army AI.</summary>
public sealed class ArmySettlementOwnershipReplayTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly ITestOutputHelper output;

    public ArmySettlementOwnershipReplayTests(ITestOutputHelper output)
    {
        this.output = output;
        environment = new E2ETestEnvironment(output, numClients: 1);
    }

    public void Dispose() => environment.Dispose();

    [Theory]
    [InlineData(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByGift)]
    [InlineData(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege)]
    public void ServerOwnershipEventStillSelectsTheArmyTarget(
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        var armyId = environment.CreateRegisteredObject<Army>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var server = environment.Server;
        server.Call(() =>
        {
            Assert.Empty(Campaign.Current.QuestManager.Quests);
            Assert.Empty(Campaign.Current.IssueManager.Issues);
            var army = server.GetRegisteredObject<Army>(armyId);
            var settlement = server.GetRegisteredObject<Settlement>(settlementId);
            var town = server.GetRegisteredObject<Town>(townId);
            var owner = server.GetRegisteredObject<Hero>(ownerId);
            owner.Clan.InitMembers();
            owner.Clan.SetLeader(owner);
            settlement.SetSettlementComponent(town);
            town.OwnerClan = owner.Clan;
            army._armyGatheringStartTime = 0f;
            army._aiBehaviorObject = settlement;
            Assert.True(army.IsWaitingForArmyMembers());
            Assert.NotSame(settlement.MapFaction, army.LeaderParty.MapFaction);
            Assert.Null(army.LeaderParty.SiegeEvent);
            Assert.Null(army.LeaderParty.MapEvent);
        });

        // Record entry into AI selection without requiring a native navigation mesh in this control.
        using var nativeAi = new MethodCallRecorder(
            AccessTools.Method(typeof(Army), nameof(Army.FindBestGatheringSettlementAndMoveTheLeader)));
        server.Call(() =>
        {
            var army = server.GetRegisteredObject<Army>(armyId);
            var settlement = server.GetRegisteredObject<Settlement>(settlementId);
            var owner = server.GetRegisteredObject<Hero>(ownerId);
            CampaignEventDispatcher.Instance.OnSettlementOwnerChanged(
                settlement, detail == ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege,
                owner, army.LeaderParty.LeaderHero, owner, detail);
        });
        Assert.Equal(1, nativeAi.CountFor(server));
    }

    [Theory]
    [InlineData(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.ByGift)]
    [InlineData(ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege)]
    public void OwnershipReplayWithoutAnyQuestDoesNotRecalculateTheClientArmyTarget(
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var newOwnerId = environment.CreateRegisteredObject<Hero>();
        var previousOwnerId = environment.CreateRegisteredObject<Hero>();
        var newClanId = environment.CreateRegisteredObject<Clan>();
        var previousClanId = environment.CreateRegisteredObject<Clan>();
        var armyId = environment.CreateRegisteredObject<Army>();
        var client = environment.Clients.Single();
        var notifications = new List<Settlement>();

        client.Call(() =>
        {
            Assert.Empty(Campaign.Current.QuestManager.Quests);
            Assert.Empty(Campaign.Current.IssueManager.Issues);
            var settlement = client.GetRegisteredObject<Settlement>(settlementId);
            var town = client.GetRegisteredObject<Town>(townId);
            var owner = client.GetRegisteredObject<Hero>(newOwnerId);
            var previousOwner = client.GetRegisteredObject<Hero>(previousOwnerId);
            var clan = client.GetRegisteredObject<Clan>(newClanId);
            var previousClan = client.GetRegisteredObject<Clan>(previousClanId);
            var army = client.GetRegisteredObject<Army>(armyId);
            using (new AllowedThread())
            {
                clan.InitMembers();
                previousClan.InitMembers();
                owner.Clan = clan;
                clan.SetLeader(owner);
                previousOwner.Clan = previousClan;
                previousClan.SetLeader(previousOwner);
                settlement.SetSettlementComponent(town);
                town.OwnerClan = previousClan;
                army.LeaderParty.ActualClan = previousClan;
                army._armyGatheringStartTime = 0f;
                army._aiBehaviorObject = settlement;
            }
            Assert.True(army.IsWaitingForArmyMembers());
            Assert.Same(settlement, army.AiBehaviorObject);
            Assert.NotSame(clan.MapFaction, army.LeaderParty.MapFaction);
            Assert.Null(army.LeaderParty.SiegeEvent);
            Assert.Null(army.LeaderParty.MapEvent);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this,
                (changed, _, _, _, _, _) => notifications.Add(changed));
        });

        using var nativeAi = new MethodCallRecorder(Priority.Normal, true,
            AccessTools.Method(typeof(Army), nameof(Army.FindBestGatheringSettlementAndMoveTheLeader)));
        client.SimulateMessage(environment.Server, new NetworkChangeSettlementOwnership(
            settlementId, newOwnerId, previousOwnerId, newOwnerId, (int)detail));

        client.Call(() =>
        {
            var settlement = client.GetRegisteredObject<Settlement>(settlementId);
            var owner = client.GetRegisteredObject<Hero>(newOwnerId);
            Assert.Same(owner.Clan, settlement.OwnerClan);
            Assert.Empty(Campaign.Current.QuestManager.Quests);
            Assert.Empty(Campaign.Current.IssueManager.Issues);
            output.WriteLine($"quest-free native army recalculations={nativeAi.CountFor(client)}, completed listeners={notifications.Count}");
            Assert.Equal(0, nativeAi.CountFor(client));
            Assert.Same(settlement, Assert.Single(notifications));
            Assert.Same(settlement, client.GetRegisteredObject<Army>(armyId).AiBehaviorObject);
        });
    }
}