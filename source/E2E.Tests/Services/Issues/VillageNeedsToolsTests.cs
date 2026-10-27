using Autofac;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.VillageNeedsTools;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class VillageNeedsToolsTests : IDisposable
{
    private const string IssueName = "VillageNeedsToolsIssue";
    private const string IssueId = "issue_village_needs_tools";
    private const string ControllerA = "player-A";
    private const string ControllerB = "player-B";
    private const int ServerPayment = 900;

    private static readonly FieldInfo PaymentField = AccessTools.Field(typeof(Issue), nameof(Issue._payment));
    private static Hero ConversationHeroOverride;
    private static readonly List<Hero> WorldEventOwners = new();

    private static readonly MethodBase[] QuestLogTexts =
    {
        AccessTools.Method(typeof(Quest), nameof(Quest.SetDialogs)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestStartedLog)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestSuccessLog)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.WarDeclaredQuestCancelLog)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerDeclaredWarQuestLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestTimeOutFailLog)),
    };

    private static readonly MethodBase[] AlternativeSolutionLogTexts =
    {
        AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.AlternativeSolutionStartLog)),
        AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.IssueAlternativeSolutionSuccessLog)),
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
        // The patched alternative ending can inline SetCharacterProperties before this test disables it.
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
    };

    private readonly string notableId;
    private readonly string settlementId;
    private readonly string villageId;
    private readonly string boundSettlementId;
    private readonly string townId;
    private readonly string giverClanId;
    private readonly string kingdomId;
    private readonly string playerClanId;
    private readonly string ownerHeroId;
    private readonly string ownerPartyId;
    private readonly string otherHeroId;

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance ClientA => TestEnvironment.Clients.First();
    private EnvironmentInstance ClientB => TestEnvironment.Clients.Last();
    private EnvironmentInstance[] Instances => new[] { Server, ClientA, ClientB };

    public VillageNeedsToolsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        villageId = TestEnvironment.CreateRegisteredObject<Village>();
        boundSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        townId = TestEnvironment.CreateRegisteredObject<Town>();
        giverClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        (ownerHeroId, ownerPartyId) = RegisterPlayer(ClientA, ControllerA);
        (otherHeroId, _) = RegisterPlayer(ClientB, ControllerB);

        GiveTheNotableTheIssue(Server, ServerPayment);
        GiveTheNotableTheIssue(ClientA, ServerPayment);
        GiveTheNotableTheIssue(ClientB, ServerPayment);
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                instance.ObjectManager.AddExisting("tools", DefaultItems.Tools);
                instance.ObjectManager.AddExisting("grain", DefaultItems.Grain);
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
                using (new AllowedThread())
                {
                    owner._partyBelongedTo = party;
                    party.ItemRoster.Clear();
                    if (instance == ClientA) Campaign.Current.MainParty = party;
                }
            });
        }
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void DebugCapture_IsRefusedForAHeroOutsideAVillage()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));

            Assert.False(Descriptor(Server).CreationCaptureStrategy.TryBuildDebugCapture(hero, out _));
        });
    }

    [Fact]
    public void QuestSolutionOptionCondition_IsTrueAfterTheOfferQuoteForAnEligibleOwner()
    {
        var conversationHeroGetter = AccessTools.PropertyGetter(typeof(Hero), nameof(Hero.OneToOneConversationHero));
        var questOptionCondition = AccessTools.Method(typeof(IssuesCampaignBehavior), "issue_offer_player_accept_quest_condition");
        Assert.NotNull(conversationHeroGetter);
        Assert.NotNull(questOptionCondition);

        ConversationHeroOverride = null;
        var harmony = new Harmony("bannerlordcoop.3766.quest-option-condition-test");
        harmony.Patch(conversationHeroGetter,
            prefix: new HarmonyMethod(typeof(VillageNeedsToolsTests), nameof(GetConversationHeroPrefix)));

        try
        {
            ClientA.Call(() =>
            {
                Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var giver));
                Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
                ConversationHeroOverride = giver;

                using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
                {
                    Assert.True(giver.Issue.CheckPreconditions(giver, out _));
                    Assert.True(giver.Issue.IsThereAlternativeSolution);
                    _ = giver.Issue.IssueQuestSolutionExplanationByIssueGiver;
                    Assert.True((bool)questOptionCondition.Invoke(null, null)!);
                }
            });
        }
        finally
        {
            ConversationHeroOverride = null;
            harmony.Unpatch(conversationHeroGetter, HarmonyPatchType.Prefix, harmony.Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreationCapture_PreservesTheOfferWhenTheClientsVillageEconomyDiffers(bool exchange)
    {
        if (exchange) SetExchangeOffer();
        byte[] captured = null!;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            captured = Descriptor(Server).CreationCaptureStrategy.Capture(notable.Issue);
        });

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            using (new AllowedThread())
            {
                notable.CurrentSettlement.Village.Hearth = exchange ? 650f : 250f;
                var type = new VillageType("tools_test");
                type._productions = new MBList<(ItemObject, float)> { (DefaultItems.Grain, 1f) };
                notable.CurrentSettlement.Village.VillageType = type;
                var copy = (Issue)Descriptor(ClientA).CreationCaptureStrategy.CreateIssue(notable, captured);
                var original = (Issue)notable.Issue;
                Assert.Same(DefaultItems.Tools, copy._requestedItem);
                Assert.Equal(original._numberOfRequestedItem, copy._numberOfRequestedItem);
                Assert.Equal(original._payment, copy._payment);
                Assert.Equal(original._exchangeItem, copy._exchangeItem);
                Assert.Equal(original._numberOfExchangeItem, copy._numberOfExchangeItem);
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TwoPlayersCanHoldSeparateToolsQuestsButOnePlayerCannotHoldTwo(bool anotherPlayer)
    {
        Accept();
        var secondGiverId = TestEnvironment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(secondGiverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var firstGiver));
                using (new AllowedThread())
                {
                    giver.StayingInSettlement = firstGiver.CurrentSettlement;
                    giver.Clan = firstGiver.Clan;
                    var issue = new Issue(giver, DefaultItems.Tools);
                    issue.StringId = "tools_second";
                    issue._issueState = IssueBase.IssueState.Ongoing;
                    Campaign.Current.IssueManager._issues.Add(giver, issue);
                    giver.OnIssueCreatedForHero(issue);
                }
            });
        }

        var client = anotherPlayer ? ClientB : ClientA;
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(secondGiverId, out var giver));
            giver.Issue.StartIssueWithQuest();
            using (new AllowedThread())
            {
                giver.Issue.IssueQuest.StartQuest();
            }
        }, QuestLogTexts);

        Server.Call(() =>
        {
            var owns = Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(secondGiverId, "tools_second", out var owner);
            Assert.Equal(anotherPlayer, owns);
            if (anotherPlayer) Assert.Equal(ControllerB, owner);
        });
        Assert.Equal(ControllerA, OwnerOnServer());
        Assert.Equal(anotherPlayer ? 2 : 1, Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>().Count());
        Assert.Equal(anotherPlayer ? 0 : 1, Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>().Count());
    }

    [Fact]
    public void Accept_IsRefusedWhenTheVillageAlreadyHasTools()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var village));
            using (new AllowedThread()) village.ItemRoster.AddToCounts(DefaultItems.Tools, 1);
        });
        Accept();
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAccepted>());
    }

    [Fact]
    public void Accept_KeepsTheCapturedOfferAndGivesOnlyTheOwnerAQuest()
    {
        Accept();

        var accepterQuest = QuestOf(ClientA);
        var serverQuest = QuestOf(Server);

        Assert.Equal(ControllerA, OwnerOnServer());
        Assert.Equal(ServerPayment, accepterQuest.RewardGold);
        Assert.Equal(ServerPayment, serverQuest.RewardGold);
        Assert.Equal(accepterQuest._numberOfRequestedGood, serverQuest._numberOfRequestedGood);

        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.Null(notable.Issue.IssueQuest);
            Assert.True(notable.Issue.IsSolvingWithQuest);
        });
    }

    [Fact]
    public void CompanionFunding_PreconditionRefusesBeforeStartingWhenTheOwnerSpentTheQuotedFunds()
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            using (new MainHeroSubstitutionScope(owner, owner.PartyBelongedTo))
            {
                owner.Gold = 0;
                var strategy = Server.Container.Resolve<VillageNeedsToolsDescriptor>().AlternativeSolutionAcceptStrategy;
                var precondition = Assert.IsAssignableFrom<IAlternativeSolutionAcceptPrecondition>(strategy);
                Assert.False(precondition.CanAccept(notable.Issue, owner));
                Assert.Equal(0, owner.Gold);
                Assert.True(notable.Issue.IsOngoingWithoutQuest);
                Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out _));
                owner.Gold = ((Issue)notable.Issue).CostOfToolsForAlternativeSolution;
                Assert.True(precondition.CanAccept(notable.Issue, owner));
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerEnding_PreservesTheReasonInOnlyTheOwnersJournal(bool fail)
    {
        Accept();
        var ownerQuest = QuestOf(ClientA);
        var previousLogs = ownerQuest.JournalEntries.Count;
        const string reason = "Polisia was raided. Your agreement was canceled.";

        Server.Call(() =>
        {
            var quest = QuestOfHere(Server);
            var text = new TaleWorlds.Localization.TextObject("{=!}" + reason);
            if (fail) quest.CompleteQuestWithFail(text);
            else quest.CompleteQuestWithCancel(text);
        });

        Assert.True(ownerQuest.IsFinalized);
        Assert.Equal(previousLogs + 1, ownerQuest.JournalEntries.Count);
        Assert.Equal(reason, ownerQuest.JournalEntries.Last().LogText.ToString());
        var journal = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageNeedsToolsJournal>());
        Assert.Equal(reason, journal.Text);
        Assert.Single(ClientA.InternalMessages.GetMessages<NetworkVillageNeedsToolsJournal>());
        Assert.Empty(ClientB.InternalMessages.GetMessages<NetworkVillageNeedsToolsJournal>());
        ClientB.Call(() => Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>()));
    }

    [Fact]
    public void CompanionFunding_RejectsTheRequestWithoutApplyingSentTroops()
    {
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                troop.Level = 16;
                troop.UpgradeTargets = Array.Empty<CharacterObject>();
            });
        }
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            owner.PartyBelongedTo.MemberRoster.AddToCounts(troop, 6);
            using (new AllowedThread()) owner.Gold = 0;
        });
        TestEnvironment.FlushCoalescer();
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var giver));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(ClientA.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            using (new AllowedThread())
            {
                giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }
            AlternativeSolutionPickerDriver.Commit(ClientA, giver.Issue, owner, owner.PartyBelongedTo, troop, 6);
            giver.Issue.AlternativeSolutionStartConsequence();
            giver.Issue.StartIssueWithAlternativeSolution();
        }, AlternativeSolutionLogTexts);
        TestEnvironment.FlushCoalescer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAcceptRejected>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var giver));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out _));
            Assert.True(giver.Issue.IsOngoingWithoutQuest);
            Assert.Empty(giver.Issue.AlternativeSolutionSentTroops.GetTroopRoster());
            Assert.Equal(0, owner.Gold);
            Assert.Equal(6, owner.PartyBelongedTo.MemberRoster.GetTroopCount(troop));
        });
    }

    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, 1)]
    public void SuccessBranch_RunsOnlyWhenTheOwnerBringsTheToolsToTheVillage(bool inVillage, bool carriesEnough, int expectedRuns)
    {
        Accept();

        using var completion = new MethodCallRecorder(AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess)));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            var quest = QuestOfHere(Server);

            using (new AllowedThread())
            {
                ownerParty.CurrentSettlement = inVillage ? settlement : null;
                ownerParty.ItemRoster.AddToCounts(DefaultItems.Tools, quest._numberOfRequestedGood - (carriesEnough ? 0 : 1));
            }

            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = ownerParty;

            try
            {
                var proof = (byte)VillageNeedsToolsBranch.DeliverTools;
                Assert.True(Descriptor(Server).FinalizationProofStrategy.TryRunBranch(quest, proof));
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }
        }, QuestLogTexts);

        Assert.Equal(expectedRuns, completion.CountFor(Server));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnersSuccess_PaysThePromisedGoldOrGoodsAndEndsTheIssueEverywhere(bool exchange)
    {
        if (exchange) SetExchangeOffer();
        Accept();

        var ownerQuest = QuestOf(ClientA);
        var amount = QuestOf(Server)._numberOfRequestedGood;
        var otherGoldBefore = 0;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherHero));

            using (new AllowedThread())
            {
                ownerHero._partyBelongedTo = ownerParty;
                ownerParty.CurrentSettlement = settlement;
                ownerParty.ItemRoster.AddToCounts(DefaultItems.Tools, amount);
                ownerHero.Gold = 10000;
                otherHero.Gold = 50;
            }

            otherGoldBefore = otherHero.Gold;
        });

        var successLogGetter = AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestSuccessLog));
        var journalHarmony = new Harmony("bannerlordcoop.3766.delivery-ending-journal-test");
        journalHarmony.Patch(successLogGetter,
            prefix: new HarmonyMethod(typeof(VillageNeedsToolsTests), nameof(GetSuccessLogPrefix)));
        try
        {
            ClientA.Call(() =>
            {
                var quest = (Quest)QuestOfHere(ClientA);
                quest.FinishQuestSuccess1();
            }, QuestLogTexts.Where(method => method != successLogGetter));
        }
        finally
        {
            journalHarmony.Unpatch(successLogGetter, HarmonyPatchType.Prefix, journalHarmony.Id);
        }

        var request = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Equal((byte)VillageNeedsToolsBranch.DeliverTools, request.Proof);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestSuccess, finalized.Outcome);

        var ending = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageNeedsToolsJournal>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueJournal>());
        Assert.Single(ClientA.InternalMessages.GetMessages<NetworkVillageNeedsToolsJournal>());
        Assert.Empty(ClientB.InternalMessages.GetMessages<NetworkVillageNeedsToolsJournal>());
        Assert.Equal("You brought the promised tools.", ending.Text);
        Assert.Contains(ownerQuest.JournalEntries, log => log.LogText?.ToString() == ending.Text);

        TestEnvironment.FlushCoalescer();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.Null(notable.Issue);
                // The server's hearth and giver power rewards reach every machine
                Assert.True(settlement.Village.Hearth == (exchange ? 690f : 670f), $"{Describe(instance)} hearth was {settlement.Village.Hearth}");
                Assert.True(notable.Power == 10f, $"{Describe(instance)} power was {notable.Power}");
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherHero));

            Assert.Equal(10000 + (exchange ? 0 : ServerPayment), ownerHero.Gold);
            Assert.Equal(otherGoldBefore, otherHero.Gold);
            Assert.Equal(0, ownerParty.ItemRoster.GetItemNumber(DefaultItems.Tools));
            Assert.Equal(exchange ? 12 : 0, ownerParty.ItemRoster.GetItemNumber(DefaultItems.Grain));
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out _));
        });

        ClientA.Call(() => Assert.DoesNotContain(Campaign.Current.QuestManager.Quests.OfType<Quest>(), quest => quest.IsOngoing));
    }

    [Fact]
    public void OwnersTimeout_RunsTheFailConsequencesOnTheServerOnly()
    {
        var ownerRelationBefore = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            ownerRelationBefore = ownerHero.GetRelation(notable);
        });
        Accept();

        ClientA.Call(() =>
        {
            QuestOfHere(ClientA).CompleteQuestWithTimeOut();
        }, QuestLogTexts);
        TestEnvironment.FlushCoalescer();

        var request = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Equal(IssueOutcome.QuestTimeOut, request.Outcome);

        // The timeout penalty is authoritative and is not repeated during client replay
        var finalized = Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>();
        Assert.Equal(IssueOutcome.QuestTimeOut, Assert.Single(finalized).Outcome);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.Null(notable.Issue);
                // Only the server's -30 is applied
                Assert.True(settlement.Village.Hearth == 620f, $"{Describe(instance)} hearth was {settlement.Village.Hearth}");
                Assert.True(notable.Power == -10f, $"{Describe(instance)} power was {notable.Power}");
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
                Assert.Equal(ownerRelationBefore - 5, ownerHero.GetRelation(notable));
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AlternativeSolution_ChargesTheOwnerAndReturnsTroopsWithTheVanillaReward(bool exchange)
    {
        if (exchange) SetExchangeOffer();
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var otherGoldBefore = 0;
        var expectedGold = 0;
        var expectedStartReward = 0;
        var grainBefore = new Dictionary<EnvironmentInstance, int>();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                troop.Level = 16;
                troop.UpgradeTargets = Array.Empty<CharacterObject>();
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));

            ownerParty.MemberRoster.AddToCounts(ownerHero.CharacterObject, 1);
            ownerParty.MemberRoster.AddToCounts(companion.CharacterObject, 1);
            ownerParty.MemberRoster.AddToCounts(troop, 6);

            using (new AllowedThread())
            {
                ownerHero._partyBelongedTo = ownerParty;
                ownerHero.Gold = 10000;
                otherHero.Gold = 50;
            }

            otherGoldBefore = otherHero.Gold;
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            var issue = (Issue)notable.Issue;
            expectedGold = 10000 - issue.CostOfToolsForAlternativeSolution + (exchange ? 0 : issue._payment) + issue.RewardGold;
            expectedStartReward = issue.RewardGold;
        });
        TestEnvironment.FlushCoalescer();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            party.MemberRoster.AddToCounts(companion.CharacterObject, -1);
        });
        TestEnvironment.FlushCoalescer();

        var startLogGetter = AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.AlternativeSolutionStartLog));
        var startLogHarmony = new Harmony("bannerlordcoop.3766.companion-start-quote-test");
        startLogHarmony.Patch(startLogGetter,
            prefix: new HarmonyMethod(typeof(VillageNeedsToolsTests), nameof(GetAlternativeStartLogWithLocalPricePrefix)));
        try
        {
            ClientA.Call(() =>
            {
                Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
                Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(ClientA.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
                var issue = notable.Issue;

                using (new AllowedThread())
                {
                    Campaign.Current.MainParty = ownerParty;
                    issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                }
                AlternativeSolutionPickerDriver.Commit(ClientA, issue, owner, ownerParty, troop, 6);

                issue.AlternativeSolutionStartConsequence();
                issue.StartIssueWithAlternativeSolution();
            }, AlternativeSolutionLogTexts.Where(method => method != startLogGetter));
            TestEnvironment.FlushCoalescer();
        }
        finally
        {
            startLogHarmony.Unpatch(startLogGetter, HarmonyPatchType.Prefix, startLogHarmony.Id);
        }

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            var issue = (Issue)notable.Issue;
            var expectedText = "Tools delivery reward: " + expectedStartReward + ".";
            Assert.Equal(expectedText, Assert.Single(issue.JournalEntries).LogText.ToString());
        });
        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.Equal(string.Empty, Assert.Single(notable.Issue.JournalEntries).LogText.ToString());
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Assert.Equal(ControllerA, OwnerOnServer());

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                var issue = (Issue)notable.Issue;

                Assert.True(issue.IsSolvingWithAlternative);
                Assert.Equal(ServerPayment, issue._payment);
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
                grainBefore[instance] = party.ItemRoster.GetItemNumber(DefaultItems.Grain);
            });
        }

        Issue ownerIssue = null;
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            ownerIssue = (Issue)notable.Issue;
        });
        var successLogGetter = AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.IssueAlternativeSolutionSuccessLog));
        var journalHarmony = new Harmony("bannerlordcoop.3766.companion-ending-journal-test");
        journalHarmony.Patch(successLogGetter,
            prefix: new HarmonyMethod(typeof(VillageNeedsToolsTests), nameof(GetAlternativeSuccessLogPrefix)));
        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                notable.Issue._failureChance = 0f;
                notable.Issue.CompleteIssueWithAlternativeSolution();
            }, AlternativeSolutionLogTexts.Where(method => method != successLogGetter));
        }
        finally
        {
            journalHarmony.Unpatch(successLogGetter, HarmonyPatchType.Prefix, journalHarmony.Id);
        }

        TestEnvironment.FlushCoalescer();
        Assert.Single(ClientA.InternalMessages.GetMessages<NetworkVillageNeedsToolsAlternativeJournal>());
        Assert.Empty(ClientB.InternalMessages.GetMessages<NetworkVillageNeedsToolsAlternativeJournal>());

        ClientA.Call(() =>
        {
            Assert.Contains(ownerIssue.JournalEntries, entry => entry.LogText?.ToString() == "Your companion delivered the promised tools.");
            var archived = Assert.Single(Campaign.Current.LogEntryHistory.GetGameActionLogs(
                (JournalLogEntry log) => log.IsRelatedTo(ownerIssue)));
            Assert.True(archived.IsEnded());
            Assert.False(archived.IsEndedUnsuccessfully());
            Assert.Contains(archived.GetEntries(), entry => entry.LogText?.ToString() == "Your companion delivered the promised tools.");
        });
        ClientB.Call(() => Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs(
            (JournalLogEntry log) => log.RelatedHero.StringId == ownerIssue.IssueOwner.StringId)));

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.Null(notable.Issue);
                Assert.Equal(700f, settlement.Village.Hearth);
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
                Assert.Equal(grainBefore[instance] + (exchange ? 12 : 0), party.ItemRoster.GetItemNumber(DefaultItems.Grain));
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherHero));

            Assert.Equal(expectedGold, ownerHero.Gold);
            Assert.Equal(otherGoldBefore, otherHero.Gold);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(party.MemberRoster.Contains(companion.CharacterObject));
            Assert.Equal(6, party.MemberRoster.GetTroopCount(troop));
        });
    }

    [Fact]
    public void OwnersClient_CountsToolsTheServerAddsRemovesAndClears()
    {
        Accept();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            var quest = (Quest)QuestOfHere(ClientA);

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = ownerParty;
                quest._numberOfToolsLog = quest.AddDiscreteLog(new TaleWorlds.Localization.TextObject("{=!}Tools"), new TaleWorlds.Localization.TextObject("{=!}Tools"), 0, quest._numberOfRequestedGood);
            }

            Assert.Equal(0, quest._numberOfToolsLog.CurrentProgress);

            ItemRosterPatch.AddToCountsOverride(ownerParty.ItemRoster, new EquipmentElement(DefaultItems.Tools), 1);

            Assert.Equal(1, quest._numberOfToolsLog.CurrentProgress);

            ItemRosterPatch.AddToCountsOverride(ownerParty.ItemRoster, new EquipmentElement(DefaultItems.Tools), quest._numberOfRequestedGood + 3);
            Assert.Equal(quest._numberOfRequestedGood, quest._numberOfToolsLog.CurrentProgress);

            ItemRosterPatch.AddToCountsOverride(ownerParty.ItemRoster, new EquipmentElement(DefaultItems.Tools), -5);
            Assert.Equal(quest._numberOfRequestedGood - 1, quest._numberOfToolsLog.CurrentProgress);

            ItemRosterPatch.ClearOverride(ownerParty.ItemRoster);

            Assert.Equal(0, quest._numberOfToolsLog.CurrentProgress);
        }, QuestLogTexts);
    }

    [Theory]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByRebellion, IssueOutcome.QuestCancel)]
    [InlineData(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, IssueOutcome.QuestFail)]
    public void AWarDeclaredOnTheServerEndsTheQuestEverywhere(DeclareWarAction.DeclareWarDetail detail, IssueOutcome outcome)
    {
        Accept();
        PutThePlayersInTheirOwnClan();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));

            DeclareWarAction.ApplyInternal(kingdom, playerClan, detail);
        }, QuestLogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(outcome, finalized.Outcome);

        TestEnvironment.FlushCoalescer();
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.Null(notable.Issue);
                // A failure costs the giver power once, on every machine
                var power = outcome == IssueOutcome.QuestFail ? -10f : 0f;
                Assert.True(notable.Power == power, $"{Describe(instance)} power was {notable.Power}");
            });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerWorldEvent_VisitsTheOngoingQuestOnceAsItsOwner(bool kingdomChange)
    {
        Accept();
        PutThePlayersInTheirOwnClan();
        var listener = AccessTools.Method(typeof(Quest), kingdomChange ? nameof(Quest.OnClanChangedKingdom) : nameof(Quest.OnWarDeclared));
        var unrelatedClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var anotherClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var harmony = new Harmony("bannerlordcoop.3766.world-event-count");
        WorldEventOwners.Clear();
        harmony.Patch(listener, prefix: new HarmonyMethod(typeof(VillageNeedsToolsTests), nameof(RecordWorldEventOwner)));
        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(giverClanId, out var giverClan));
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(unrelatedClanId, out var unrelatedClan));
                Assert.True(Server.ObjectManager.TryGetObject<Clan>(anotherClanId, out var anotherClan));
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var owner));
                if (kingdomChange)
                    CampaignEventDispatcher.Instance.OnClanChangedKingdom(playerClan, null, null,
                        ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
                else
                    CampaignEventDispatcher.Instance.OnWarDeclared(unrelatedClan, anotherClan,
                        DeclareWarAction.DeclareWarDetail.CausedByRebellion);

                Assert.True(QuestOfHere(Server).IsOngoing);
                Assert.Same(owner, Assert.Single(WorldEventOwners));
            }, QuestLogTexts);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        }
        finally
        {
            harmony.Unpatch(listener, HarmonyPatchType.Prefix, harmony.Id);
            WorldEventOwners.Clear();
        }
    }

    private static void RecordWorldEventOwner()
    {
        if (Common.ModInformation.IsServer) WorldEventOwners.Add(Hero.MainHero);
    }

    [Fact]
    public void AWarReplayedOnTheOwnersClientLeavesTheQuestToTheServer()
    {
        Accept();
        PutThePlayersInTheirOwnClan();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(ClientA.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
            var quest = (Quest)QuestOfHere(ClientA);

            using (new AllowedThread())
            {
                FactionManager.DeclareWar(kingdom, playerClan);
                quest.OnWarDeclared(kingdom, playerClan, DeclareWarAction.DeclareWarDetail.CausedByRebellion);
            }

            Assert.True(quest.IsOngoing);
        }, QuestLogTexts);

        Assert.Empty(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.NotNull(notable.Issue);
            });
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerRaid_CancelsOnlyIfItIsTheGiversVillage(bool giverVillage)
    {
        Accept();
        var ownerQuest = QuestOf(ClientA);
        var mapEventId = TestEnvironment.CreateRegisteredObject<MapEvent>();
        var otherVillageId = TestEnvironment.CreateRegisteredObject<Settlement>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(giverVillage ? settlementId : otherVillageId, out var settlement));
            mapEvent.MapEventSettlement = settlement;
            CampaignEventDispatcher.Instance.RaidCompleted(BattleSideEnum.Attacker, new RaidEventComponent(mapEvent));
        }, QuestLogTexts.Concat(new[] { AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.VillageRaidedQuestCancelLog)) }));

        var finalized = Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>();
        if (giverVillage)
        {
            Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(finalized).Outcome);
            Assert.True(ownerQuest.IsFinalized);
        }
        else
        {
            Assert.Empty(finalized);
            Assert.True(QuestOf(ClientA).IsOngoing);
        }
    }

    [Fact]
    public void JoiningAHostileKingdomOnTheServer_CancelsTheOwnersQuest()
    {
        Accept();
        PutThePlayersInTheirOwnClan();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
            using (new AllowedThread()) FactionManager.DeclareWar(kingdom, playerClan);
            CampaignEventDispatcher.Instance.OnClanChangedKingdom(playerClan, null, kingdom,
                ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
        }, QuestLogTexts);
        Assert.Equal(IssueOutcome.QuestCancel, Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>()).Outcome);
    }

    [Theory]
    [InlineData(MapEvent.BattleTypes.IsForcingSupplies, true)]
    [InlineData(MapEvent.BattleTypes.IsForcingVolunteers, true)]
    [InlineData(MapEvent.BattleTypes.FieldBattle, false)]
    public void ServerCoercion_FailsTheOwnersQuestForSuppliesOrRecruits(MapEvent.BattleTypes type, bool fails)
    {
        Accept();
        var mapEventId = TestEnvironment.CreateRegisteredObject<MapEvent>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            mapEvent.MapEventSettlement = settlement;
            mapEvent._mapEventType = type;
            CampaignEventDispatcher.Instance.OnMapEventStarted(mapEvent, party.Party, settlement.Party);
        }, QuestLogTexts.Concat(new[]
        {
            AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
            AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.EncyclopediaLinkWithName)),
        }));

        var finalized = Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>();
        if (fails) Assert.Equal(IssueOutcome.QuestFail, Assert.Single(finalized).Outcome);
        else Assert.Empty(finalized);
    }

    private void Accept()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            notable.Issue.StartIssueWithQuest();
            var quest = Assert.IsType<Quest>(notable.Issue.IssueQuest);
            using (new AllowedThread())
            {
                quest.StartQuest();
            }
            quest._numberOfToolsLog = quest.AddDiscreteLog(new TaleWorlds.Localization.TextObject("{=!}Tools"), new TaleWorlds.Localization.TextObject("{=!}Tools"), 0, quest._numberOfRequestedGood);
        }, QuestLogTexts);
    }

    private void SetExchangeOffer()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                AccessTools.Field(typeof(Issue), nameof(Issue._exchangeItem)).SetValue(notable.Issue, DefaultItems.Grain);
                AccessTools.Field(typeof(Issue), nameof(Issue._numberOfExchangeItem)).SetValue(notable.Issue, 12);
            });
        }
    }

    private Quest QuestOf(EnvironmentInstance instance)
    {
        Quest quest = null!;
        instance.Call(() => quest = QuestOfHere(instance));
        return quest;
    }

    private Quest QuestOfHere(EnvironmentInstance instance)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
        return Assert.IsType<Quest>(notable.Issue.IssueQuest);
    }

    private string OwnerOnServer()
    {
        var controllerId = string.Empty;

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out controllerId));
        });

        return controllerId;
    }

    private string Describe(EnvironmentInstance instance)
    {
        return instance == Server ? "server" : instance == ClientA ? "owner" : "observer";
    }

    private IQuestTypeDescriptor Descriptor(EnvironmentInstance instance)
    {
        Assert.True(instance.Container.Resolve<IQuestTypeRegistry>().TryGetByName(IssueName, out var descriptor));
        return descriptor;
    }

    private void PutThePlayersInTheirOwnClan()
    {
        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));

                using (new AllowedThread())
                {
                    ownerHero.Clan = playerClan;
                }
            });
        }
    }

    private (string HeroId, string PartyId) RegisterPlayer(EnvironmentInstance client, string controllerId)
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(new Player(controllerId, heroId, partyId, "", ""))));
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        return (heroId, partyId);
    }

    private void GiveTheNotableTheIssue(EnvironmentInstance instance, int payment)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(boundSettlementId, out var boundSettlement));
            Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(giverClanId, out var giverClan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                settlement.SetSettlementComponent(village);
                boundSettlement.SetSettlementComponent(town);
                town._isCastle = true;
                town.OwnerClan = giverClan;
                village.Bound = boundSettlement;
                village.Hearth = 650f;
                notable.StayingInSettlement = settlement;
                notable.Occupation = Occupation.RuralNotable;

                // Gives the notable a map faction for the war tests
                giverClan._kingdom = kingdom;
                notable.Clan = giverClan;

                var issue = new Issue(notable, DefaultItems.Tools);
                PaymentField.SetValue(issue, payment);
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });
    }

    private static bool GetConversationHeroPrefix(ref Hero __result)
    {
        __result = ConversationHeroOverride;
        return false;
    }

    private static bool GetSuccessLogPrefix(ref TaleWorlds.Localization.TextObject __result)
    {
        __result = new TaleWorlds.Localization.TextObject("{=!}You brought the promised tools.");
        return false;
    }

    private static bool GetAlternativeStartLogWithLocalPricePrefix(Issue __instance, ref TaleWorlds.Localization.TextObject __result)
    {
        // Reproduce divergent local settlement prices without changing the server's reward calculation.
        var localReward = __instance.RewardGold - (Common.ModInformation.IsServer ? 0 : 350);
        __result = new TaleWorlds.Localization.TextObject("{=!}Tools delivery reward: {REWARD_GOLD}.");
        __result.SetTextVariable("REWARD_GOLD", localReward);
        return false;
    }

    private static bool GetAlternativeSuccessLogPrefix(ref TaleWorlds.Localization.TextObject __result)
    {
        __result = new TaleWorlds.Localization.TextObject("{=!}Your companion delivered the promised tools.");
        return false;
    }
}
