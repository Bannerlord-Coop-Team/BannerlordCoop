using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Client.Services.Heroes.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class AwaitingAlternativeSolutionTroopsTests : IDisposable
{
    private static readonly MethodInfo CheckIfTroopsCanReturnToMainPartyMethod =
        AccessTools.Method(typeof(IssueManager), "CheckIfTroopsCanReturnToMainParty");

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private EnvironmentInstance Client => TestEnvironment.Clients.First();

    public AwaitingAlternativeSolutionTroopsTests(ITestOutputHelper output)
    {
        TestQuestTypeFixture.EnsureVillageNeedsToolsRegistered();
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    private record VillageFixture(string HeroId, string VillageId, string SettlementId, string ItemId, string CompanionHeroId);

    private VillageFixture SetupVillageOwner()
    {
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var villageId = TestEnvironment.CreateRegisteredObject<Village>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        var itemId = TestEnvironment.CreateRegisteredObject<ItemObject>();
        var companionHeroId = TestEnvironment.CreateRegisteredObject<Hero>();

        foreach (var instance in new[] { Server, Client })
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionHeroId, out var companion));

                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();

                    settlement.SetSettlementComponent(village);
                    village.Bound = settlement;
                    village.Hearth = 650f;
                    hero.StayingInSettlement = settlement;
                    hero.Occupation = Occupation.RuralNotable;
                    AccessTools.Property(typeof(ItemObject), nameof(ItemObject.Value)).SetValue(item, 40);
                    companion.CharacterObject.HeroObject = companion;
                    companion.ChangeState(Hero.CharacterStates.Disabled);
                }
            });
        }

        return new VillageFixture(heroId, villageId, settlementId, itemId, companionHeroId);
    }

    private void CreateIssueOnBothPeers(VillageFixture fixture)
    {
        var generation = 0;
        foreach (var instance in new[] { Server, Client })
        {
            var isServer = instance == Server;
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(fixture.ItemId, out var requestedItem));

                if (owner.Issue == null)
                {
                    var pid = new PotentialIssueData(
                        (in PotentialIssueData _, Hero h) => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, requestedItem),
                        typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
                        IssueBase.IssueFrequency.VeryCommon);

                    using (new AllowedThread())
                    {
                        Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in pid, owner));
                    }
                }

                var generationRegistry = instance.Resolve<IIssueGenerationRegistry>();
                if (isServer) generation = generationRegistry.Bump(owner);
                else generationRegistry.SetGeneration(owner, generation);
            });
        }
    }

    private sealed class TestDataStore : IDataStore
    {
        private readonly Dictionary<string, object> records;

        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;

        internal TestDataStore(bool isSaving, Dictionary<string, object> records)
        {
            IsSaving = isSaving;
            this.records = records;
        }

        public bool SyncData<T>(string key, ref T data)
        {
            if (IsSaving)
            {
                records[key] = data;
                return true;
            }

            if (!records.TryGetValue(key, out var value)) return false;
            data = (T)value;
            return true;
        }
    }

    [Fact]
    public void ClientOwnedAlternativeSolutionCompletion_WhileOwnerUnreachable_TroopsSurviveASaveReloadAndReturnOnReconnect()
    {
        var controllerId = "player-A-" + Guid.NewGuid();
        int depositedManCount = 0;
        string completedRevision = null;

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                eligibleTroop.Level = 20;
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                party.CurrentSettlement = settlement;
                owner.Gold = 1000000;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var clientOwnerId));
            MessageBroker.Instance.Publish(owner, new IssueConversationOpenedLocally(owner, controllerId));
        });

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestIssueConversationOpened>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetId(owner, out var ownerId));

            Assert.NotNull(owner.Issue);
            Assert.True(owner.Issue.IsOngoingWithoutQuest, $"IsOngoingWithoutQuest={owner.Issue.IsOngoingWithoutQuest}");
            Assert.True(owner.Issue.IssueStayAliveConditions(), "IssueStayAliveConditions false");
            var mirrorDescriptor = QuestTypeRegistry.Get(owner.Issue);
            Assert.True(
                mirrorDescriptor?.SupportsQuestSolutionAccept == true || mirrorDescriptor?.SupportsAlternativeAccept == true,
                "not mirror eligible");

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.TryGetPlayer(controllerId, out var player), "player not found by controllerId");

            var conversationTracker = Server.Resolve<IIssueConversationTracker>();
            Assert.True(conversationTracker.TryGetTrackedRequester(ownerId, controllerId, out _),
                $"no tracked requester for ownerId={ownerId}, controllerId={controllerId}");
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(owner, out var ownerControllerId));
            Assert.Equal(controllerId, ownerControllerId);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));

            var previousState = Hero.MainHero.HeroState;
            Hero.MainHero.ChangeState(Hero.CharacterStates.Prisoner);
            Assert.True(Hero.MainHero.IsPrisoner);
            try
            {
                var exception = Record.Exception(() => Campaign.Current.IssueManager.TryToMakeTroopsReturn(owner.Issue));
                Assert.Null(exception);
            }
            finally
            {
                Hero.MainHero.ChangeState(previousState);
            }

            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var deposited));
            Assert.True(deposited.TotalManCount >= 1);
            Assert.True(deposited.TotalHeroes >= 1);
        });

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDeposit>());
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var serverDeposited));
            Assert.True(serverDeposited.TotalManCount >= 1);
            depositedManCount = serverDeposited.TotalManCount;
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGetRevision(controllerId, out completedRevision));
        });
        Client.Call(() =>
        {
            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var confirmed));
            Assert.Equal(depositedManCount, confirmed.TotalManCount);
            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGetRevision(controllerId, out var revision));
            Assert.Equal(completedRevision, revision);
        });

        Server.Call(() =>
        {
            var troopsRegistry = Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
            var behavior = new IssuesCampaignBehavior();
            var records = new Dictionary<string, object>();

            behavior.SyncData(new TestDataStore(isSaving: true, records));

            troopsRegistry.ClearAll();

            behavior.SyncData(new TestDataStore(isSaving: false, records));

            Assert.True(troopsRegistry.TryGet(controllerId, out var restored));
            Assert.Equal(depositedManCount, restored.TotalManCount);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Campaign.Current.MainParty = clientParty;
        });

        object capturedInquiry = null;
        var onShowInquiry = InquiryCaptureHandler.MakeDelegate(data => capturedInquiry = data);
        InquiryCaptureHandler.OnShowInquiryEvent.AddEventHandler(null, onShowInquiry);
        var serverActivationMessagesBeforeDrain = Server.NetworkSentMessages.GetMessages<NetworkHeroStateChanged>()
            .Count(message => message.HeroId == fixture.CompanionHeroId && message.HeroState == (int)Hero.CharacterStates.Active);
        var clientActivationMessagesBeforeDrain = Client.InternalMessages.GetMessages<ChangeHeroState>()
            .Count(message => message.HeroId == fixture.CompanionHeroId && message.HeroState == (int)Hero.CharacterStates.Active);
        try
        {
            Client.Call(() =>
            {
                if (Hero.MainHero.IsPrisoner) Hero.MainHero.ChangeState(Hero.CharacterStates.Active);

                var result = CheckIfTroopsCanReturnToMainPartyMethod.Invoke(Campaign.Current.IssueManager, null);
                Assert.Null(result);
            });

            Assert.NotNull(capturedInquiry);

            Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.PollerThread;
            Client.Call(() => InquiryCaptureHandler.InvokeAffirmativeAction(capturedInquiry));
            Server.PumpGameThread();
        }
        finally
        {
            InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, onShowInquiry);
        }

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDrain>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(6, party.MemberRoster.GetTroopCount(eligibleTroop));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed>());
        Assert.True(Server.NetworkSentMessages.GetMessages<NetworkHeroStateChanged>()
            .Count(message => message.HeroId == fixture.CompanionHeroId && message.HeroState == (int)Hero.CharacterStates.Active)
            > serverActivationMessagesBeforeDrain, "Server did not broadcast companion activation");
        Assert.True(Client.InternalMessages.GetMessages<ChangeHeroState>()
            .Count(message => message.HeroId == fixture.CompanionHeroId && message.HeroState == (int)Hero.CharacterStates.Active)
            > clientActivationMessagesBeforeDrain, "Client did not receive companion activation");
        Client.PumpGameThread();
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
        });
        TestEnvironment.FlushCoalescer();
        Client.PumpGameThread();
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);

            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.Equal(1, clientParty.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(6, clientParty.MemberRoster.GetTroopCount(eligibleTroop));

            Assert.False(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });
        Server.Resolve<TestNetworkRouter>().ReceiveContext = TestNetworkReceiveContext.GameThread;

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            var stalePending = TroopRoster.CreateDummyTroopRoster();
            stalePending.AddToCounts(companion.CharacterObject, 1);
            stalePending.AddToCounts(eligibleTroop, 6);
            Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(controllerId, stalePending);
            MessageBroker.Instance.Publish(null, new AwaitingAlternativeSolutionTroopsDrainedLocally(controllerId, stalePending));
        });
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(6, party.MemberRoster.GetTroopCount(eligibleTroop));
        });
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed>().Count());
        Client.Call(() => Assert.False(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _)));

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            var laterDeposit = TroopRoster.CreateDummyTroopRoster();
            laterDeposit.AddToCounts(companion.CharacterObject, 1);
            laterDeposit.AddToCounts(eligibleTroop, 6);
            Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(controllerId, laterDeposit);
        });
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            var oldRequest = TroopRoster.CreateDummyTroopRoster();
            oldRequest.AddToCounts(companion.CharacterObject, 1);
            oldRequest.AddToCounts(eligibleTroop, 6);
            Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Restore(controllerId, oldRequest, completedRevision);
            MessageBroker.Instance.Publish(null, new AwaitingAlternativeSolutionTroopsDrainedLocally(controllerId, oldRequest));
        });
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var laterDeposit));
            Assert.Equal(7, laterDeposit.TotalManCount);
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGetRevision(controllerId, out var revision));
            Assert.NotEqual(completedRevision, revision);
        });
        Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed>().Count());
        Client.Call(() =>
        {
            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var laterDeposit));
            Assert.Equal(7, laterDeposit.TotalManCount);
            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGetRevision(controllerId, out var revision));
            Assert.NotEqual(completedRevision, revision);
        });
    }

    [Fact]
    public void ClientOwnedAlternativeSolutionCompletion_ServerBroadcastsConfirmedDeposit_HourlyTickRequestsAuthoritativeReturn()
    {
        var controllerId = "player-A-" + Guid.NewGuid();

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                eligibleTroop.Level = 20;
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                party.CurrentSettlement = settlement;
                owner.Gold = 1000000;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            MessageBroker.Instance.Publish(owner, new IssueConversationOpenedLocally(owner, controllerId));
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(owner.Issue.IsSolvingWithAlternative);
            var issue = (IssueBase)owner.Issue;

            using (new AllowedThread())
            {
                AlternativeSolutionCompletionRunner.CompleteOnServer(owner, issue);
            }
        });

        var confirmed = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>());
        Assert.Equal(fixture.HeroId, confirmed.OwnerId);

        Client.Call(() =>
        {
            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var deposited));
            Assert.True(deposited.TotalManCount >= 1);
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            using (new AllowedThread())
            {
                Campaign.Current.MainParty = clientParty;
            }
        });

        object capturedInquiry = null;
        var onShowInquiry = InquiryCaptureHandler.MakeDelegate(data => capturedInquiry = data);
        InquiryCaptureHandler.OnShowInquiryEvent.AddEventHandler(null, onShowInquiry);
        try
        {
            Client.Call(() =>
            {
                new IssuesCampaignBehavior().RegisterEvents();
                CampaignEvents.Instance.HourlyTick();
            });

            Assert.NotNull(capturedInquiry);

            Client.Call(() => InquiryCaptureHandler.InvokeAffirmativeAction(capturedInquiry));
        }
        finally
        {
            InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, onShowInquiry);
        }

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDrain>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(6, party.MemberRoster.GetTroopCount(eligibleTroop));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAwaitingAlternativeSolutionTroopsDrainConfirmed>());
        TestEnvironment.FlushCoalescer();
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var clientParty));
            Assert.Equal(1, clientParty.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(6, clientParty.MemberRoster.GetTroopCount(eligibleTroop));

            Assert.False(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });
    }

    [Fact]
    public void TryToMakeTroopsReturn_OwnerLocallyPresentAndAvailable_StillGoesThroughServerValidatedDepositNotAnInstantLocalAdd()
    {
        var controllerId = "player-A-" + Guid.NewGuid();

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(fixture.SettlementId, out var settlement));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                eligibleTroop.Level = 20;
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                party.CurrentSettlement = settlement;
                owner.Gold = 1000000;
            }

            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            MessageBroker.Instance.Publish(owner, new IssueConversationOpenedLocally(owner, controllerId));
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(eligibleTroop, 6);
            }
            owner.Issue.StartIssueWithAlternativeSolution();
        });

        var clientPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(clientPartyId, out var clientParty));
            using (new AllowedThread())
            {
                clientParty.IsActive = false;
                Campaign.Current.MainParty = clientParty;
            }

            Campaign.Current.IssueManager.TryToMakeTroopsReturn(owner.Issue);

            Assert.False(clientParty.MemberRoster.Contains(companion.CharacterObject));
            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var deposited));
            Assert.True(deposited.TotalManCount >= 1);
        });

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDeposit>());
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var serverDeposited));
            Assert.True(serverDeposited.TotalManCount >= 1);
        });
    }

    [Fact]
    public void RequestAwaitingAlternativeSolutionTroopsDeposit_RejectedByServer_RollsBackTheClientsSpeculativeLocalDeposit()
    {
        var controllerId = "player-A-" + Guid.NewGuid();

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));

            var troops = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                troops.AddToCounts(companion.CharacterObject, 1);
            }

            Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(controllerId, troops);
            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));

            MessageBroker.Instance.Publish(owner, new AwaitingAlternativeSolutionTroopsDepositedLocally(owner, controllerId, troops));
        });

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDeposit>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>());

        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });
        Client.Call(() =>
        {
            Assert.False(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });

        var pendingTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        string confirmedRevision = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(pendingTroopId, out var pendingTroop));
            var confirmedTroops = TroopRoster.CreateDummyTroopRoster();
            confirmedTroops.AddToCounts(pendingTroop, 2);
            var registry = Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
            registry.Deposit(controllerId, confirmedTroops);
            Assert.True(registry.TryGetRevision(controllerId, out confirmedRevision));
        });
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(pendingTroopId, out var pendingTroop));
            var confirmedTroops = TroopRoster.CreateDummyTroopRoster();
            confirmedTroops.AddToCounts(pendingTroop, 2);
            var speculativeTroops = TroopRoster.CreateDummyTroopRoster();
            speculativeTroops.AddToCounts(pendingTroop, 1);
            var registry = Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
            registry.Restore(controllerId, confirmedTroops, confirmedRevision);
            registry.Deposit(controllerId, speculativeTroops);
            MessageBroker.Instance.Publish(owner,
                new AwaitingAlternativeSolutionTroopsDepositedLocally(owner, controllerId, speculativeTroops));
        });
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var pending));
            Assert.Equal(2, pending.TotalManCount);
        });
        Client.Call(() =>
        {
            var registry = Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
            Assert.True(registry.TryGet(controllerId, out var pending));
            Assert.Equal(2, pending.TotalManCount);
            Assert.True(registry.TryGetRevision(controllerId, out var revision));
            Assert.Equal(confirmedRevision, revision);
        });
    }

    [Fact]
    public void TryToMakeTroopsReturn_HeadlessServer_NoCrash_TroopsDeposited()
    {
        var controllerId = "player-A-" + Guid.NewGuid();

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);

            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }

            var previousPlayerTroop = Game.Current.PlayerTroop;
            Game.Current.PlayerTroop = null;
            try
            {
                Assert.Null(Game.Current?.PlayerTroop);

                var exception = Record.Exception(() => Campaign.Current.IssueManager.TryToMakeTroopsReturn(owner.Issue));

                Assert.Null(exception);
            }
            finally
            {
                Game.Current.PlayerTroop = previousPlayerTroop;
            }

            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var deposited));
            Assert.Equal(1, deposited.TotalManCount);
        });
    }

    [Fact]
    public void OnHourlyTick_DedicatedServerLeavesAwaitingTroopsForTheOwner()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var controllerId = "player-A-" + Guid.NewGuid();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.Resolve<IPlayerManager>().AddPlayer(
                new Player(controllerId, fixture.HeroId, partyId, "", "")));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);

            var roster = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                roster.AddToCounts(companion.CharacterObject, 1);
            }
            Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(controllerId, roster);
        });
        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        object capturedInquiry = null;
        var onShowInquiry = InquiryCaptureHandler.MakeDelegate(data => capturedInquiry = data);
        InquiryCaptureHandler.OnShowInquiryEvent.AddEventHandler(null, onShowInquiry);
        try
        {
            Server.Call(() =>
            {
                var previousPlayerTroop = Game.Current.PlayerTroop;
                var previousMainParty = Campaign.Current.MainParty;
                Game.Current.PlayerTroop = null;
                Campaign.Current.MainParty = null;
                try
                {
                    Assert.Null(Game.Current.PlayerTroop);
                    Assert.Null(MobileParty.MainParty);
                    new IssuesCampaignBehavior().RegisterEvents();
                    CampaignEvents.Instance.HourlyTick();
                    Assert.Null(capturedInquiry);
                    Assert.False(IssueManagerAlternativeSolutionTroopsPatches.ReturnAwaitingTroops());
                    Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
                    Assert.Equal(Hero.CharacterStates.Disabled, companion.HeroState);
                    Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var pending));
                    Assert.Equal(1, pending.TotalManCount);
                }
                finally
                {
                    Campaign.Current.MainParty = previousMainParty;
                    Game.Current.PlayerTroop = previousPlayerTroop;
                }
            });
        }
        finally
        {
            InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, onShowInquiry);
        }

    }

    [Fact]
    public void RequestAwaitingAlternativeSolutionTroopsDeposit_ClaimedRosterExceedsSentTroops_ClampedToWhatWasActuallySent()
    {
        var controllerId = "player-A-" + Guid.NewGuid();

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));

            var playerManager = Server.Resolve<IPlayerManager>();
            var player = new Player(controllerId, fixture.HeroId, partyId, "", "");
            Assert.True(playerManager.AddPlayer(player));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);

            using (new AllowedThread())
            {
                eligibleTroop.Level = 20;
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                owner.Gold = 1000000;
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }
            AlternativeSolutionStartRunner.StartOnServer(owner, player);
            Assert.True(owner.Issue.IsSolvingWithAlternative);
        });

        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetHandle(companion.CharacterObject, out var companionCharacterId));

            var fabricatedPacked = new TroopRosterData(new[]
            {
                new TroopRosterElementData(companionCharacterId, 999999, 0, 0),
            });

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, fabricatedPacked));
        });

        Assert.Empty(Server.NetworkSentMessages.OfType<NetworkAwaitingAlternativeSolutionTroopsDepositRejected>());
        Assert.Single(Server.NetworkSentMessages.OfType<NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed>());

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var deposited));
            Assert.Equal(1, deposited.TotalManCount);
        });
    }

    [Fact]
    public void RequestAwaitingAlternativeSolutionTroopsDeposit_ReplayedForSameGeneration_NotAccumulated()
    {
        var controllerId = "player-A-" + Guid.NewGuid();

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var eligibleTroopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(eligibleTroopId, out var eligibleTroop));

            var playerManager = Server.Resolve<IPlayerManager>();
            var player = new Player(controllerId, fixture.HeroId, partyId, "", "");
            Assert.True(playerManager.AddPlayer(player));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);

            using (new AllowedThread())
            {
                eligibleTroop.Level = 20;
                party.MemberRoster.AddToCounts(eligibleTroop, 6);
                owner.Gold = 1000000;
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
            }
            AlternativeSolutionStartRunner.StartOnServer(owner, player);
            Assert.True(owner.Issue.IsSolvingWithAlternative);
        });

        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));

            var troopRosterInterface = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>();
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(companion.CharacterObject, 1);
            var packed = troopRosterInterface.PackTroopRosterData(roster);

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, packed));
            network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, packed));
        });

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out var deposited));
            Assert.Equal(1, deposited.TotalManCount);
        });
    }

    [Fact]
    public void RequestAwaitingAlternativeSolutionTroopsDeposit_RequesterNotRecordedOwner_Rejected()
    {
        var controllerId = "player-A-" + Guid.NewGuid();

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var playerManager = Server.Resolve<IPlayerManager>();
            Assert.True(playerManager.AddPlayer(new Player(controllerId, fixture.HeroId, partyId, "", "")));
        });

        TestEnvironment.ConnectRegisteredPlayer(Client, controllerId);
        Client.Resolve<IControllerIdProvider>().SetControllerId(controllerId);

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Client.ObjectManager.TryGetId(owner, out var ownerId));
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));

            var troopRosterInterface = Client.Resolve<GameInterface.Services.TroopRosters.Interfaces.ITroopRosterInterface>();
            var fabricatedRoster = TroopRoster.CreateDummyTroopRoster();
            fabricatedRoster.AddToCounts(companion.CharacterObject, 5);
            var fabricatedPacked = troopRosterInterface.PackTroopRosterData(fabricatedRoster);

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, fabricatedPacked));
        });

        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });
    }

    private static class InquiryCaptureHandler
    {
        private static readonly Type InformationManagerType =
            Type.GetType("TaleWorlds.Library.InformationManager, TaleWorlds.Library");
        private static readonly Type InquiryDataType =
            Type.GetType("TaleWorlds.Library.InquiryData, TaleWorlds.Library");

        public static readonly EventInfo OnShowInquiryEvent =
            InformationManagerType.GetEvent("OnShowInquiry", BindingFlags.Public | BindingFlags.Static);

        private static readonly FieldInfo AffirmativeActionField =
            InquiryDataType.GetField("AffirmativeAction", BindingFlags.Public | BindingFlags.Instance);

        public static Delegate MakeDelegate(Action<object> callback)
        {
            Action<object, bool, bool> handler = (data, pauseGameActiveState, prioritize) => callback(data);
            return Delegate.CreateDelegate(OnShowInquiryEvent.EventHandlerType, handler.Target, handler.Method);
        }

        public static void InvokeAffirmativeAction(object inquiryData)
        {
            var action = (Action)AffirmativeActionField.GetValue(inquiryData);
            action();
        }
    }
}
