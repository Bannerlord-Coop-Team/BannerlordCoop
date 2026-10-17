using Common.Util;
using E2E.Tests.Environment;
using Common.Messaging;
using Common.Network.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface;
using GameInterface.Services.Heroes.Patches;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Framework.AcceptCoordination;
using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.RevenueFarming;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.ObjectSystem;
using Xunit.Abstractions;
using Issue = TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior.RevenueFarmingIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest;

namespace E2E.Tests.Services.Issues;

public sealed class RevenueFarmingTests : IDisposable
{
    private readonly E2ETestEnvironment environment;
    private readonly QuestScenario scenario;
    private readonly string townId;
    private readonly string[] villageIds;
    private readonly string notableId;
    private readonly Player owner;
    private readonly Player other;
    private readonly MethodCallRecorder menus;
    private readonly MethodCallRecorder hourly;
    private readonly Harmony random = new("RevenueFarmingTests.Random");
    private static float? incidentDraw;
    private EnvironmentInstance Server => environment.Server;
    private EnvironmentInstance Client => scenario.ClientA;
    private EnvironmentInstance Observer => scenario.ClientB;
    private EnvironmentInstance[] Instances => new[] { Server, Client, Observer };

    public RevenueFarmingTests(ITestOutputHelper output)
    {
        environment = new E2ETestEnvironment(output);
        townId = environment.CreateRegisteredObject<Settlement>();
        var townComponent = environment.CreateRegisteredObject<Town>();
        villageIds = new[] { environment.CreateRegisteredObject<Settlement>(), environment.CreateRegisteredObject<Settlement>() };
        var components = new[] { environment.CreateRegisteredObject<Village>(), environment.CreateRegisteredObject<Village>() };
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        notableId = environment.CreateRegisteredObject<Hero>();
        var villageTypeId = environment.CreateRegisteredObject<VillageType>();
        foreach (var instance in environment.Clients.Prepend(Server)) instance.Call(() =>
        {
            using (new AllowedThread())
            {
                var town = Object<Settlement>(instance, townId);
                town.SetSettlementComponent(Object<Town>(instance, townComponent));
                var villageType = Object<VillageType>(instance, villageTypeId);
                villageType._productions = new MBList<(ItemObject, float)> { (DefaultItems.Grain, 1f) };
                for (var i = 0; i < villageIds.Length; i++)
                {
                    var village = Object<Village>(instance, components[i]);
                    Object<Settlement>(instance, villageIds[i]).SetSettlementComponent(village);
                    village.Id = new MBGUID((uint)i + 1);
                    village.Bound = town;
                    village.Hearth = 200;
                    village.VillageType = villageType;
                }
                var notable = Object<Hero>(instance, notableId);
                notable.Clan = null;
                notable.Occupation = Occupation.Headman;
                notable.StayingInSettlement = Object<Settlement>(instance, villageIds[0]);
                Object<Settlement>(instance, villageIds[0])._notablesCache = new MBList<Hero> { notable };
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                var behavior = new RevenueFarmingIssueBehavior();
                Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { behavior }));
                behavior.AddVillageEvents(new CampaignGameStarter(Campaign.Current.GameMenuManager, Campaign.Current.ConversationManager));
            }
        });
        scenario = new QuestScenario(environment, giver =>
        {
            Assert.True(ContainerProvider.TryResolve<IObjectManager>(out var objects));
            Assert.True(objects.TryGetObject<Settlement>(townId, out var town));
            giver.Clan.SetLeader(giver);
            giver.Occupation = Occupation.Lord;
            town.Town.OwnerClan = giver.Clan;
            giver.StayingInSettlement = town;
            return new Issue(giver, town);
        });
        owner = Player(QuestScenario.ControllerA);
        other = Player(QuestScenario.ControllerB);
        foreach (var instance in Instances) instance.Call(() =>
        {
            using (new AllowedThread())
            {
                foreach (var player in new[] { owner, other })
                {
                    var hero = Object<Hero>(instance, player.HeroId);
                    var party = Object<MobileParty>(instance, player.MobilePartyId);
                    hero.PartyBelongedTo = party;
                    hero.Clan.SetLeader(hero);
                    hero.Gold = 2000;
                    party.ItemRoster.Clear();
                    party.MemberRoster.Clear();
                    party.MemberRoster.AddToCounts(Object<CharacterObject>(instance, troopId), 40);
                    party.CurrentSettlement = Object<Settlement>(instance, townId);
                }
                if (instance != Server)
                {
                    var player = instance == Client ? owner : other;
                    var hero = Object<Hero>(instance, player.HeroId);
                    Game.Current.PlayerTroop = hero.CharacterObject;
                    Campaign.Current.MainParty = hero.PartyBelongedTo;
                    Campaign.Current.PlayerDefaultFaction = hero.Clan;
                    var states = Game.Current.GameStateManager;
                    var map = states.CreateState<MapState>();
                    states._gameStates.Add(map);
                    map._menuContext = Game.Current.ObjectManager.CreateObject<MenuContext>();
                    map._menuContext.GameMenu = ObjectHelper.SkipConstructor<GameMenu>();
                    map._menuContext.GameMenu.StringId = "village";
                }
            }
        });
        menus = new MethodCallRecorder(Priority.VeryHigh,
            AccessTools.Method(typeof(GameMenu), nameof(GameMenu.SwitchToMenu)),
            AccessTools.Method(typeof(PlayerEncounter), nameof(PlayerEncounter.Finish)),
            AccessTools.Method(typeof(InformationManager), nameof(InformationManager.ShowInquiry)));
        hourly = new MethodCallRecorder(Priority.Low,
            AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.HourlyTick)));
        random.Patch(AccessTools.PropertyGetter(typeof(MBRandom), nameof(MBRandom.RandomFloat)),
            prefix: new HarmonyMethod(typeof(RevenueFarmingTests), nameof(RandomPrefix)));
    }

    public void Dispose()
    {
        hourly.Dispose();
        incidentDraw = null;
        random.UnpatchAll(random.Id);
        menus.Dispose();
        environment.Dispose();
    }

    [Fact]
    public void AcceptUsesFrameworkOwnershipAndStartsOnlyThePlayersQuest()
    {
        Accept();
        Assert.Equal(QuestScenario.ControllerA, scenario.OwnerOnServer());
        Server.Call(() =>
        {
            Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>());
            Assert.NotNull(Quest(Server)._questProgressLog);
            Assert.True(Server.Resolve<IQuestTypeRegistry>().TryGet(typeof(Issue), out var descriptor));
            Assert.Null(descriptor.AlternativeSolutionAcceptStrategy);
            Assert.NotNull(descriptor.FinalizationProofStrategy);
        });
        Client.Call(() => Assert.Same(Quest(Client), Assert.Single(Campaign.Current.QuestManager.Quests.OfType<Quest>())));
        Observer.Call(() =>
        {
            Assert.Null(Object<Hero>(Observer, scenario.NotableId).Issue.IssueQuest);
            Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>());
            Assert.Null(RevenueFarmingIssueBehavior.Instance);
        });
    }

    [Fact]
    public void HourlyCollectionReplicatesProgressAndGoldOnlyForTheOwner()
    {
        Accept();
        Collect();
        Tick();
        environment.FlushCoalescer();
        foreach (var instance in new[] { Server, Client }) instance.Call(() =>
        {
            Assert.Equal(80, Quest(instance).RevenueVillages[0].CollectedAmount);
            Assert.Equal(2080, Object<Hero>(instance, owner.HeroId).Gold);
            Assert.Equal(2000, Object<Hero>(instance, other.HeroId).Gold);
        });
        Client.Call(() => Quest(Client).HourlyTick());
        Client.Call(() => Assert.Equal(80, Quest(Client).RevenueVillages[0].CollectedAmount));
        Assert.Empty(menus.MenusFor(Observer));
    }

    [Fact]
    public void ProgressBeforeTheAcceptanceDialogueDoesNotPreventStartingTheQuest()
    {
        scenario.Accept(Client);
        using var tracking = new MethodCallRecorder(AccessTools.Method(typeof(QuestManager), nameof(QuestManager.RemoveTrackedObjectForQuest)));
        Server.Call(() => Quest(Server).OnVillageRaid(Quest(Server).RevenueVillages[0].Village));
        Assert.Equal(0, tracking.CountFor(Client));
        Client.Call(() =>
        {
            var quest = Quest(Client);
            var count = quest.JournalEntries.Count;
            Assert.Equal(1, quest._questProgressLog.CurrentProgress);
            quest.QuestAcceptedConsequences();
            Assert.Same(quest, Assert.Single(Campaign.Current.QuestManager.Quests.OfType<Quest>()));
            Assert.Equal(count, quest.JournalEntries.Count);
            Assert.Equal(1, quest._questProgressLog.CurrentProgress);
        });
    }

    [Fact]
    public void AcceptanceReconcilesARaidTheAcceptingClientHasNotReceived()
    {
        Server.Call(() =>
        {
            using (new AllowedThread()) Object<Settlement>(Server, villageIds[0]).Village.VillageState = Village.VillageStates.Looted;
        });
        Accept();
        foreach (var instance in new[] { Server, Client }) instance.Call(() =>
        {
            Assert.Equal(1, Quest(instance)._questProgressLog.CurrentProgress);
            Assert.True(Quest(instance).RevenueVillages[0].IsRaided);
            Assert.Equal(266, Quest(instance)._totalRequestedDenars);
        });
    }

    [Fact]
    public void CancellationBeforeTheAcceptanceDialogueDoesNotStartTheDetachedQuest()
    {
        scenario.Accept(Client);
        var local = Quest(Client);
        Server.Call(() =>
        {
            var quest = Quest(Server);
            quest.OnVillageRaid(quest.RevenueVillages[0].Village);
            quest.OnVillageRaid(quest.RevenueVillages[1].Village);
        });
        Client.Call(() =>
        {
            local.QuestAcceptedConsequences();
            Assert.Empty(Campaign.Current.QuestManager.Quests.OfType<Quest>());
            Assert.Null(Client.Resolve<IRevenueFarmingSessionStore>().LocalQuest);
        });
        Server.Call(() =>
        {
            var giver = Object<Hero>(Server, scenario.NotableId);
            var town = Object<Settlement>(Server, townId);
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero hero) => new Issue(hero, town),
                typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        scenario.Accept(Client);
        Client.Call(() =>
        {
            var quest = Quest(Client);
            var conversation = Campaign.Current.ConversationManager;
            var agent = new Mock<IAgent>();
            agent.SetupGet(value => value.Character).Returns(quest.QuestGiver.CharacterObject);
            conversation._conversationAgents.Add(agent.Object);
            conversation.ActiveToken = conversation.GetStateIndex("issue_classic_quest_start");
            var line = conversation._sentences.First(sentence =>
                sentence.InputToken == conversation.ActiveToken && sentence.RunCondition());
            line.RunConsequence(Game.Current);
            Assert.NotSame(local, quest);
            Assert.Same(quest, Assert.Single(Campaign.Current.QuestManager.Quests.OfType<Quest>()));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FrameworkAllowsConcurrentPlayersAndRefusesTheSamePlayersSecondQuest(bool samePlayer)
    {
        Accept();
        var giverId = environment.CreateRegisteredObject<Hero>();
        foreach (var instance in Instances) instance.Call(() =>
        {
            using (new AllowedThread())
            {
                var giver = Object<Hero>(instance, giverId);
                giver.Clan = Object<Hero>(instance, scenario.NotableId).Clan;
                giver.StayingInSettlement = Object<Settlement>(instance, townId);
                var issue = new Issue(giver, giver.StayingInSettlement) { StringId = "issue_revenue_second" };
                Campaign.Current.IssueManager._issues.Add(giver, issue);
                giver.OnIssueCreatedForHero(issue);
            }
        });
        var accepter = samePlayer ? Client : Observer;
        accepter.Call(() => Object<Hero>(accepter, giverId).Issue.StartIssueWithQuest());
        if (samePlayer)
        {
            Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
            Client.Call(() => Assert.Single(Campaign.Current.QuestManager.Quests.OfType<Quest>()));
        }
        else
        {
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
            Observer.Call(() => ((Quest)Object<Hero>(Observer, giverId).Issue.IssueQuest).QuestAcceptedConsequences());
            Server.Call(() =>
            {
                Assert.True(Server.Resolve<IIssueOwnershipRegistry>().TryGetOwner(giverId, "issue_revenue_second", out var id));
                Assert.Equal(QuestScenario.ControllerB, id);
            });
            foreach (var client in new[] { Client, Observer })
                client.Call(() => Assert.Single(Campaign.Current.QuestManager.Quests.OfType<Quest>()));
        }
    }

    public static IEnumerable<object[]> IncidentChoices()
    {
        foreach (var id in new[] { "offer_goods_and_troops", "brawl_breaks_out", "landlord_asks_for_money",
            "village_is_under_quarantine", "refuse_to_pay_what_they_owe", "relief_for_the_poorest" })
            for (var option = 0; option < 3; option++)
            {
                yield return new object[] { id, option, true };
                if (option == 0 && id != "offer_goods_and_troops" && id != "brawl_breaks_out")
                    yield return new object[] { id, option, false };
            }
    }

    [Theory]
    [MemberData(nameof(IncidentChoices))]
    public void EveryIncidentChoiceRunsOnServerAndReplaysToOwner(string id, int option, bool harmful)
    {
        Accept();
        Server.Call(() =>
        {
            Object<Settlement>(Server, townId).Town.Loyalty = 100;
            foreach (var key in Quest(Server)._currentVillageEvents.Keys.ToArray()) Quest(Server)._currentVillageEvents[key] = key != id;
        });
        Collect();
        for (var i = 0; i < 4; i++) Tick();
        Server.Call(() => Assert.Equal(id, Session(Server).Incident));
        Client.Call(() => Campaign.Current.CurrentMenuContext.GameMenu.StringId = id);
        incidentDraw = harmful ? 0.1f : 0.9f;
        try
        {
            Client.Call(() => Client.Resolve<IRevenueFarmingActions>().Request(Quest(Client), RevenueFarmingAction.ResolveIncident, id, option));
        }
        finally
        {
            incidentDraw = null;
        }
        environment.FlushCoalescer();
        int amount = 0, progress = 0;
        float additional = 0;
        bool collecting = false;
        Server.Call(() =>
        {
            Assert.Null(Session(Server).Incident);
            amount = Quest(Server).RevenueVillages[0].CollectedAmount;
            progress = Quest(Server)._questProgressLog.CurrentProgress;
            additional = Quest(Server).RevenueVillages[0]._customProgress;
            collecting = Quest(Server).CollectingRevenues;
            AssertIncidentEffects(Server, id, option, harmful);
        });
        Client.Call(() =>
        {
            Assert.Null(Session(Client).Incident);
            Assert.Equal(amount, Quest(Client).RevenueVillages[0].CollectedAmount);
            Assert.Equal(progress, Quest(Client)._questProgressLog.CurrentProgress);
            Assert.Equal(additional, Quest(Client).RevenueVillages[0]._customProgress);
            Assert.Equal(collecting, Quest(Client).CollectingRevenues);
            AssertIncidentEffects(Client, id, option, harmful);
        });
        Assert.Empty(menus.MenusFor(Observer));
    }

    [Theory]
    [InlineData("giver")]
    [InlineData("giver-transit")]
    [InlineData("steward")]
    [InlineData("betray")]
    public void CompletionUsesFrameworkBranchAndFinalizesTheOwnersJournal(string ending)
    {
        Accept();
        CompleteCollections();
        Move(townId);
        if (ending == "giver-transit") Server.Call(() =>
        {
            using (new AllowedThread()) Object<Hero>(Server, owner.HeroId).PartyBelongedTo.CurrentSettlement = null;
        });
        var quest = Quest(Client);
        Client.Call(() =>
        {
            Campaign.Current.CurrentMenuContext.GameMenu.StringId = "town_guard";
            if (ending == "steward") Campaign.Current.GetCampaignBehavior<RevenueFarmingIssueBehavior>().talk_to_steward_on_consequence(new MenuCallbackArgs((MenuContext)null, null));
            else if (ending == "betray") quest.QuestCompletedWithBetray();
            else quest.QuestCompletedWithSuccess();
        });
        Assert.NotEmpty(Client.NetworkSentMessages.GetMessages<GameInterface.Services.Issues.Framework.Finalization.RequestQuestBranch>());
        environment.FlushCoalescer();
        Client.Call(() => Assert.True(quest.IsFinalized));
        foreach (var instance in Instances) instance.Call(() => Assert.Null(Object<Hero>(instance, scenario.NotableId).Issue));
        environment.FlushCoalescer();
        Server.Call(() =>
        {
            Assert.Equal(ending == "betray" ? 3600 : 3068, Object<Hero>(Server, owner.HeroId).Gold);
            Assert.Equal(2000, Object<Hero>(Server, other.HeroId).Gold);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TimeoutPromptsOwnerWithFundsOrFinalizesWithoutFunds(bool hasFunds)
    {
        Accept();
        var clientQuest = Quest(Client);
        Server.Call(() =>
        {
            using (new AllowedThread()) Object<Hero>(Server, owner.HeroId).Gold = hasFunds ? 2000 : 0;
            Quest(Server).QuestDueTime = new CampaignTime(Campaign.Current.MapTimeTracker.Now.NumTicks - CampaignTime.Days(1).NumTicks);
        });
        Tick();
        environment.FlushCoalescer();
        if (hasFunds)
        {
            Client.Call(() => Assert.True(Session(Client).AwaitingPayment));
            Client.Call(() => clientQuest.QuestCompletedWithSuccess());
            environment.FlushCoalescer();
        }
        Client.Call(() => Assert.True(clientQuest.IsFinalized));
        Server.Call(() => Assert.Null(Object<Hero>(Server, scenario.NotableId).Issue));
    }

    [Fact]
    public void RaidStopsCollectionAndAllVillagesRaidedCancelsOnlyThisQuest()
    {
        Accept();
        Collect();
        var quest = Quest(Client);
        Server.Call(() => Quest(Server).OnVillageRaid(Quest(Server).RevenueVillages[0].Village));
        Client.Call(() =>
        {
            Assert.False(quest.CollectingRevenues);
            Assert.Equal(1, quest._questProgressLog.CurrentProgress);
            Assert.Equal(266, quest._totalRequestedDenars);
        });
        Server.Call(() => Quest(Server).OnVillageRaid(Quest(Server).RevenueVillages[1].Village));
        Client.Call(() => Assert.True(quest.IsFinalized));
        Assert.Empty(menus.MenusFor(Observer));
    }

    [Fact]
    public void TheServerDoesNotRemoveTheOwnersClientMapTracking()
    {
        Accept();
        using var tracking = new MethodCallRecorder(AccessTools.Method(typeof(QuestManager), nameof(QuestManager.RemoveTrackedObjectForQuest)));
        CompleteCollections();
        Assert.Equal(0, tracking.CountFor(Server));
        Assert.True(tracking.CountFor(Client) >= 2);
    }

    [Fact]
    public void RefusedPaymentRefreshesTheOwnersProgress()
    {
        Accept();
        Client.Call(() => Quest(Client).QuestCompletedWithSuccess());
        Assert.True(Server.NetworkSentMessages.GetMessages<NetworkRevenueFarmingState>().Last().RefreshPresentation);
        Client.Call(() => Assert.True(Quest(Client).IsOngoing));
    }

    [Fact]
    public void LosingTheDemandMoneyBeforeReplyFinalizesWithTimeout()
    {
        Accept();
        var quest = Quest(Client);
        Server.Call(() => Quest(Server).QuestDueTime = new CampaignTime(Campaign.Current.MapTimeTracker.Now.NumTicks - CampaignTime.Days(1).NumTicks));
        Tick();
        Client.Call(() => Assert.True(Session(Client).AwaitingPayment));
        Server.Call(() =>
        {
            using (new AllowedThread()) Object<Hero>(Server, owner.HeroId).Gold = 0;
        });
        Client.Call(() => quest.QuestCompletedWithSuccess());
        environment.FlushCoalescer();
        Client.Call(() => Assert.True(quest.IsFinalized));
        Server.Call(() => Assert.Null(Object<Hero>(Server, scenario.NotableId).Issue));
    }

    [Fact]
    public void DismissalAndReconnectDoNotLetTheSavedSessionClaimANewQuest()
    {
        Accept();
        var abandoned = Quest(Server);
        Server.Call(() => Server.Resolve<IPlayerManager>().ClearPeer(Client.NetPeer));
        Server.SimulateMessage(this, new PlayerDisconnected(Client.NetPeer, default));
        Server.Call(() =>
        {
            Assert.True(abandoned.IsOngoing);
            Assert.Null(Server.Resolve<IRevenueFarmingSessionStore>().FindOngoing(Object<Hero>(Server, owner.HeroId)));
            Server.Resolve<IControllerIdProvider>().SetControllerId(owner.ControllerId);
            Assert.Null(Server.Resolve<IRevenueFarmingSessionStore>().LocalQuest);
        });
        environment.ConnectRegisteredPlayer(Client, owner.ControllerId);
        Server.Call(() =>
        {
            var giver = Object<Hero>(Server, scenario.NotableId);
            var town = Object<Settlement>(Server, townId);
            var potential = new PotentialIssueData((in PotentialIssueData _, Hero hero) => new Issue(hero, town),
                typeof(Issue), IssueBase.IssueFrequency.Common);
            Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in potential, giver));
        });
        Accept();
        Server.Call(() =>
        {
            var current = Quest(Server);
            Assert.NotSame(abandoned, current);
            Assert.Same(current, Server.Resolve<IRevenueFarmingSessionStore>().LocalQuest);
            Assert.Same(current, Server.Resolve<IRevenueFarmingSessionStore>().FindOngoing(Object<Hero>(Server, owner.HeroId)));
        });
        Collect();
        Tick();
        Client.Call(() => Assert.Equal(80, Quest(Client).RevenueVillages[0].CollectedAmount));
    }

    private void AssertIncidentEffects(EnvironmentInstance instance, string id, int option, bool harmful)
    {
        var quest = Quest(instance);
        var village = quest.RevenueVillages[0];
        var hero = Object<Hero>(instance, owner.HeroId);
        var party = hero.PartyBelongedTo;
        var completes = option == 2 || (id == "offer_goods_and_troops" && option == 1) ||
            (id == "landlord_asks_for_money" && option == 0 && harmful);
        var relation = option == 2 ? (id == "offer_goods_and_troops" ? 3 : 4) : option == 1 ? 2 : -5;
        if (id == "offer_goods_and_troops" && option == 1) relation = 1;
        if (id == "village_is_under_quarantine" && option < 2) relation = option == 0 ? -4 : -2;
        var gold = id == "landlord_asks_for_money" && option == 0 && harmful ? 2000 :
            id == "landlord_asks_for_money" && option == 1 ? 2240 - (village.TargetAmount / 3) : 2240;
        Assert.Equal(gold, hero.Gold);
        Assert.Equal(relation, CharacterRelationManager.GetHeroRelation(hero, Object<Hero>(instance, notableId)));
        Assert.Equal(completes ? 1 : 0, quest._questProgressLog.CurrentProgress);
        Assert.Equal(!completes, quest.CollectingRevenues);
        var concession = option == 1 && id == "refuse_to_pay_what_they_owe" ? 0.5f :
            option == 1 && (id == "village_is_under_quarantine" || id == "relief_for_the_poorest") ? 0.35f : 0f;
        Assert.Equal(concession, village._customProgress);
        var goods = id == "offer_goods_and_troops" && option == 1
            ? (int)(village.TargetAmount * 0.5f / DefaultItems.Grain.Value) : 0;
        Assert.Equal(goods, party.ItemRoster.GetItemNumber(DefaultItems.Grain));
        if (option == 0 && harmful && (id == "landlord_asks_for_money" || id == "relief_for_the_poorest"))
            Assert.InRange(party.MemberRoster.TotalManCount, 36, 38);
        else
            Assert.Equal(40, party.MemberRoster.TotalManCount);
        if (id == "offer_goods_and_troops" && option == 0)
            Assert.InRange(party.MemberRoster.TotalHealthyCount, 37, 38);
        else if (id == "brawl_breaks_out" && option == 1)
            Assert.InRange(party.MemberRoster.TotalHealthyCount, 31, 34);
        else if (id == "refuse_to_pay_what_they_owe" && option == 0 && harmful)
            Assert.InRange(party.MemberRoster.TotalHealthyCount, 33, 34);
        else if (id == "village_is_under_quarantine" && option == 0 && harmful)
            Assert.Equal(20, party.MemberRoster.TotalHealthyCount);
        else
            Assert.Equal(party.MemberRoster.TotalManCount, party.MemberRoster.TotalHealthyCount);
        var otherHero = Object<Hero>(instance, other.HeroId);
        Assert.Equal(2000, otherHero.Gold);
        Assert.Equal(40, otherHero.PartyBelongedTo.MemberRoster.TotalHealthyCount);
        Assert.Equal(0, otherHero.PartyBelongedTo.ItemRoster.GetItemNumber(DefaultItems.Grain));
        Assert.Equal(0, CharacterRelationManager.GetHeroRelation(otherHero, Object<Hero>(instance, notableId)));
    }

    private static bool RandomPrefix(ref float __result)
    {
        if (!incidentDraw.HasValue || !Common.ModInformation.IsServer || RevenueFarmingExecutionScope.Current == null) return true;
        __result = incidentDraw.Value;
        return false;
    }

    private void Accept()
    {
        scenario.Accept(Client);
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkQuestSolutionAcceptRejected>());
        Client.Call(() => Quest(Client).QuestAcceptedConsequences());
    }

    private void Collect()
    {
        Move(villageIds[0]);
        Client.Call(() => Client.Resolve<IRevenueFarmingActions>().Request(Quest(Client), RevenueFarmingAction.Collect));
    }

    private void CompleteCollections()
    {
        foreach (var id in villageIds)
        {
            Move(id);
            Server.Call(() => Quest(Server).RevenueVillages.Single(village => village.Village.Settlement == Object<Settlement>(Server, id)).EventOccurred = true);
            Client.Call(() => Client.Resolve<IRevenueFarmingActions>().Request(Quest(Client), RevenueFarmingAction.Collect));
            for (var i = 0; i < 10; i++) Tick();
        }
        Server.Call(() => Assert.True(Quest(Server)._allRevenuesAreCollected));
    }

    private void Tick() => Server.Call(() => CampaignEventDispatcher.Instance.HourlyTick());
    private void Move(string id)
    {
        foreach (var instance in Instances) instance.Call(() =>
        {
            using (new AllowedThread()) Object<MobileParty>(instance, owner.MobilePartyId).CurrentSettlement = Object<Settlement>(instance, id);
        });
    }
    private Quest Quest(EnvironmentInstance instance) => (Quest)scenario.QuestOf(instance);
    private RevenueFarmingSession Session(EnvironmentInstance instance) => instance.Resolve<IRevenueFarmingSessionStore>().Get(Quest(instance));
    private Player Player(string controller)
    {
        Player result = null!;
        Server.Call(() => Assert.True(Server.Resolve<IPlayerManager>().TryGetPlayer(controller, out result)));
        return result;
    }
    private static T Object<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }
}
