using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
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
    public void ClientOwnedAlternativeSolutionCompletion_WhileOwnerUnreachable_TroopsSurviveASaveReloadAndReturnOnReconnect()
    {
        var controllerId = "player-A-" + Guid.NewGuid();
        int depositedManCount = 0;

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
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
            using (new AllowedThread())
            {
                owner.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
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

        var clientPartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Client.Call(() =>
        {
            Assert.True(Client.ObjectManager.TryGetObject<MobileParty>(clientPartyId, out var clientParty));
            clientParty.IsActive = false;
            Campaign.Current.MainParty = clientParty;
        });

        object capturedInquiry = null;
        var onShowInquiry = InquiryCaptureHandler.MakeDelegate(data => capturedInquiry = data);
        InquiryCaptureHandler.OnShowInquiryEvent.AddEventHandler(null, onShowInquiry);
        try
        {
            Client.Call(() =>
            {
                if (Hero.MainHero.IsPrisoner) Hero.MainHero.ChangeState(Hero.CharacterStates.Active);

                var result = CheckIfTroopsCanReturnToMainPartyMethod.Invoke(Campaign.Current.IssueManager, null);
                Assert.Null(result);
            });

            Assert.NotNull(capturedInquiry);

            Client.Call(() => InquiryCaptureHandler.InvokeAffirmativeAction(capturedInquiry));
        }
        finally
        {
            InquiryCaptureHandler.OnShowInquiryEvent.RemoveEventHandler(null, onShowInquiry);
        }

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
    public void RequestAwaitingAlternativeSolutionTroopsDeposit_ClaimedRosterExceedsSentTroops_ClampedToWhatWasActuallySent()
    {
        var controllerId = "player-A-" + Guid.NewGuid();

        var fixture = SetupVillageOwner();
        CreateIssueOnBothPeers(fixture);

        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            var playerManager = Server.Resolve<IPlayerManager>();
            var player = new Player(controllerId, fixture.HeroId, partyId, "", "");
            Assert.True(playerManager.AddPlayer(player));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);

            using (new AllowedThread())
            {
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
            var fabricatedRoster = TroopRoster.CreateDummyTroopRoster();
            fabricatedRoster.AddToCounts(companion.CharacterObject, 999999);
            var fabricatedPacked = troopRosterInterface.PackTroopRosterData(fabricatedRoster);

            var network = Client.Resolve<Common.Network.INetwork>();
            network.SendAll(new RequestAwaitingAlternativeSolutionTroopsDeposit(ownerId, fabricatedPacked));
        });

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
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.HeroId, out var owner));
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(fixture.CompanionHeroId, out var companion));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));

            var playerManager = Server.Resolve<IPlayerManager>();
            var player = new Player(controllerId, fixture.HeroId, partyId, "", "");
            Assert.True(playerManager.AddPlayer(player));
            Server.Resolve<IIssueOwnershipRegistry>().SetOwner(owner, controllerId);

            using (new AllowedThread())
            {
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
