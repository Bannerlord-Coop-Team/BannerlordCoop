using Autofac;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest;

namespace E2E.Tests.Services.Issues;

public class VillageNeedsCraftingMaterialsTests : IDisposable
{
    private const string IssueName = "VillageNeedsCraftingMaterialsIssue";
    private const string IssueId = "issue_village_needs_crafting_materials";
    private const string ControllerA = "player-A";
    private const string ControllerB = "player-B";
    private const int ServerPayment = 900;
    private const int AccepterPayment = 1234;

    private static readonly FieldInfo RequestedItemField = AccessTools.Field(typeof(Issue), nameof(Issue._requestedItem));

    private static readonly MethodBase[] QuestLogTexts =
    {
        AccessTools.Method(typeof(Quest), nameof(Quest.SetDialogs)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestStartedLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestSuccessLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestCanceledWarDeclaredLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.PlayerHasNeededItemsLogText)),
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestFailedWithTimeOutLogText)),
    };

    private readonly string notableId;
    private readonly string settlementId;
    private readonly string villageId;
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

    public VillageNeedsCraftingMaterialsTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);

        notableId = TestEnvironment.CreateRegisteredObject<Hero>();
        settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        villageId = TestEnvironment.CreateRegisteredObject<Village>();
        giverClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        playerClanId = TestEnvironment.CreateRegisteredObject<Clan>();

        (ownerHeroId, ownerPartyId) = RegisterPlayer(ClientA, ControllerA);
        (otherHeroId, _) = RegisterPlayer(ClientB, ControllerB);

        GiveTheNotableTheIssue(Server, ServerPayment);
        GiveTheNotableTheIssue(ClientA, AccepterPayment);
        GiveTheNotableTheIssue(ClientB, ServerPayment);
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
    public void CreationCapture_GivesTheClientTheServersRequestedMaterial()
    {
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        byte[] captured = null!;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
            var issue = (Issue)RuntimeHelpers.GetUninitializedObject(typeof(Issue));
            RequestedItemField.SetValue(issue, item);

            captured = Descriptor(Server).CreationCaptureStrategy.Capture(issue);
        });

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(ClientA.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));

            using (new AllowedThread())
            {
                var issue = (Issue)Descriptor(ClientA).CreationCaptureStrategy.CreateIssue(notable, captured);

                Assert.Same(item, issue._requestedItem);
            }
        });
    }

    [Fact]
    public void Accept_MakesTheAccepterTheOwnerAndForcesItsPaymentOntoTheServer()
    {
        Accept();

        var accepterQuest = QuestOf(ClientA);
        var serverQuest = QuestOf(Server);

        Assert.Equal(ControllerA, OwnerOnServer());
        Assert.Equal(AccepterPayment, accepterQuest.RewardGold);
        Assert.Equal(AccepterPayment, serverQuest.RewardGold);
        Assert.Equal(accepterQuest._requestedItemAmount, serverQuest._requestedItemAmount);

        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.Null(notable.Issue.IssueQuest);
            Assert.True(notable.Issue.IsSolvingWithQuest);
        });
    }

    [Fact]
    public void AlternativeAcceptCapture_ForcesThePromisedPaymentOntoTheMirror()
    {
        Server.Call(() =>
        {
            var strategy = Descriptor(Server).AlternativeSolutionAcceptStrategy;
            var serverIssue = (Issue)RuntimeHelpers.GetUninitializedObject(typeof(Issue));
            var mirrorIssue = (Issue)RuntimeHelpers.GetUninitializedObject(typeof(Issue));
            serverIssue._promisedPayment = 1500;
            mirrorIssue._promisedPayment = 700;

            strategy.Apply(mirrorIssue, strategy.Capture(serverIssue));

            Assert.Equal(1500, mirrorIssue._promisedPayment);
        });
    }

    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, true, 1)]
    public void SuccessBranch_RunsOnlyWhenTheOwnerBringsTheMaterialsToTheVillage(bool inVillage, bool carriesEnough, int expectedRuns)
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
                ownerParty.ItemRoster.AddToCounts(DefaultItems.IronIngot1, quest._requestedItemAmount - (carriesEnough ? 0 : 1));
            }

            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = ownerParty;

            try
            {
                var proof = (byte)VillageNeedsCraftingMaterialsBranch.SuccessByHandingOverMaterials;
                Assert.True(Descriptor(Server).FinalizationProofStrategy.TryRunBranch(quest, proof));
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }
        }, QuestLogTexts);

        Assert.Equal(expectedRuns, completion.CountFor(Server));
    }

    [Fact]
    public void OwnersSuccess_IsHandedToTheServerWhichPaysTheOwnerAndEndsTheIssueEverywhere()
    {
        Accept();

        var amount = QuestOf(Server)._requestedItemAmount;
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
                ownerParty.ItemRoster.AddToCounts(DefaultItems.IronIngot1, amount);
                ownerHero.Gold = 0;
                otherHero.Gold = 50;
            }

            otherGoldBefore = otherHero.Gold;
        });

        ClientA.Call(() =>
        {
            var quest = (Quest)QuestOfHere(ClientA);
            quest.Success();
        }, QuestLogTexts);

        var request = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestBranch>());
        Assert.Equal((byte)VillageNeedsCraftingMaterialsBranch.SuccessByHandingOverMaterials, request.Proof);

        var finalized = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>());
        Assert.Equal(IssueOutcome.QuestSuccess, finalized.Outcome);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.Null(notable.Issue);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherHero));

            Assert.Equal(AccepterPayment, ownerHero.Gold);
            Assert.Equal(otherGoldBefore, otherHero.Gold);
            Assert.Equal(0, ownerParty.ItemRoster.GetItemNumber(DefaultItems.IronIngot1));
            Assert.Equal(amount, settlement.Party.ItemRoster.GetItemNumber(DefaultItems.IronIngot1));
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(notableId, IssueId, out _));
        });
    }

    [Fact]
    public void OwnersTimeout_RunsTheFailConsequencesOnTheServerOnly()
    {
        Accept();

        ClientA.Call(() =>
        {
            QuestOfHere(ClientA).CompleteQuestWithTimeOut();
        }, QuestLogTexts);

        var request = Assert.Single(ClientA.NetworkSentMessages.GetMessages<RequestQuestOutcome>());
        Assert.Equal(IssueOutcome.QuestTimeOut, request.Outcome);

        // Vanilla fails the quest from inside its timeout, the fail is announced first
        var finalized = Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>();
        Assert.Equal(IssueOutcome.QuestFail, finalized.First().Outcome);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.Null(notable.Issue);
                // The server's -40 replicates, the owner's client must not apply its own on top
                Assert.Equal(610f, settlement.Village.Hearth);
            });
        }
    }

    [Fact]
    public void OwnersClient_CountsMaterialsTheServerAddsToItsParty()
    {
        Accept();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            var quest = (Quest)QuestOfHere(ClientA);

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = ownerParty;
                quest.QuestAcceptedConsequences();
            }

            Assert.Equal(0, quest._playerAcceptedQuestLog.CurrentProgress);

            ItemRosterPatch.AddToCountsOverride(ownerParty.ItemRoster, new EquipmentElement(DefaultItems.IronIngot1), 1);

            Assert.Equal(1, quest._playerAcceptedQuestLog.CurrentProgress);

            ItemRosterPatch.ClearOverride(ownerParty.ItemRoster);

            Assert.Equal(0, quest._playerAcceptedQuestLog.CurrentProgress);
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

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.Null(notable.Issue);
            });
        }
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

    private void Accept()
    {
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            notable.Issue.StartIssueWithQuest();
        }, QuestLogTexts);
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
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(giverClanId, out var giverClan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                settlement.SetSettlementComponent(village);
                village.Bound = settlement;
                village.Hearth = 650f;
                notable.StayingInSettlement = settlement;

                // Gives the notable a map faction for the war tests
                giverClan._kingdom = kingdom;
                notable.Clan = giverClan;

                var issue = new Issue(notable);
                RequestedItemField.SetValue(issue, DefaultItems.IronIngot1);
                issue._promisedPayment = payment;
                issue.StringId = IssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });
    }
}
