using Autofac;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.LordNeedsGarrisonTroops;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Issues.Framework.Finalization;
using HarmonyLib;
using System.Runtime.CompilerServices;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class LordNeedsGarrisonTroopsTests : IDisposable
{
    private const string IssueName = "LordNeedsGarrisonTroopsIssue";
    private readonly E2ETestEnvironment testEnvironment;
    private readonly string settlementId;
    private readonly string troopId;
    private readonly string townId;
    private readonly string giverClanId;
    private readonly QuestScenario scenario;

    public LordNeedsGarrisonTroopsTests(ITestOutputHelper output)
    {
        testEnvironment = new E2ETestEnvironment(output);
        settlementId = testEnvironment.CreateRegisteredObject<Settlement>();
        troopId = testEnvironment.CreateRegisteredObject<CharacterObject>();
        townId = testEnvironment.CreateRegisteredObject<Town>();
        giverClanId = testEnvironment.CreateRegisteredObject<Clan>();
        scenario = new QuestScenario(testEnvironment, (instance, giver) =>
        {
            var troop = Get<CharacterObject>(instance, troopId);
            troop.UpgradeTargets = Array.Empty<CharacterObject>();
            troop._basicName = new TextObject("Test troop");
            troop.Level = 20;
            var culture = GameObjectCreator.CreateInitializedObject<CultureObject>();
            culture.BasicTroop = troop;
            giver.CharacterObject.Culture = culture;
            var giverClan = Get<Clan>(instance, giverClanId);
            giverClan.Culture = culture;
            giver.Clan = giverClan;
            giver.CharacterObject._basicName = new TextObject("Garrison giver");
            giver.Culture.BasicTroop = troop;
            Assert.NotNull(troop.UpgradeTargets);
            return new Issue(giver, Get<Settlement>(instance, settlementId));
        });

        foreach (var instance in testEnvironment.Clients.Prepend(testEnvironment.Server))
        {
            instance.Call(() =>
            {
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    var settlement = Get<Settlement>(instance, settlementId);
                    var town = Get<Town>(instance, townId);
                    var giver = Get<Hero>(instance, scenario.NotableId);
                    var giverClan = Get<Clan>(instance, giverClanId);
                    var owner = Get<Hero>(instance, scenario.OwnerHeroId);
                    var ownerParty = Get<MobileParty>(instance, scenario.OwnerPartyId);

                    settlement.SetSettlementComponent(town);
                    town._ownerClan = giverClan;
                    giver.Clan = giverClan;
                    giverClan._leader = giver;
                    GarrisonPartyComponent.CreateGarrisonParty("garrison-test", settlement);
                    owner._partyBelongedTo = ownerParty;
                    ownerParty.CurrentSettlement = settlement;
                    owner.Gold = 10000;

                    if (instance == testEnvironment.Clients.First())
                    {
                        Game.Current.PlayerTroop = owner.CharacterObject;
                        Campaign.Current.MainParty = ownerParty;
                        Campaign.Current.PlayerDefaultFaction = owner.Clan;
                    }
                }
            });
        }
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
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetObject<Settlement>(settlementId, out var expectedSettlement));
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetObject<CharacterObject>(troopId, out var expectedTroop));
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetId(expectedSettlement, out var expectedSettlementId));
            Assert.True(testEnvironment.Clients.First().ObjectManager.TryGetId(expectedTroop, out var expectedTroopObjectId));

            var strategy = (LordNeedsGarrisonTroopsCreationCaptureStrategy)Descriptor(testEnvironment.Clients.First()).CreationCaptureStrategy;
            Assert.True(strategy.TryRead(captured, out var decodedSettlement, out var decodedTroop));
            Assert.Same(expectedSettlement, decodedSettlement);
            Assert.Same(expectedTroop, decodedTroop);
            // Invalid creation data is rejected instead of producing a partially populated issue.
            Assert.ThrowsAny<Exception>(() => strategy.CreateIssue(Hero.MainHero, Array.Empty<byte>()));
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
            AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold)).SetValue(accepting, 3456);
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
            Assert.Equal(3456, stale.RewardGold);
            Assert.Equal(accepting.QuestDueTime, stale.QuestDueTime);
        });
    }

    [Fact]
    public void DeliveryProofRefusesAnUnknownBranch()
    {
        testEnvironment.Server.Call(() =>
        {
            var quest = Uninitialized<Quest>();
            var strategy = (LordNeedsGarrisonTroopsFinalizationProofStrategy)Descriptor(testEnvironment.Server).FinalizationProofStrategy;
            Assert.False(strategy.TryRunBranch(quest, 0));
        });
    }

    [Fact]
    public void DirectHandInSuccessMirrorsOneVanillaSuccessEntryToOwnerAndArchive()
    {
        foreach (var instance in testEnvironment.Clients.Prepend(testEnvironment.Server))
            instance.Call(() => new JournalLogsCampaignBehavior().RegisterEvents());

        var rewardPatch = new Harmony($"garrison-direct-success.{Guid.NewGuid():N}");
        rewardPatch.Patch(
            AccessTools.PropertyGetter(typeof(Issue), "RewardGold"),
            prefix: new HarmonyMethod(typeof(LordNeedsGarrisonTroopsTests), nameof(ReturnTestReward)));
        try
        {
            scenario.Accept(scenario.ClientA);
            var serverQuest = (Quest)scenario.QuestOf(scenario.Server);
            var ownerQuest = (Quest)scenario.QuestOf(scenario.ClientA);

            scenario.ClientA.Call(() =>
            {
                using var allowed = new AllowedThread();
                var accepted = AccessTools.Method(typeof(Quest), "QuestAcceptedConsequences");
                Assert.NotNull(accepted);
                accepted!.Invoke(ownerQuest, null);
            });

            string expected = null!;
            scenario.Server.Call(() =>
            {
                expected = Assert.IsType<TextObject>(AccessTools.PropertyGetter(typeof(Quest), "SuccessQuestLogText")!.Invoke(serverQuest, null)).ToString();
                serverQuest._questState = QuestBase.QuestStates.Ongoing;
                var requestedTroopAmount = serverQuest._requestedTroopAmount;
                serverQuest._settlement.Town!.GarrisonParty!.MemberRoster.AddToCounts(
                    Get<CharacterObject>(scenario.Server, troopId), requestedTroopAmount);
                var party = Get<MobileParty>(scenario.Server, scenario.OwnerPartyId);
                party.MemberRoster.AddToCounts(Get<CharacterObject>(scenario.Server, troopId), requestedTroopAmount);
                Campaign.Current.MainParty = party;

                var owner = Get<Hero>(scenario.Server, scenario.OwnerHeroId);
                using (new MainHeroSubstitutionScope(owner, party))
                {
                    Assert.True(Descriptor(scenario.Server).FinalizationProofStrategy!.TryRunBranch(
                        serverQuest, (byte)LordNeedsGarrisonTroopsBranch.TroopsDelivered));
                }
            });
            testEnvironment.FlushCoalescer();

            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
            Assert.Single(scenario.Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
            scenario.Server.Call(() =>
            {
                Assert.Single(serverQuest.JournalEntries, log => log.LogText.ToString() == expected);
                var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                    entry => entry.IsRelatedTo(serverQuest)));
                Assert.True(archive.IsEnded());
                Assert.Single(archive.GetEntries(), log => log.LogText.ToString() == expected);
            });
            scenario.ClientA.Call(() =>
            {
                var ownerEntries = ownerQuest.JournalEntries.ToArray();
                Assert.Equal(2, ownerEntries.Length);
                Assert.Contains(expected, ownerEntries.Select(log => log.LogText.ToString()));
                var archive = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                    entry => entry.IsRelatedTo(ownerQuest)));
                Assert.True(archive.IsEnded());
                var archivedEntries = archive.GetEntries().ToArray();
                Assert.Equal(2, archivedEntries.Length);
                Assert.Contains(expected, archivedEntries.Select(log => log.LogText.ToString()));
            });
            scenario.ClientB.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(
                entry => entry._relatedObjectIds.Contains(QuestScenario.IssueId))));
        }
        finally
        {
            rewardPatch.UnpatchAll(rewardPatch.Id);
        }
    }

    private static bool ReturnTestReward(ref int __result)
    {
        __result = 100;
        return false;
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    public void DeliveryProofRunsVanillaHandInOnlyWhenTheOwnerCarriesEnoughTroops(int troopCount, int expectedHandIns)
    {
        using var tracking = new MethodCallRecorder(AccessTools.Method(typeof(Quest), nameof(Quest.PlayerTransferredTroopsToGarrisonCommander)));
        var partyId = testEnvironment.CreateRegisteredObject<MobileParty>();
        var townId = testEnvironment.CreateRegisteredObject<Town>();

        testEnvironment.Server.Call(() =>
        {
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(testEnvironment.Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            settlement.Town = town;
            settlement.SetSettlementComponent(town);
            GarrisonPartyComponent.CreateGarrisonParty("test-garrison", settlement);
            party.CurrentSettlement = settlement;
            party.MemberRoster.AddToCounts(troop, troopCount);

            var quest = Uninitialized<Quest>();
            quest._questState = QuestBase.QuestStates.Ongoing;
            quest._settlement = settlement;
            quest._requestedTroopType = troop;
            quest._requestedTroopAmount = 5;
            quest.QuestGiver = Hero.MainHero;
            quest.QuestDueTime = CampaignTime.DaysFromNow(1f);

            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = party;
            try
            {
                Assert.True(Descriptor(testEnvironment.Server).FinalizationProofStrategy.TryRunBranch(
                    quest, (byte)LordNeedsGarrisonTroopsBranch.TroopsDelivered));
                Assert.Equal(expectedHandIns, tracking.CountFor(testEnvironment.Server));
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }
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

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class =>
        instance.GetRegisteredObject<T>(id);
}
