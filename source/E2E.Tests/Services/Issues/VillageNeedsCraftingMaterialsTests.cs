using Autofac;
using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Finalization;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.VillageNeedsCraftingMaterials;
using GameInterface.Services.ItemRosters.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Helpers;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.MapEvents;
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
    private const string SecondIssueId = "issue_village_needs_crafting_materials_second";
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
        AccessTools.PropertyGetter(typeof(Quest), nameof(Quest.QuestGiverVillageRaidedLogText)),
        AccessTools.PropertyGetter(typeof(Settlement), nameof(Settlement.EncyclopediaLinkWithName)),
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
        // The patched quest text inlines this helper on the server.
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
    };

    private static readonly MethodBase[] AlternativeSolutionLogTexts =
    {
        AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.AlternativeSolutionStartLog)),
        AccessTools.PropertyGetter(typeof(Issue), nameof(Issue.IssueAlternativeSolutionSuccessLog)),
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.SetCharacterProperties)),
        // Development patches the alternative solution end, so Harmony inlines SetCharacterProperties into it
        AccessTools.Method(typeof(StringHelpers), nameof(StringHelpers.GetCharacterProperties)),
    };

    private readonly string notableId;
    private readonly string secondNotableId;
    private readonly string settlementId;
    private readonly string villageId;
    private readonly string boundTownSettlementId;
    private readonly string boundTownId;
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
        secondNotableId = TestEnvironment.CreateRegisteredObject<Hero>();
        settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        villageId = TestEnvironment.CreateRegisteredObject<Village>();
        boundTownSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        boundTownId = TestEnvironment.CreateRegisteredObject<Town>();
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
        var encounterSettlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

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

        // Another player's encounter is what Settlement.CurrentSettlement reads first on the server
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(encounterSettlementId, out var encounterSettlement));

            var encounter = ObjectHelper.SkipConstructor<PlayerEncounter>();
            encounter.EncounterSettlementAux = encounterSettlement;
            Campaign.Current.PlayerEncounter = encounter;
        });

        ClientA.Call(() =>
        {
            var quest = (Quest)QuestOfHere(ClientA);
            quest.Success();
        }, QuestLogTexts);

        Server.Call(() => Campaign.Current.PlayerEncounter = null);

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
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(encounterSettlementId, out var encounterSettlement));
            Assert.Equal(0, encounterSettlement.Party.ItemRoster.GetItemNumber(DefaultItems.IronIngot1));
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
    public void RaidAtOneOwnersVillage_CancelsThatQuestOnceAndLeavesTheOtherOwnerQuestOngoing()
    {
        foreach (var instance in Instances)
        {
            GiveTheNotableTheIssue(instance, secondNotableId, SecondIssueId, ServerPayment);
        }

        Accept();
        Accept(ClientB, secondNotableId);

        Assert.True(QuestOf(Server).IsOngoing);
        Assert.True(QuestOf(Server, secondNotableId).IsOngoing);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(boundTownSettlementId, out var otherGiverSettlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(secondNotableId, out var secondNotable));

            // Keep the second owner's giver away from the raided village so only the first quest matches.
            secondNotable.Issue.IssueQuest.QuestGiver.StayingInSettlement = otherGiverSettlement;

            var raid = (RaidEventComponent)RuntimeHelpers.GetUninitializedObject(typeof(RaidEventComponent));
            raid.MapEvent = VillageMapEvent(Server, settlementId, MapEvent.BattleTypes.Raid);
            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = null;

            try
            {
                CampaignEventDispatcher.Instance.RaidCompleted(BattleSideEnum.Attacker, raid);
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }
        }, QuestLogTexts);

        var finalized = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>(),
            message => message.IssueOwnerId == notableId && message.IssueId == IssueId);
        Assert.Equal(IssueOutcome.QuestCancel, finalized.Outcome);
        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkIssueFinalized>(),
            message => message.IssueOwnerId == secondNotableId && message.IssueId == SecondIssueId);

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(secondNotableId, out var secondNotable));
                Assert.Null(notable.Issue);
                Assert.NotNull(secondNotable.Issue);
                Assert.True(secondNotable.Issue.IsSolvingWithQuest);

                if (ReferenceEquals(instance, Server) || ReferenceEquals(instance, ClientB))
                {
                    Assert.NotNull(secondNotable.Issue.IssueQuest);
                    Assert.True(secondNotable.Issue.IssueQuest.IsOngoing);
                }
                else
                {
                    Assert.Null(secondNotable.Issue.IssueQuest);
                }
            });
        }
    }

    [Fact]
    public void AlternativeSolution_KeepsTheServersPromiseAndPaysItToTheOwnerOnce()
    {
        var companionId = TestEnvironment.CreateRegisteredObject<Hero>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var otherGoldBefore = 0;

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
                ownerHero.Gold = 0;
                otherHero.Gold = 50;
            }

            otherGoldBefore = otherHero.Gold;
        });
        TestEnvironment.FlushCoalescer();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.True(ClientA.ObjectManager.TryGetObject<MobileParty>(ownerPartyId, out var ownerParty));
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(ClientA.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var issue = notable.Issue;

            using (new AllowedThread())
            {
                Campaign.Current.MainParty = ownerParty;
                ownerParty.MemberRoster.AddToCounts(companion.CharacterObject, -1);
                ownerParty.MemberRoster.AddToCounts(troop, -6);
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                issue.AlternativeSolutionSentTroops.AddToCounts(troop, 6);
            }

            ClientA.Resolve<IMessageBroker>().Publish(this, new AlternativeSolutionAcceptRequested(issue));
        }, AlternativeSolutionLogTexts);
        TestEnvironment.FlushCoalescer();

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAlternativeSolutionAccepted>());
        Assert.Equal(ControllerA, OwnerOnServer());

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                var issue = (Issue)notable.Issue;

                // The accepting client quoted its own market price, the server's promise replaces it
                Assert.True(issue.IsSolvingWithAlternative);
                Assert.Equal(ServerPayment, issue._promisedPayment);
            });
        }

        // Vanilla's daily tick reads the first journal entry of every issue solved by an alternative
        ClientB.Call(() =>
        {
            Assert.True(ClientB.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            Assert.Single(notable.Issue.JournalEntries);

            Campaign.Current.IssueManager.DailyTick();

            Assert.True(notable.Issue.IsSolvingWithAlternative);

            // The day before the troops return vanilla logs it, which only the owner may see
            notable.Issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.Now;
            Campaign.Current.IssueManager.DailyTick();

            Assert.Single(notable.Issue.JournalEntries);
            Assert.True(notable.Issue._areIssueEffectsResolved);
        }, AlternativeSolutionLogTexts);

        // The owner still hears its troops are on their way back
        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            var entriesBefore = notable.Issue.JournalEntries.Count;

            notable.Issue.AlternativeSolutionIssueEffectClearTime = CampaignTime.Now;
            Campaign.Current.IssueManager.DailyTick();

            Assert.Equal(entriesBefore + 1, notable.Issue.JournalEntries.Count);
            Assert.True(notable.Issue._areIssueEffectsResolved);
        }, AlternativeSolutionLogTexts);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
            notable.Issue._failureChance = 0f;
            notable.Issue.CompleteIssueWithAlternativeSolution();
        }, AlternativeSolutionLogTexts);
        TestEnvironment.FlushCoalescer();

        foreach (var instance in Instances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(notableId, out var notable));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.Null(notable.Issue);
                Assert.Equal(710f, settlement.Village.Hearth);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerHeroId, out var ownerHero));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(otherHeroId, out var otherHero));

            Assert.Equal(ServerPayment, ownerHero.Gold);
            Assert.Equal(otherGoldBefore, otherHero.Gold);
        });
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

    [Theory]
    [InlineData("war")]
    [InlineData("kingdom")]
    [InlineData("raid")]
    [InlineData("coercion")]
    public void AWorldEventReplayedOnTheOwnersClientLeavesTheQuestToTheServer(string listener)
    {
        Accept();
        PutThePlayersInTheirOwnClan();

        ClientA.Call(() =>
        {
            Assert.True(ClientA.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(ClientA.ObjectManager.TryGetObject<Clan>(playerClanId, out var playerClan));
            var quest = (Quest)QuestOfHere(ClientA);

            // Vanilla reads the local main hero, which must be at war with the giver for the war listeners to act
            using (new AllowedThread())
            {
                Hero.MainHero.Clan = playerClan;
                FactionManager.DeclareWar(kingdom, playerClan);
            }

            switch (listener)
            {
                // War and kingdom changes reach a client as replays, where the framework drops any ending they cause
                case "war":
                    using (new AllowedThread())
                    {
                        quest.OnWarDeclared(kingdom, playerClan, DeclareWarAction.DeclareWarDetail.CausedByRebellion);
                    }
                    break;
                case "kingdom":
                    using (new AllowedThread())
                    {
                        quest.OnClanChangedKingdom(playerClan, null, null, ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom, false);
                    }
                    break;
                case "raid":
                    var raid = (RaidEventComponent)RuntimeHelpers.GetUninitializedObject(typeof(RaidEventComponent));
                    raid.MapEvent = VillageMapEvent(ClientA, settlementId, MapEvent.BattleTypes.Raid);
                    quest.OnRaidCompleted(BattleSideEnum.Attacker, raid);
                    break;
                case "coercion":
                    var village = Assert.IsType<Settlement>(quest.QuestGiver.CurrentSettlement);
                    village._notablesCache.Add(quest.QuestGiver);
                    quest.OnMapEventStarted(VillageMapEvent(ClientA, settlementId, MapEvent.BattleTypes.IsForcingSupplies), PartyBase.MainParty, null);
                    break;
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

    // A map event at the giver's village, so the quest's vanilla listener would act on it
    private MapEvent VillageMapEvent(EnvironmentInstance instance, string targetSettlementId, MapEvent.BattleTypes type)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var village));

        var mapEvent = (MapEvent)RuntimeHelpers.GetUninitializedObject(typeof(MapEvent));
        mapEvent._mapEventType = type;
        mapEvent.MapEventSettlement = village;
        return mapEvent;
    }

    private void Accept()
    {
        Accept(ClientA, notableId);
    }

    private void Accept(EnvironmentInstance client, string targetNotableId)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(targetNotableId, out var notable));
            notable.Issue.StartIssueWithQuest();
        }, QuestLogTexts);
    }

    private Quest QuestOf(EnvironmentInstance instance)
    {
        return QuestOf(instance, notableId);
    }

    private Quest QuestOf(EnvironmentInstance instance, string targetNotableId)
    {
        Quest quest = null!;
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(targetNotableId, out var notable));
            quest = Assert.IsType<Quest>(notable.Issue.IssueQuest);
        });
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
        GiveTheNotableTheIssue(instance, notableId, IssueId, payment);
    }

    private void GiveTheNotableTheIssue(EnvironmentInstance instance, string targetNotableId, string targetIssueId, int payment)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(targetNotableId, out var notable));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(giverClanId, out var giverClan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(boundTownSettlementId, out var boundTownSettlement));
            Assert.True(instance.ObjectManager.TryGetObject<Town>(boundTownId, out var boundTown));

            using (new AllowedThread())
            {
                // The village's map faction is its bound town's, which belongs to the giver's kingdom
                boundTownSettlement.SetSettlementComponent(boundTown);
                boundTown._ownerClan = giverClan;
                settlement.SetSettlementComponent(village);
                village.Bound = boundTownSettlement;
                village.Hearth = 650f;
                notable.StayingInSettlement = settlement;
                notable.Occupation = Occupation.RuralNotable;

                // Gives the notable a map faction for the war tests
                giverClan._kingdom = kingdom;
                notable.Clan = giverClan;

                var issue = new Issue(notable);
                RequestedItemField.SetValue(issue, DefaultItems.IronIngot1);
                issue._promisedPayment = payment;
                issue.StringId = targetIssueId;
                issue._issueState = IssueBase.IssueState.Ongoing;

                Campaign.Current.IssueManager._issues.Add(notable, issue);
                notable.OnIssueCreatedForHero(issue);
            }
        });
    }
}
