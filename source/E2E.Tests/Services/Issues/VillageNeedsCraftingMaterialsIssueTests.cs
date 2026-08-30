using Common.Messaging;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.AcceptMirror;
using GameInterface.Services.Issues.Generic.Migrated.VillageNeedsCraftingMaterials;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class VillageNeedsCraftingMaterialsIssueTests : IDisposable
{
    private static readonly PropertyInfo PlayerProgressProperty =
        AccessTools.Property(typeof(Campaign), nameof(Campaign.PlayerProgress));
    private static readonly FieldInfo GameModelsField = AccessTools.Field(typeof(Campaign), "_gameModels");
    private static readonly PropertyInfo CharacterDevelopmentModelProperty =
        AccessTools.Property(typeof(GameModels), nameof(GameModels.CharacterDevelopmentModel));
    private static readonly PropertyInfo PlayerTraitDeveloperProperty =
        AccessTools.Property(typeof(Campaign), nameof(Campaign.PlayerTraitDeveloper));

    private static void InstallCharacterDevelopmentModel()
    {
        var models = (GameModels)GameModelsField.GetValue(Campaign.Current);
        if (models == null)
        {
            models = ObjectHelper.SkipConstructor<GameModels>();
            GameModelsField.SetValue(Campaign.Current, models);
        }

        CharacterDevelopmentModelProperty.SetValue(models, new DefaultCharacterDevelopmentModel());
        PlayerTraitDeveloperProperty.SetValue(Campaign.Current, Campaign.Current.PlayerTraitDeveloper ?? new PropertyOwner<PropertyObject>());
    }

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();
    private EnvironmentInstance OtherClient => TestEnvironment.Clients.Last();
    private IEnumerable<EnvironmentInstance> AllInstances => new[] { Server }.Concat(TestEnvironment.Clients);

    public VillageNeedsCraftingMaterialsIssueTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record CraftingFixture(string HeroId, string SettlementId);

    private void RegisterDefaultCraftingMaterialItemsOnClients()
    {
        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.AddExisting(DefaultItems.IronIngot1.StringId, DefaultItems.IronIngot1));
                Assert.True(client.ObjectManager.AddExisting(DefaultItems.IronIngot2.StringId, DefaultItems.IronIngot2));
            });
        }
    }

    private CraftingFixture SetupIssueOwner()
    {
        RegisterDefaultCraftingMaterialItemsOnClients();

        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    InstallCharacterDevelopmentModel();

                    hero.StayingInSettlement = settlement;
                }
            });
        }

        return new CraftingFixture(heroId, settlementId);
    }

    private void ForcePromisedPayment(EnvironmentInstance instance, string ownerId, int payment)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            var issue = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);

            using (new AllowedThread())
            {
                issue._promisedPayment = payment;
            }
        });
    }

    private void ForcePromisedPaymentEverywhere(string ownerId, int payment = 500)
    {
        foreach (var instance in AllInstances)
        {
            ForcePromisedPayment(instance, ownerId, payment);
        }
    }

    private void OpenConversation(EnvironmentInstance instance, string ownerId, string controllerId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            MessageBroker.Instance.Publish(owner, new IssueConversationOpenedLocally(owner, controllerId));
        });
    }

    private void CreateIssueOnServer(string ownerId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) =>
                {
                    var issue = new VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue(h);
                    if (!Server.ObjectManager.Contains(issue._requestedItem))
                    {
                        Assert.True(Server.ObjectManager.AddExisting(issue._requestedItem.StringId, issue._requestedItem));
                    }
                    return issue;
                },
                typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue),
                IssueBase.IssueFrequency.Rare);

            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
        });
    }

    [Fact]
    public void GenuineServerCreation_CapturesTheRolledRequestedItemAndReplicatesAByteIdenticalIssueToEveryClient()
    {
        var fixture = SetupIssueOwner();

        CreateIssueOnServer(fixture.HeroId);

        var created = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueCreated>());
        Assert.Equal(fixture.HeroId, created.OwnerId);
        Assert.True(created.RequestedItemId is "ironIngot1" or "ironIngot2",
            $"Expected one of the two real SelectCraftingMaterial() variants, got {created.RequestedItemId}");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var serverIssue = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);
            Assert.True(Server.ObjectManager.TryGetId(serverIssue._requestedItem, out var serverItemId));
            Assert.Equal(created.RequestedItemId, serverItemId);
        });

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var mirrored = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue>(owner.Issue);

                Assert.True(client.ObjectManager.TryGetObject<ItemObject>(created.RequestedItemId, out var requestedItem));
                Assert.Same(requestedItem, mirrored._requestedItem);
            });
        }
    }

    [Fact]
    public void ClientOriginatedCreation_IsBlocked_IssueManagerNeverCreatesIt()
    {
        var fixture = SetupIssueOwner();

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            var pid = new PotentialIssueData(
                (in PotentialIssueData _, Hero h) => new VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue(h),
                typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue),
                IssueBase.IssueFrequency.Rare);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
            Assert.Null(owner.Issue);
        });

        Assert.Empty(Client.NetworkSentMessages.GetMessages<NetworkVillageCraftingIssueCreated>());
    }

    [Fact]
    public void QuestOwnershipGate_BlocksTurnInForAnyoneOtherThanTheRecordedOwner_EvenWithTheMaterialsInHand()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest quest = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "host-controller");
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Campaign.Current.MainParty = party;

            party.ItemRoster.AddToCounts(quest._requestedItem, quest._requestedItemAmount);

            quest.QuestAcceptedConsequences();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("host-controller", ownerControllerId);
        });

        Server.Call(() =>
        {
            Assert.True(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.Null(explanation);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("someone-else");
        Server.Call(() =>
        {
            Assert.False(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.NotNull(explanation);
        });

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            party.ItemRoster.AddToCounts(quest._requestedItem, -quest._requestedItemAmount);
            quest.UpdateQuestLog();
        });
        Server.Call(() =>
        {
            Assert.False(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.NotNull(explanation);
        });
    }

    [Fact]
    public void RemoteClientAccept_ForceCorrectsQuantityAndRewardOnEveryPeer_IncludingTheAccepterItself_WhenIssueDifficultyMultiplierDiverges()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 1.0f));
        Client.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.1f));
        OtherClient.Call(() => PlayerProgressProperty.SetValue(Campaign.Current, 0.55f));

        ForcePromisedPayment(Server, fixture.HeroId, 2000);
        ForcePromisedPayment(Client, fixture.HeroId, 500);
        ForcePromisedPayment(OtherClient, fixture.HeroId, 800);

        OpenConversation(Client, fixture.HeroId, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
        });

        Assert.Single(Client.InternalMessages.GetMessages<QuestTypeQuestSolutionAcceptTriggered>());

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        var acceptedFields = GenericAcceptFieldsSerializer.Deserialize<VillageNeedsCraftingMaterialsAcceptFields>(accepted.FieldsBytes);
        Assert.True(acceptedFields.RequestedItemAmount > 0);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                var quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                Assert.Equal(acceptedFields.RequestedItemAmount, quest._requestedItemAmount);
                Assert.Equal(acceptedFields.RewardGold, quest.RewardGold);
            });
        }

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void MirrorQuestAccepted_RecomputesCurrentProgressAgainstTheCorrectedAmount_UsingTheOwnersRealParty()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var decoyPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Resolve<IControllerIdProvider>().SetControllerId("host-controller");

        VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest quest = null;
        Hero owner = null;
        int originalAmount = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out owner));

            using (new QuestSolutionStartAuthorityGuard())
            {
                Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
            }
            quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
            originalAmount = quest._requestedItemAmount;
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "host-controller");
        });

        // Vanilla's own QuestAcceptedConsequences reads MobileParty.MainParty, so this local seed step
        // needs it to genuinely be the owner's party - matching what's true on the owner's own client
        // at the moment they accept. The owner already has exactly enough for the amount their own
        // client originally computed - this is the "already has enough items" precondition from the
        // review.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Campaign.Current.MainParty = party;

            using (new AllowedThread())
            {
                // Assigning PartyBelongedTo switches Hero.CurrentSettlement to resolve through the
                // party instead of StayingInSettlement, so the party needs its own settlement too.
                party.CurrentSettlement = settlement;
                owner.PartyBelongedTo = party;
                party.ItemRoster.AddToCounts(quest._requestedItem, originalAmount);
                quest.QuestAcceptedConsequences();
            }
        });

        Server.Call(() =>
        {
            Assert.Equal(originalAmount, quest._playerAcceptedQuestLog.CurrentProgress);
            Assert.Equal(originalAmount, quest._requestedItemAmount);
            Assert.Equal(originalAmount, quest._playerAcceptedQuestLog.Range);
            Assert.True(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.Null(explanation);
        });

        // Reassign MainParty to an unrelated decoy party before mirroring, so the assertions below can
        // only pass if the recompute uses owner.PartyBelongedTo (the real, synced party) and not
        // MobileParty.MainParty (which is peer-local and would be wrong on any receiving peer other
        // than the accepter's own client).
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(decoyPartyId, out var decoyParty));
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = decoyParty;
            }
        });

        // A correction arrives with a larger amount than the owner's party actually has on hand -
        // CurrentProgress must be recomputed against the real roster, not left at the stale old cap.
        var correctedAmount = originalAmount + 5;
        Server.Call(() =>
        {
            VillageNeedsCraftingMaterialsQuestType.QuestSolutionAccept.Mirror(
                owner, new VillageNeedsCraftingMaterialsAcceptFields(correctedAmount, quest.RewardGold));

            Assert.Equal(correctedAmount, quest._requestedItemAmount);
            Assert.Equal(originalAmount, quest._playerAcceptedQuestLog.CurrentProgress);
            Assert.False(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.NotNull(explanation);
        });

        // Giving the owner's real party the rest of the corrected amount and re-mirroring the same
        // fields must bring CurrentProgress up to what the owner's party genuinely has now.
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            using (new AllowedThread())
            {
                party.ItemRoster.AddToCounts(quest._requestedItem, correctedAmount - originalAmount);
            }

            VillageNeedsCraftingMaterialsQuestType.QuestSolutionAccept.Mirror(
                owner, new VillageNeedsCraftingMaterialsAcceptFields(correctedAmount, quest.RewardGold));

            Assert.Equal(correctedAmount, quest._playerAcceptedQuestLog.CurrentProgress);
            Assert.True(quest.CompleteQuestClickableConditions(out var explanation));
            Assert.Null(explanation);
        });
    }

    [Fact]
    public void RequestQuestTypeAcceptQuest_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
            Assert.True(playerManager.AddPlayer(new Player("player-B", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        TestEnvironment.ConnectRegisteredPlayer(OtherClient, "player-B");
        OpenConversation(Client, fixture.HeroId, "player-A");

        var generation = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, generation));
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.False(owner.Issue.IsOngoingWithoutQuest);
            Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
        });

        OpenConversation(OtherClient, fixture.HeroId, "player-B");
        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, generation));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Assert.Single(OtherClient.InternalMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Empty(Client.InternalMessages.GetMessages<NetworkQuestTypeAcceptRejected>());

        foreach (var client in TestEnvironment.Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                Assert.True(client.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestQuestTypeAcceptQuest_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer, new RequestQuestTypeAcceptQuest(fixture.HeroId, 0));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeQuestAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });
    }

    [Fact]
    public void RequestQuestTypeAcceptAlternative_MirrorsTheCapturedVanillaStateToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 5);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Assert.Single(Client.InternalMessages.GetMessages<QuestTypeAlternativeAcceptTriggered>());

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.NotEqual(default, accepted.State.ReturnTime);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(owner.Issue.IsSolvingWithAlternative);
                Assert.Equal(accepted.State.ReturnTime, owner.Issue.AlternativeSolutionReturnTimeForTroops);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal("player-A", ownerControllerId);
            Assert.Equal(7, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
        });
    }

    [Fact]
    public void AlternativeSolutionCompletionRunner_CompleteOnServer_ActuallyFinalizesTheServersOwnIssue()
    {
        var fixture = SetupIssueOwner();

        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Village>(villageId, out var village));
            using (new AllowedThread())
            {
                settlement.SetSettlementComponent(village);
                village.Bound = settlement;
                village.Hearth = 650f;
                owner.Occupation = Occupation.RuralNotable;
            }
        });

        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 5);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsSolvingWithAlternative);
            var issue = (IssueBase)owner.Issue;

            using (new AllowedThread())
            {
                AlternativeSolutionCompletionRunner.CompleteOnServer(owner, issue);
            }

            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var ownerAfter));
            Assert.Null(ownerAfter.Issue);
        });
    }

    [Fact]
    public void IssueFinalized_OrganicUnguardedCall_DoesNotClearOwnershipOrConversationTrackingSinceNothingWasActuallyFinalized()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            Client.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
            Client.Resolve<IIssueConversationTracker>().Register(ownerId, "player-A", 0);

            owner.Issue.IssueFinalized();
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));

            Assert.True(Client.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var controllerId));
            Assert.Equal("player-A", controllerId);
            Assert.True(Client.Resolve<IIssueConversationTracker>().TryGetTrackedRequester(ownerId, "player-A", out _));

            Assert.NotNull(owner.Issue);
        });
    }

    [Fact]
    public void RequestQuestTypeAcceptAlternative_RejectedForTheGenuineClaimant_ClearsItsLocallyPopulatedRoster()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 5);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.Equal(0, owner.Issue.AlternativeSolutionSentTroops.TotalManCount);
        });
    }

    [Fact]
    public void RequestQuestTypeAcceptAlternative_FirstRequestWins_SecondIsRejectedAndOwnershipConvergesOnEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                party.CurrentSettlement = settlement;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
            Assert.True(playerManager.AddPlayer(new Player("player-B", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        TestEnvironment.ConnectRegisteredPlayer(OtherClient, "player-B");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");
        OpenConversation(Client, fixture.HeroId, "player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 5);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
        Assert.Equal(fixture.HeroId, accepted.OwnerId);
        Assert.Equal("player-A", accepted.OwnerControllerId);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());

        OpenConversation(OtherClient, fixture.HeroId, "player-B");
        var generation = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out generation));
            Server.Resolve<IMessageBroker>().Publish(OtherClient.NetPeer,
                new RequestQuestTypeAcceptAlternative(fixture.HeroId, generation, default));
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(owner.Issue.IsSolvingWithAlternative);
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
                Assert.Equal("player-A", ownerControllerId);
            });
        }
    }

    [Fact]
    public void RequestQuestTypeAcceptAlternative_FromUnregisteredRequester_IsRejectedWithoutMutatingTheIssue()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Server.Call(() =>
        {
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestQuestTypeAcceptAlternative(fixture.HeroId, 0, default));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAlternativeAccepted>());
        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestTypeAcceptRejected>());
        Assert.Equal(fixture.HeroId, rejected.OwnerId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsOngoingWithoutQuest);
            Assert.False(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out _));
        });
    }

    [Fact]
    public void RequestVillageIssueRemoved_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupIssueOwner();

        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(village);
                    village.Bound = settlement;
                    village.Hearth = 650f;
                }
            });
        }

        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                using (new QuestSolutionStartAuthorityGuard())
                {
                    Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
                }
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                if (instance == Server)
                {
                    Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
                }
            });
        }

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                var instanceQuest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                using (new AllowedThread())
                {
                    party.ItemRoster.AddToCounts(instanceQuest._requestedItem, instanceQuest._requestedItemAmount);
                }
            });
        }

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestSuccess, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestSuccess, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
                Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            });
        }
    }

    [Fact]
    public void RequestVillageIssueRemoved_QuestCancel_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                using (new QuestSolutionStartAuthorityGuard())
                {
                    Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
                }
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                if (instance == Server)
                {
                    Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
                }
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestCancel, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestCancel, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
                Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            });
        }
    }

    [Fact]
    public void RequestVillageIssueRemoved_QuestFail_ClaimingWarWasDeclared_FinalizesTheRealQuestAndBroadcastsRemovalToEveryPeer()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                using (new QuestSolutionStartAuthorityGuard())
                {
                    Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
                }
                Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                if (instance == Server)
                {
                    Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
                }
            });
        }

        float powerBefore = 0f;
        float relationBefore = 0f;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            powerBefore = owner.Power;
            relationBefore = owner.GetRelationWithPlayer();

            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestFail, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestFail, removed.Reason);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.Equal(powerBefore, owner.Power);
            Assert.Equal(relationBefore, owner.GetRelationWithPlayer());
        });

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
                Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            });
        }
    }

    [Fact]
    public void RequestVillageIssueRemoved_QuestFail_ClaimingTheDeliveryDeadlinePassed_AppliesTheVanillaTimeoutPenalties()
    {
        var fixture = SetupIssueOwner();

        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(village);
                    village.Bound = settlement;
                    village.Hearth = 650f;
                }
            });
        }

        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest serverQuest = null;
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                using (new QuestSolutionStartAuthorityGuard())
                {
                    Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
                }
                var quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                using (new AllowedThread())
                {
                    quest.ChangeQuestDueTime(CampaignTime.DaysFromNow(-1f));
                }
                if (instance == Server)
                {
                    Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
                    serverQuest = quest;
                }
            });
        }

        float powerBefore = 0f;
        float hearthBefore = 0f;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            powerBefore = owner.Power;
            hearthBefore = owner.CurrentSettlement.Village.Hearth;

            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.QuestFail, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(fixture.HeroId, removed.OwnerId);
        Assert.Equal(IssueFinalizeReason.QuestFail, removed.Reason);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.Equal(powerBefore - 10f, owner.Power);
            Assert.Equal(-5, serverQuest.RelationshipChangeWithQuestGiver);
            Assert.Equal(hearthBefore - 40f, owner.CurrentSettlement.Village.Hearth);
        });

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
                Assert.False(Campaign.Current.IssueManager.Issues.ContainsKey(owner));
            });
        }
    }

    [Fact]
    public void RealPlayerLocalSuccessClick_RewardIsGrantedAuthoritativelyOnServerNotJustOnTheClickingClient()
    {
        var fixture = SetupIssueOwner();

        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                using (new AllowedThread())
                {
                    settlement.SetSettlementComponent(village);
                    village.Bound = settlement;
                    village.Hearth = 650f;
                }
            });
        }

        CreateIssueOnServer(fixture.HeroId);
        ForcePromisedPaymentEverywhere(fixture.HeroId, 500);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                using (new QuestSolutionStartAuthorityGuard())
                {
                    Assert.True(Campaign.Current.IssueManager.StartIssueQuest(owner));
                }
                if (instance == Server)
                {
                    Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
                }
            });
        }

        ItemObject requestedItem = null;
        int requestedItemAmount = 0;
        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                var instanceQuest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);
                using (new AllowedThread())
                {
                    party.ItemRoster.AddToCounts(instanceQuest._requestedItem, instanceQuest._requestedItemAmount);
                }
                if (instance == Server)
                {
                    requestedItem = instanceQuest._requestedItem;
                    requestedItemAmount = instanceQuest._requestedItemAmount;
                }
            });
        }

        var goldBefore = 0;
        int honorXpBefore = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            goldBefore = owner.Gold;
            honorXpBefore = Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            var quest = Assert.IsType<VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssueQuest>(owner.Issue.IssueQuest);

            quest.Success();
        });

        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.Null(owner.Issue);
                Assert.Equal(0, party.ItemRoster.GetItemNumber(requestedItem));
            });
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.Equal(goldBefore + 500, owner.Gold);
            Assert.Equal(honorXpBefore + 30, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
        });
    }

    [Fact]
    public void RequestVillageIssueRemoved_WithNoQuestYet_FallsBackToBareIssueFinalizedWithoutOrphaning()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", "", "", "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
            Server.Resolve<IMessageBroker>().Publish(Client.NetPeer,
                new RequestIssueRemoved(fixture.HeroId, IssueFinalizeReason.IssueOnly, generation));
        });

        var removed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkIssueRemoved>());
        Assert.Equal(IssueFinalizeReason.IssueOnly, removed.Reason);

        foreach (var instance in AllInstances)
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.Null(owner.Issue);
            });
        }
    }

    [Fact]
    public void OnHourlyTick_OneDueOwnedIssue_SendsExactlyOneCompletionRequest()
    {
        var fixture = SetupIssueOwner();
        CreateIssueOnServer(fixture.HeroId);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player("player-A", fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, "player-A");
        Client.Resolve<IControllerIdProvider>().SetControllerId("player-A");

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Client.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, "player-A");

            using (new AllowedThread())
            {
                owner.Issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                owner.Issue.IsTriedToSolveBefore = true;
                owner.Issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.Now - CampaignTime.Days(1f);
            }

            new IssuesCampaignBehavior().RegisterEvents();
            new VillageNeedsCraftingMaterialsIssueBehavior().RegisterEvents();
            CampaignEvents.Instance.HourlyTick();
        });

        var requests = Client.NetworkSentMessages.GetMessages<RequestAlternativeSolutionCompletion>();
        Assert.Single(requests);
    }
}
