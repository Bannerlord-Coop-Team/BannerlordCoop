using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.TroopRosters.Data;
using GameInterface.Services.TroopRosters.Interfaces;
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
    public void ReturningToMainMenuInvalidatesOldTroopInquiryAndAllowsTheNextOne()
    {
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Game.Current.PlayerTroop = party.LeaderHero.CharacterObject;
            Campaign.Current.MainParty = party;
            Client.Resolve<IControllerIdProvider>().SetControllerId("returning-player");
            var troops = TroopRoster.CreateDummyTroopRoster();
            troops.AddToCounts(troop, 3);
            Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit("returning-player", troops);
        });
        var inquiries = new List<object>();
        var capture = InquiryCaptureHandler.MakeDelegate(inquiries.Add);
        InquiryCaptureHandler.OnShowInquiryEvent.AddEventHandler(null, capture);
        try
        {
            Client.Call(() => CheckIfTroopsCanReturnToMainPartyMethod.Invoke(Campaign.Current.IssueManager, null));
            var oldInquiry = Assert.Single(inquiries);
            Client.SimulateMessage(this, new MainMenuEntered());
            Client.Call(() => CheckIfTroopsCanReturnToMainPartyMethod.Invoke(Campaign.Current.IssueManager, null));
            Assert.Equal(2, inquiries.Count);
            Client.Call(() => InquiryCaptureHandler.InvokeAffirmativeAction(oldInquiry));
            Assert.Empty(Client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDrain>());
            Client.Call(() => InquiryCaptureHandler.InvokeAffirmativeAction(inquiries[1]));
            Assert.Single(Client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDrain>());
        }
        finally
        {
            InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, capture);
            if (inquiries.Count > 0)
                Client.Call(() => InquiryCaptureHandler.InvokeAffirmativeAction(inquiries.Last()));
        }
    }

    [Fact]
    public void ReturnAcknowledgementDoesNotEraseTheNextExpeditionsDeposit()
    {
        var troopId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        const string controller = "returning-player";
        TroopRosterData nextDeposit = default;
        Client.Call(() =>
        {
            Client.Resolve<IControllerIdProvider>().SetControllerId(controller);
            Assert.True(Client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var roster = TroopRoster.CreateDummyTroopRoster();
            roster.AddToCounts(troop, 3, woundedCount: 1, xpChange: 21);
            Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(controller, roster);
            nextDeposit = Client.Resolve<ITroopRosterInterface>().PackTroopRosterData(roster);
        });

        Client.SimulateMessage(Server.NetPeer, new NetworkAwaitingAlternativeSolutionTroopsDrained(), markGameThread: false);
        Client.SimulateMessage(Server.NetPeer, new NetworkAwaitingAlternativeSolutionTroopsDepositConfirmed("next-giver", nextDeposit), markGameThread: false);
        Client.PumpGameThread();

        Client.Call(() =>
        {
            Assert.True(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controller, out var remaining));
            Assert.Equal(3, remaining.TotalManCount);
            Assert.Equal(1, remaining.TotalWounded);
            Assert.Equal(21, remaining.GetElementXp(0));
        });
    }

    [Fact]
    public void ClientOwnedAlternativeSolutionCompletion_WhileOwnerUnreachable_TroopsSurviveASaveReloadAndReturnOnReconnect()
    {
        var controllerId = "player-A-" + Guid.NewGuid();
        int depositedManCount = 0;

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

        var clientPartyId = partyId;
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(clientPartyId, out var clientParty));
            using (new AllowedThread())
            {
                clientParty.IsActive = false;
                Campaign.Current.MainParty = clientParty;
            }
        });

        object capturedInquiry = null;
        var onShowInquiry = InquiryCaptureHandler.MakeDelegate(data => capturedInquiry = data);
        InquiryCaptureHandler.OnShowInquiryEvent.AddEventHandler(null, onShowInquiry);
        try
        {
            Server.Call(() =>
            {
                Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
                Assert.Equal(Hero.CharacterStates.Disabled, companion.HeroState);
            });
            Client.Call(() =>
            {
                if (Hero.MainHero.IsPrisoner) Hero.MainHero.ChangeState(Hero.CharacterStates.Active);

                var result = CheckIfTroopsCanReturnToMainPartyMethod.Invoke(Campaign.Current.IssueManager, null);
                Assert.Null(result);
            });

            Assert.NotNull(capturedInquiry);

            Client.Call(() => InquiryCaptureHandler.InvokeAffirmativeAction(capturedInquiry));
            TestEnvironment.FlushCoalescer();
        }
        finally
        {
            InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, onShowInquiry);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(party.MemberRoster.Contains(companion.CharacterObject));
        });

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.Equal(Hero.CharacterStates.Active, companion.HeroState);

            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(clientPartyId, out var clientParty));
            Assert.True(clientParty.MemberRoster.Contains(companion.CharacterObject));

            Assert.False(Client.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });

        Assert.Single(Client.NetworkSentMessages.GetMessages<RequestAwaitingAlternativeSolutionTroopsDrain>());
        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(controllerId, out _));
        });
    }

    [Fact]
    public void ClientOwnedAlternativeSolutionCompletion_ServerBroadcastsConfirmedDeposit_ClientsOwnHourlyTickDrainsItIntoMainParty()
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

        var clientPartyId = partyId;
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(clientPartyId, out var clientParty));
            using (new AllowedThread())
            {
                clientParty.IsActive = false;
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
            TestEnvironment.FlushCoalescer();
        }
        finally
        {
            InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, onShowInquiry);
        }

        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(clientPartyId, out var clientParty));
            Assert.True(clientParty.MemberRoster.Contains(companion.CharacterObject));

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

        var clientPartyId = partyId;
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
    public void OnHourlyTick_ServerWithoutMainHeroKeepsAwaitingTroopsForTheirPlayer()
    {
        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        string serverControllerId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Game.Current.PlayerTroop = null;
            Campaign.Current.MainParty = null;
            serverControllerId = "disconnected-player-" + Guid.NewGuid();
            Server.Resolve<IControllerIdProvider>().SetControllerId(serverControllerId);
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, serverControllerId);

            var roster = TroopRoster.CreateDummyTroopRoster();
            using (new AllowedThread())
            {
                roster.AddToCounts(companion.CharacterObject, 1);
            }
            Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit(serverControllerId, roster);

        });

        object capturedInquiry = null;
        var onShowInquiry = InquiryCaptureHandler.MakeDelegate(data => capturedInquiry = data);
        InquiryCaptureHandler.OnShowInquiryEvent.AddEventHandler(null, onShowInquiry);
        try
        {
            Server.Call(() =>
            {
                new IssuesCampaignBehavior().RegisterEvents();
                CampaignEvents.Instance.HourlyTick();
            });

            Assert.Null(capturedInquiry);
        }
        finally
        {
            InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, onShowInquiry);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.Equal(Hero.CharacterStates.Disabled, companion.HeroState);
            Assert.True(Server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet(serverControllerId, out var awaiting));
            Assert.True(awaiting.Contains(companion.CharacterObject));
        });
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
