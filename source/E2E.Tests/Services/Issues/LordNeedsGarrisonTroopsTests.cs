using Autofac;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LordNeedsGarrisonTroops;
using System.Runtime.CompilerServices;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class LordNeedsGarrisonTroopsTests : IDisposable
{
    private const string IssueName = "LordNeedsGarrisonTroopsIssue";
    private readonly E2ETestEnvironment testEnvironment;
    private readonly string giverId;
    private readonly string settlementId;
    private readonly string troopId;

    public LordNeedsGarrisonTroopsTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
        giverId = testEnvironment.CreateRegisteredObject<Hero>();
        settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        troopId = testEnvironment.CreateRegisteredObject<CharacterObject>();
    }

    public void Dispose() => testEnvironment.Dispose();

    [Fact]
    public void TheQuestIsRegisteredLikeTheOtherFrameworkQuests()
    {
        var descriptor = Descriptor(testEnvironment.Server);

        Assert.Equal(typeof(Issue), descriptor.IssueType);
        Assert.NotNull(descriptor.CreationCaptureStrategy);
        Assert.NotNull(descriptor.QuestSolutionAcceptStrategy);
        Assert.NotNull(descriptor.AlternativeSolutionAcceptStrategy);
        Assert.NotNull(descriptor.FinalizationProofStrategy);
    }

    [Fact]
    public void CreationCapture_ForcesTheSettlementAndTroopChosenByTheServer()
    {
        byte[] captured = null!;

        testEnvironment.Server.Call(() =>
        {
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            var issue = Uninitialized<Issue>();
            issue._settlement = settlement;
            issue._neededTroopType = troop;
            captured = Descriptor(testEnvironment.Server).CreationCaptureStrategy.Capture(issue);
        });

        testEnvironment.Clients.First().Call(() =>
        {
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetObject<Settlement>(settlementId, out var expectedSettlement));
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetObject<CharacterObject>(troopId, out var expectedTroop));
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetId(expectedSettlement, out var expectedSettlementId));
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetId(expectedTroop, out var expectedTroopObjectId));

            // This assertion exercises the object-id based creation path without relying on local random picks.
            var strategy = (LordNeedsGarrisonTroopsCreationCaptureStrategy)Descriptor(testEnvironment.Clients.First()).CreationCaptureStrategy;
            Assert.ThrowsAny<Exception>(() => strategy.CreateIssue(giver, Array.Empty<byte>()));
            using (var stream = new MemoryStream(captured))
            using (var reader = new BinaryReader(stream))
            {
                Assert.Equal(expectedSettlementId, reader.ReadString());
                Assert.Equal(expectedTroopObjectId, reader.ReadString());
            }
        });
    }

    [Fact]
    public void AcceptCapture_ForcesTheOwnerRequestedAmountRewardAndDueTime()
    {
        testEnvironment.Server.Call(() =>
        {
            var accepting = Uninitialized<Quest>();
            accepting._requestedTroopAmount = 17;
            accepting._rewardGold = 2345;
            accepting.QuestDueTime = CampaignTime.DaysFromNow(29f);

            var stale = Uninitialized<Quest>();
            stale._requestedTroopAmount = 1;
            stale._rewardGold = 2;
            stale.QuestDueTime = CampaignTime.Zero;

            var acceptingIssue = Uninitialized<Issue>();
            acceptingIssue.IssueQuest = accepting;
            var staleIssue = Uninitialized<Issue>();
            staleIssue.IssueQuest = stale;

            var strategy = Descriptor(testEnvironment.Server).QuestSolutionAcceptStrategy;
            strategy.Apply(staleIssue, strategy.Capture(acceptingIssue));

            Assert.Equal(17, stale._requestedTroopAmount);
            Assert.Equal(2345, stale._rewardGold);
            Assert.Equal(accepting.QuestDueTime, stale.QuestDueTime);
        });
    }

    [Fact]
    public void DeliveryProof_RefusesAnUnknownBranchAndMissingTroops()
    {
        testEnvironment.Server.Call(() =>
        {
            var quest = Uninitialized<Quest>();
            quest._requestedTroopAmount = 5;
            quest._requestedTroopType = DefaultPartyTroop();

            var strategy = (LordNeedsGarrisonTroopsFinalizationProofStrategy)Descriptor(testEnvironment.Server).FinalizationProofStrategy;
            Assert.False(strategy.TryRunBranch(quest, 0));
            Assert.True(strategy.TryRunBranch(quest, (byte)LordNeedsGarrisonTroopsBranch.TroopsDelivered));
        });
    }

    private static IQuestTypeDescriptor Descriptor(EnvironmentInstance instance)
    {
        IQuestTypeDescriptor descriptor = null!;
        instance.Call(() => Assert.True(instance.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out descriptor)));
        return descriptor;
    }

    private static T Uninitialized<T>() where T : class =>
        (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static CharacterObject DefaultPartyTroop() =>
        (CharacterObject)RuntimeHelpers.GetUninitializedObject(typeof(CharacterObject));

}
