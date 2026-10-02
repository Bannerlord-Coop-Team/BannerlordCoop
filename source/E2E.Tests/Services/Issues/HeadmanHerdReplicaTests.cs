using Common.Util;
using Common.Network;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Connections.States;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using GameInterface.Services.Heroes.Patches;
using E2E.Tests.Environment.Instance;
using Coop.Core.Client.Services.Heroes.Messages;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using Moq;
using GameInterface.Services.Issues.Generic.Dispatch;
using GameInterface.Services.Party.Messages;
using GameInterface.Services.TroopRosters.Messages;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using Common.Messaging;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Roster;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using E2E.Tests.Environment;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Issues;

public class HeadmanHerdReplicaTests
{
    private readonly ITestOutputHelper output;

    public HeadmanHerdReplicaTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AcceptanceFreezesTheOwnersQuestOnBothClients(bool alternative)
        => ExerciseAcceptance(alternative);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeliveryAndRejectionApplyTheOwnersConsequencesOnBothClients(bool reject)
        => WithHerdEnabled(() => ExerciseAcceptance(false, reject));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompanionCompletionIncludesTheNativeZeroRandomFailure(bool failure)
        => WithHerdEnabled(() => ExerciseAcceptance(true, alternativeFailure: failure));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayerRemovalReturnsTheCompanionAndClearsPendingTroops(bool completeFirst)
        => WithHerdEnabled(() => ExerciseAcceptance(true, alternativeFailure: completeFirst ? false : null,
            removePlayerAfterAlternative: completeFirst));

    [Theory]
    [InlineData(false, "hero")]
    [InlineData(false, "party")]
    [InlineData(false, "both")]
    [InlineData(true, "hero")]
    [InlineData(true, "party")]
    [InlineData(true, "both")]
    public void PlayerRemovalWithMissingObjectsStillReleasesCompanions(bool completeFirst, string missingObject)
        => WithHerdEnabled(() => ExerciseAcceptance(true, alternativeFailure: completeFirst ? false : null,
            removePlayerAfterAlternative: completeFirst, missingOwnerObject: missingObject));

    [Theory]
    [InlineData(false, "hero")]
    [InlineData(false, "both")]
    [InlineData(true, "hero")]
    [InlineData(true, "both")]
    public void MissingHeroReconnectReleasesTheOldPlayersCompanions(bool completeFirst, string missingObject)
        => WithHerdEnabled(() => ExerciseAcceptance(true, alternativeFailure: completeFirst ? false : null,
            removePlayerAfterAlternative: completeFirst, missingOwnerObject: missingObject, reconnectMissingHero: true));

    [Fact]
    public void AcceptedCompanionMissionIgnoresALatePickerCancel()
        => WithHerdEnabled(() => ExerciseAcceptance(true, latePickerCancel: true));

    [Fact]
    public void FailedCompanionAcceptanceRestoresThePartyAndUnacceptedIssueOnBothClients()
        => ExerciseAcceptance(true, failAlternativeCapture: true);

    [Theory]
    [InlineData("wounded")]
    [InlineData("pregnant")]
    [InlineData("wrong-party")]
    public void CompanionAcceptanceRechecksAvailabilityAfterThePicker(string changedState)
        => ExerciseAcceptance(true, staleCompanion: changedState);

    private sealed class Store : IDataStore
    {
        private readonly Dictionary<string, object> data;
        public bool IsSaving { get; }
        public bool IsLoading => !IsSaving;
        public Store(bool saving, Dictionary<string, object> data) { IsSaving = saving; this.data = data; }
        public bool SyncData<T>(string key, ref T value)
        {
            if (IsSaving) data[key] = value;
            else value = data.TryGetValue(key, out var saved) ? (T)saved : default;
            return true;
        }
    }

    private static void ResolveMissingPlayer(EnvironmentInstance server, EnvironmentInstance client, Player player)
    {
        var connection = new Mock<IConnectionLogic>();
        connection.SetupGet(value => value.Peer).Returns(client.NetPeer);
        using var state = new ResolveCharacterState(connection.Object, server.Resolve<IMessageBroker>(), server.Resolve<INetwork>(),
            Mock.Of<IModuleValidator>(), server.Resolve<IPlayerManager>(), server.Resolve<IPlayerPartyRestorer>(), server.ObjectManager,
            Mock.Of<IModuleInfoProvider>(), Mock.Of<IExistingPlayerSender>(), Mock.Of<ISteamBanList>(), Mock.Of<IJoinValidationDenialLog>(),
            server.Resolve<IHeadmanHerdQuestAuthority>());
        state.Handle_ClientValidate(new MessagePayload<NetworkClientValidate>(client.NetPeer, new NetworkClientValidate(player.ControllerId)));
        connection.Verify(value => value.CreateCharacter(), Times.Once);
    }

    private static void FailAlternativeCapture()
        => throw new InvalidOperationException("Injected accepted-field capture failure");

    private static void WithHerdEnabled(Action action)
    {
        var added = DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Add(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior));
        try
        {
            action();
        }
        finally
        {
            if (added) DisableAllIssueBehaviorsExceptAllowlist.Allowlist.Remove(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior));
        }
    }

    private void ExerciseAcceptance(bool alternative, bool? reject = null, bool? alternativeFailure = null,
        bool failAlternativeCapture = false, string? staleCompanion = null, bool? removePlayerAfterAlternative = null,
        string? missingOwnerObject = null, bool latePickerCancel = false, bool reconnectMissingHero = false)
    {
        using var environment = new E2ETestEnvironment(output);
        var server = environment.Server;
        server.Resolve<GameInterface.IGameInterface>().PatchGameStarted();
        var client = environment.Clients.First();
        if (alternativeFailure.HasValue || reject.HasValue)
        {
            foreach (var getSkill in new Func<SkillObject>[] { () => DefaultSkills.Riding, () => DefaultSkills.Scouting, () => DefaultSkills.Charm })
            {
                uint handle = 0;
                server.Call(() =>
                {
                    var skill = getSkill();
                    Assert.True(server.ObjectManager.AddExisting(skill.StringId, skill));
                    Assert.True(server.ObjectManager.TryGetHandle(skill, out handle));
                });
                foreach (var peer in environment.Clients)
                    peer.Call(() =>
                    {
                        var skill = getSkill();
                        Assert.True(peer.ObjectManager.AddExisting(skill.StringId, skill, handle));
                    });
            }
        }
        var giverId = environment.CreateRegisteredObject<Hero>();
        var targetId = environment.CreateRegisteredObject<Hero>();
        var companionId = environment.CreateRegisteredObject<Hero>();
        var villageId = environment.CreateRegisteredObject<Village>();
        var villageSettlementId = environment.CreateRegisteredObject<Settlement>();
        var townId = environment.CreateRegisteredObject<Town>();
        var targetSettlementId = environment.CreateRegisteredObject<Settlement>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        var categoryId = environment.CreateRegisteredObject<ItemCategory>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var clanId = environment.CreateRegisteredObject<Clan>();
        string heroId = null!;
        foreach (var instance in environment.Clients.Prepend(server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(targetId, out var target));
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<Village>(villageId, out var village));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(villageSettlementId, out var villageSettlement));
                Assert.True(instance.ObjectManager.TryGetObject<Town>(townId, out var town));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var destination));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.True(instance.ObjectManager.TryGetObject<ItemCategory>(categoryId, out var category));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
                using (new AllowedThread())
                {
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    Campaign.Current.PlayerTraitDeveloper ??= new PropertyOwner<PropertyObject>();
                    if (failAlternativeCapture) new JournalLogsCampaignBehavior().RegisterEvents();
                    if (reject.HasValue) new IssuesCampaignBehavior().RegisterEvents();
                    villageSettlement.SetSettlementComponent(village);
                    destination.SetSettlementComponent(town);
                    village.Bound = destination;
                    village.Hearth = 100;
                    town.OwnerClan = clan;
                    town.Prosperity = 100;
                    party.LeaderHero.Clan = clan;
                    town.Security = 40;
                    giver.StayingInSettlement = villageSettlement;
                    giver.Occupation = Occupation.RuralNotable;
                    target.StayingInSettlement = destination;
                    target.ChangeState(Hero.CharacterStates.Active);
                    item.Value = 40;
                    item.ItemCategory = category;
                    party.CurrentSettlement = villageSettlement;
                    party.LeaderHero.PartyBelongedTo = party;
                    companion.ChangeState(Hero.CharacterStates.Active);
                    companion.Clan = clan;
                    companion.PartyBelongedTo = party;
                    companion.HitPoints = 100;
                    troop.Level = 20;
                    if (instance == server)
                    {
                        party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
                        party.MemberRoster.AddToCounts(troop, 30);
                    }
                }
                var factory = instance.Resolve<IHeadmanNeedsToDeliverAHerdIssueInterface>();
                var issue = factory.ConstructReplicated(giver, destination, target, item);
                factory.RegisterReplicated(giver, issue, "accept-herd", CampaignTime.Now, CampaignTime.DaysFromNow(30));
                instance.Resolve<IIssueGenerationRegistry>().SetGeneration(giver, 1);
                if (instance == server) Assert.True(instance.ObjectManager.TryGetId(party.LeaderHero, out heroId));
            });
        }
        environment.FlushCoalescer();
        var player = new Player("herd-owner", heroId, partyId, "", "");
        server.Call(() => Assert.True(server.Resolve<IPlayerManager>().AddPlayer(player)));
        environment.ConnectRegisteredPlayer(client, player.ControllerId);
        foreach (var peer in environment.Clients)
        {
            peer.Resolve<IControllerIdProvider>().SetControllerId(peer == client ? player.ControllerId : "other-player");
            peer.SimulateMessage(server.NetPeer, new NetworkNewPlayerHeroCreated(player.ControllerId, player, Array.Empty<byte>()));
        }
        var initialIssues = new Dictionary<EnvironmentInstance, (CampaignTime Due, float Difficulty)>();
        foreach (var instance in environment.Clients.Prepend(server))
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                initialIssues[instance] = (giver.Issue.IssueDueTime, giver.Issue._issueDifficultyMultiplier);
            });
        var capture = AccessTools.Method(typeof(HeadmanNeedsToDeliverAHerdAcceptInterface),
            nameof(HeadmanNeedsToDeliverAHerdAcceptInterface.TryCaptureAlternativeFields));
        var fault = new Harmony("herd-accept-capture-test");
        var changeState = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
        var fixturePrefix = Harmony.GetPatchInfo(changeState).Prefixes.Single(patch => patch.owner == "Coop.Testing");
        var testing = new Harmony(fixturePrefix.owner);
        if (staleCompanion != null)
            server.Call(() =>
            {
                Assert.True(server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                using var setup = new AllowedThread();
                if (staleCompanion == "wounded") companion.HitPoints = 1;
                if (staleCompanion == "pregnant") companion.IsPregnant = true;
                if (staleCompanion == "wrong-party") companion.PartyBelongedTo = null;
            });
        if (failAlternativeCapture)
        {
            fault.Patch(capture, prefix: new HarmonyMethod(typeof(HeadmanHerdReplicaTests), nameof(FailAlternativeCapture)));
            testing.Unpatch(changeState, fixturePrefix.PatchMethod);
        }
        try
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                MessageBroker.Instance.Publish(giver, new IssueConversationOpenedLocally(giver, player.ControllerId));
                Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var requestedParty));
                output.WriteLine($"Before acceptance client troop count={requestedParty.MemberRoster.GetTroopCount(troop)}");
                if (alternative)
                {
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                    giver.Issue.AlternativeSolutionSentTroops.AddToCounts(troop, 30);
                    if (latePickerCancel)
                    {
                        var agent = new Mock<IAgent>();
                        agent.SetupGet(value => value.Character).Returns(giver.CharacterObject);
                        Campaign.Current.ConversationManager._conversationAgents.Clear();
                        Campaign.Current.ConversationManager._conversationAgents.Add(agent.Object);
                        using var playerScope = new MainHeroSubstitutionScope(requestedParty.LeaderHero, requestedParty);
                        GenericQuestTypeAlternativePickerPatch.BeginSelection();
                        IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_consequence();
                    }
                    else giver.Issue.StartIssueWithAlternativeSolution();
                }
                else
                {
                    Assert.True(giver.Issue.StartIssueWithQuest());
                }
            });
            environment.FlushCoalescer();
        }
        finally
        {
            if (failAlternativeCapture)
            {
                fault.Unpatch(capture, HarmonyPatchType.All, fault.Id);
                testing.Patch(changeState, prefix: new HarmonyMethod(fixturePrefix.PatchMethod));
            }
        }
        foreach (var instance in environment.Clients.Prepend(server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                output.WriteLine($"Acceptance instance server={instance == server} owner={instance == client}: alternative={alternative}, troops={party.MemberRoster.GetTroopCount(troop)}, issue={giver.Issue?.StringId}, quest={giver.Issue?.IssueQuest?.StringId}");
                if (failAlternativeCapture || staleCompanion != null)
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                    Assert.True(companion.IsActive);
                    Assert.True(giver.Issue.IsOngoingWithoutQuest);
                    Assert.False(giver.Issue.IsTriedToSolveBefore);
                    Assert.Equal(initialIssues[instance].Due, giver.Issue.IssueDueTime);
                    Assert.Equal(initialIssues[instance].Difficulty, giver.Issue._issueDifficultyMultiplier);
                    Assert.Empty(giver.Issue.JournalEntries);
                    Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(log => log.IsRelatedTo(giver.Issue)));
                    Assert.Equal(0, giver.Issue.AlternativeSolutionSentTroops.TotalManCount);
                    Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                    Assert.Equal(30, party.MemberRoster.GetTroopCount(troop));
                    Assert.Empty(instance.Resolve<IHeadmanHerdPersonalOwnership>().Snapshot());
                    Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
                    return;
                }
                Assert.True(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out var controller));
                Assert.Equal(player.ControllerId, controller);
                if (alternative)
                {
                    Assert.True(giver.Issue.IsSolvingWithAlternative);
                    Assert.Single(giver.Issue.JournalEntries);
                    Assert.Equal(30, giver.Issue.AlternativeSolutionSentTroops.GetTroopCount(troop));
                    Assert.True(instance.ObjectManager.TryGetHandle(party.MemberRoster, out var rosterHandle));
                    Assert.True(instance.ObjectManager.TryGetHandle(troop, out var troopHandle));
                    var batches = (instance == server ? instance.NetworkSentMessages : instance.InternalMessages).GetMessages<NetworkTroopRosterElementBatch>();
                    output.WriteLine("Roster counts on wire: " + string.Join(",", batches.Where(batch => batch.RosterId == rosterHandle && batch.CharacterId == troopHandle).SelectMany(batch => batch.Operations).Select(op => op.Count)));
                    Assert.Equal(0, party.MemberRoster.GetTroopCount(troop));
                }
                else
                {
                    var quest = Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest>(giver.Issue.IssueQuest);
                    Assert.True(quest.IsOngoing);
                    Assert.Single(quest.JournalEntries);
                    Assert.Equal(quest._animalCountToDeliver, party.ItemRoster.GetItemNumber(item));
                    Assert.Contains(quest, Campaign.Current.QuestManager.Quests);
                    if (instance != server)
                    {
                        Assert.Equal(instance == client, Campaign.Current.QuestManager.IsQuestGiver(giver));
                        Assert.Equal(instance == client, Campaign.Current.QuestManager.GetQuestGiverQuests(giver).Any());
                    }
                }
            });
        }

        if (latePickerCancel)
        {
            client.Call(() =>
            {
                var giver = client.GetRegisteredObject<Hero>(giverId);
                var party = client.GetRegisteredObject<MobileParty>(partyId);
                var troop = client.GetRegisteredObject<CharacterObject>(troopId);
                var companion = client.GetRegisteredObject<Hero>(companionId);
                using var scope = new MainHeroSubstitutionScope(party.LeaderHero, party);
                IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence();
                Assert.True(giver.Issue.IsSolvingWithAlternative);
                Assert.Equal(30, giver.Issue.AlternativeSolutionSentTroops.GetTroopCount(troop));
                Assert.Equal(1, giver.Issue.AlternativeSolutionSentTroops.GetTroopCount(companion.CharacterObject));
                Assert.Equal(0, party.MemberRoster.GetTroopCount(troop));
            });
        }

        void RemovePlayerAndAssertCleanup()
        {
            var leaderPartyId = environment.CreateRegisteredObject<MobileParty>();
            foreach (var instance in environment.Clients.Prepend(server))
                instance.Call(() =>
                {
                    using var setup = new AllowedThread();
                    var clan = instance.GetRegisteredObject<Clan>(clanId);
                    var leader = instance.GetRegisteredObject<MobileParty>(leaderPartyId).LeaderHero;
                    leader.Clan = clan;
                    clan.SetLeader(leader);
                });
            var hearths = new Dictionary<EnvironmentInstance, float>();
            foreach (var instance in environment.Clients.Prepend(server))
                instance.Call(() =>
                {
                    hearths[instance] = instance.GetRegisteredObject<Hero>(giverId).CurrentSettlement.Village.Hearth;
                    using var setup = new AllowedThread();
                    var otherTroops = TroopRoster.CreateDummyTroopRoster();
                    otherTroops.AddToCounts(instance.GetRegisteredObject<CharacterObject>(troopId), 3);
                    instance.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit("another-owner", otherTroops);
                });
            testing.Unpatch(changeState, fixturePrefix.PatchMethod);
            try
            {
                server.Call(() =>
                {
                    if (missingOwnerObject == "hero" || missingOwnerObject == "both")
                        Assert.True(server.ObjectManager.Remove(server.GetRegisteredObject<Hero>(heroId)));
                    if (missingOwnerObject == "party" || missingOwnerObject == "both")
                        Assert.True(server.ObjectManager.Remove(server.GetRegisteredObject<MobileParty>(partyId)));
                    if (reconnectMissingHero)
                        ResolveMissingPlayer(server, client, player);
                    else
                        server.Resolve<GameInterface.Services.Players.Handlers.PlayerDeletionHandler>().CompleteGameOver(player);
                    Assert.False(server.Resolve<IPlayerManager>().TryGetPlayer(player.ControllerId, out _));
                });
                environment.FlushCoalescer();
            }
            finally
            {
                testing.Patch(changeState, prefix: new HarmonyMethod(fixturePrefix.PatchMethod));
            }
            foreach (var instance in environment.Clients.Prepend(server))
                instance.Call(() =>
                {
                    var giver = instance.GetRegisteredObject<Hero>(giverId);
                    Assert.Null(giver.Issue);
                    var companion = instance.GetRegisteredObject<Hero>(companionId);
                    output.WriteLine($"Removal server={instance == server} owner={instance == client} companion={companion.HeroState}");
                    Assert.True(companion.IsActive);
                    var pending = instance.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
                    Assert.False(pending.TryGet(player.ControllerId, out _));
                    Assert.True(pending.TryGet("another-owner", out var other));
                    Assert.Equal(3, other.TotalManCount);
                    Assert.Equal(hearths[instance], giver.CurrentSettlement.Village.Hearth);
                    Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
                });
        }

        if (removePlayerAfterAlternative == false)
        {
            RemovePlayerAndAssertCleanup();
            return;
        }

        if (alternativeFailure.HasValue)
        {
            var issues = new Dictionary<EnvironmentInstance, (IssueBase Issue, int Gold, float Hearth)>();
            foreach (var instance in environment.Clients.Prepend(server))
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                    issues[instance] = (giver.Issue, hero.Gold, giver.CurrentSettlement.Village.Hearth);
                });
            }
            var reward = 0;
            var expectedSkillXp = 0f;
            SkillObject rewardSkill = null!;
            var returnedElements = Array.Empty<(int Number, int Wounded, int Xp)>();
            var companionHitPoints = 0;
            server.Call(() =>
            {
                var issue = issues[server].Issue;
                var companion = server.GetRegisteredObject<Hero>(companionId);
                rewardSkill = issue._companionRewardSkill;
                Assert.True(server.ObjectManager.TryGetHandle(rewardSkill, out _));
                var xpGain = issue.CompanionSkillRewardXP
                    * Campaign.Current.Models.GenericXpModel.GetXpMultiplier(companion)
                    * companion.HeroDeveloper.GetFocusFactor(rewardSkill);
                Assert.True(xpGain > 0);
                expectedSkillXp = companion.HeroDeveloper.GetSkillXp(rewardSkill) + (alternativeFailure.Value ? 0 : xpGain);
                reward = ((HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue)issue).RewardGold;
                issue.AlternativeSolutionReturnTimeForTroops = CampaignTime.Now - CampaignTime.Days(1);
                Assert.Equal(0, issue._failureChance);
                var rng = Game.Current.RandomGenerator;
                var saved = (rng._x, rng._y, rng._z, rng._w);
                try
                {
                    // Native XorShift returns zero from x=0,w=0 while y/z keep the state nonzero.
                    rng._x = alternativeFailure.Value ? 0u : 1u;
                    rng._y = 1;
                    rng._z = 2;
                    rng._w = 0;
                    Assert.Equal(alternativeFailure.Value, MBRandom.RandomFloat == 0);
                    rng._x = alternativeFailure.Value ? 0u : 1u;
                    rng._y = 1;
                    rng._z = 2;
                    rng._w = 0;
                    issue.CompleteIssueWithAlternativeSolution();
                    var returned = server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
                    Assert.True(returned.TryGet(player.ControllerId, out var troops));
                    returnedElements = troops.GetTroopRoster().Select(element => (element.Number, element.WoundedNumber, element.Xp)).ToArray();
                    companionHitPoints = companion.HitPoints;
                    Assert.Equal(expectedSkillXp, companion.HeroDeveloper.GetSkillXp(rewardSkill));
                }
                finally
                {
                    (rng._x, rng._y, rng._z, rng._w) = saved;
                }
            });
            environment.FlushCoalescer();
            foreach (var instance in environment.Clients.Prepend(server))
            {
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                    Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
                    Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                    var initial = issues[instance];
                    var companion = instance.GetRegisteredObject<Hero>(companionId);
                    var localSkill = instance.GetRegisteredObject<SkillObject>(rewardSkill.StringId);
                    Assert.Equal(expectedSkillXp, companion.HeroDeveloper.GetSkillXp(localSkill));
                    Assert.Equal(companionHitPoints, companion.HitPoints);
                    output.WriteLine($"Companion completion server={instance == server} owner={instance == client}: failure={alternativeFailure}, gold={hero.Gold}, hearth={giver.CurrentSettlement.Village.Hearth}, logs={initial.Issue.JournalEntries.Count}");
                    Assert.Null(giver.Issue);
                    Assert.False(instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
                    Assert.Equal(initial.Gold + (alternativeFailure.Value ? 0 : reward), hero.Gold);
                    Assert.Equal(initial.Hearth + (alternativeFailure.Value ? 0 : 50), giver.CurrentSettlement.Village.Hearth);
                    Assert.Equal(2, initial.Issue.JournalEntries.Count);
                    Assert.Equal(0, initial.Issue.AlternativeSolutionSentTroops.TotalManCount);
                    var pending = instance.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>();
                    if (instance == server || instance == client)
                    {
                        Assert.True(pending.TryGet(player.ControllerId, out var returned));
                        Assert.Equal(31, returned.TotalManCount);
                        Assert.Equal(30, returned.GetTroopCount(troop));
                        Assert.Equal(returnedElements, returned.GetTroopRoster()
                            .Select(element => (element.Number, element.WoundedNumber, element.Xp)).ToArray());
                    }
                    else Assert.False(pending.TryGet(player.ControllerId, out _));
                });
            }
            if (removePlayerAfterAlternative == true) RemovePlayerAndAssertCleanup();
            return;
        }

        if (!reject.HasValue) return;
        var before = new Dictionary<EnvironmentInstance, (QuestBase Quest, int Gold, int Herd, int Stock, float Hearth, float Prosperity)>();
        var effects = new Dictionary<EnvironmentInstance, (float Power, int Relation, int SharedHonorXp, float Crime)>();
        foreach (var instance in environment.Clients.Prepend(server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var destination));
                before[instance] = (giver.Issue.IssueQuest, party.LeaderHero.Gold, party.ItemRoster.GetItemNumber(item),
                    destination.ItemRoster.GetItemNumber(item), giver.CurrentSettlement.Village.Hearth, destination.Town.Prosperity);
                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(player.ControllerId, out var registeredPlayer));
                registeredPlayer.CrimeRatings.TryGetValue(clanId, out var crime);
                effects[instance] = (giver.Power, party.LeaderHero.GetRelation(giver),
                    Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor), crime);
                using (new AllowedThread())
                {
                    party.CurrentSettlement = destination;
                    // Publicizer preserves the readonly market field.
                    if (destination.Town._marketData == null)
                        AccessTools.Field(typeof(Town), nameof(Town._marketData)).SetValue(destination.Town, new TownMarketData(destination.Town));
                }
            });
        }
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var quest = (HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest)before[client].Quest;
            using var scope = new MainHeroSubstitutionScope(party.LeaderHero, party);
            if (reject.Value) quest.DeliverHerdRejectOnConsequence();
            else quest.DeliverHerdOnConsequence();
        });
        environment.FlushCoalescer();
        foreach (var instance in environment.Clients.Prepend(server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                Assert.True(instance.ObjectManager.TryGetObject<Settlement>(targetSettlementId, out var destination));
                var initial = before[instance];
                var quest = (HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest)initial.Quest;
                output.WriteLine($"Terminal instance server={instance == server} owner={instance == client}: reject={reject}, finalized={quest.IsFinalized}, gold={party.LeaderHero.Gold}, herd={party.ItemRoster.GetItemNumber(item)}, stock={destination.ItemRoster.GetItemNumber(item)}, hearth={giver.CurrentSettlement.Village.Hearth}, prosperity={destination.Town.Prosperity}");
                Assert.True(quest.IsFinalized);
                Assert.Null(giver.Issue);
                Assert.DoesNotContain(quest, Campaign.Current.QuestManager.Quests);
                Assert.Equal(initial.Gold + (reject.Value ? 0 : quest._rewardGold), party.LeaderHero.Gold);
                Assert.Equal(initial.Herd - (reject.Value ? 0 : quest._animalCountToDeliver), party.ItemRoster.GetItemNumber(item));
                Assert.Equal(initial.Stock + (reject.Value ? 0 : quest._animalCountToDeliver), destination.ItemRoster.GetItemNumber(item));
                Assert.Equal(initial.Hearth + (reject.Value ? 0 : 50), giver.CurrentSettlement.Village.Hearth);
                Assert.Equal(initial.Prosperity + (reject.Value ? -10 : 50), destination.Town.Prosperity);
                Assert.Equal(effects[instance].Power + (reject.Value ? -5 : 5), giver.Power);
                Assert.Equal(effects[instance].Relation + (reject.Value ? -10 : 5), party.LeaderHero.GetRelation(giver));
                Assert.Equal(effects[instance].SharedHonorXp, Campaign.Current.PlayerTraitDeveloper.GetPropertyValue(DefaultTraits.Honor));
                Assert.True(instance.Resolve<IPlayerManager>().TryGetPlayer(player.ControllerId, out var registeredPlayer));
                registeredPlayer.CrimeRatings.TryGetValue(clanId, out var crime);
                Assert.Equal(effects[instance].Crime + (reject.Value ? 20 : 0), crime);
                if (instance == server)
                {
                    var traits = instance.Resolve<PendingRegistry<PropertyOwner<PropertyObject>>>();
                    Assert.True(traits.TryGet(party.LeaderHero, out var progress));
                    Assert.Equal(reject.Value ? -30 : 30, progress.GetPropertyValue(DefaultTraits.Honor));
                }
                Assert.Equal(2, quest.JournalEntries.Count);
                if (instance != server) Assert.False(Campaign.Current.VisualTrackerManager.CheckTracked(destination));
            });
        }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "hero")]
    [InlineData(true, "party")]
    [InlineData(true, "both")]
    [InlineData(true, "hero-reconnect")]
    [InlineData(true, "both-reconnect")]
    public void PlayerChangeCancelsOnlyTheOwnersHerdAndJoinDoesNotCancelEitherQuest(bool removePlayer, string? missingObject)
        => WithHerdEnabled(() =>
        {
            using var environment = new E2ETestEnvironment(output);
            var server = environment.Server;
            server.Resolve<GameInterface.IGameInterface>().PatchGameStarted();
            var itemId = environment.CreateRegisteredObject<ItemObject>();
            var villageId = environment.CreateRegisteredObject<Settlement>();
            var destinationId = environment.CreateRegisteredObject<Settlement>();
            var giverIds = new[] { environment.CreateRegisteredObject<Hero>(), environment.CreateRegisteredObject<Hero>() };
            var partyIds = new[] { environment.CreateRegisteredObject<MobileParty>(), environment.CreateRegisteredObject<MobileParty>() };
            var players = new Player[2];
            var quests = new Dictionary<EnvironmentInstance, QuestBase[]>();
            server.Call(() =>
            {
                for (var i = 0; i < players.Length; i++)
                {
                    Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyIds[i], out var party));
                    Assert.True(server.ObjectManager.TryGetId(party.LeaderHero, out var heroId));
                    players[i] = new Player($"herd-player-{i}", heroId, partyIds[i], "", "");
                    Assert.True(server.Resolve<IPlayerManager>().AddPlayer(players[i]));
                }
            });
            var clients = environment.Clients.ToArray();
            for (var i = 0; i < clients.Length; i++)
            {
                environment.ConnectRegisteredPlayer(clients[i], players[i].ControllerId);
                clients[i].Resolve<IControllerIdProvider>().SetControllerId(players[i].ControllerId);
                foreach (var player in players)
                    clients[i].SimulateMessage(server.NetPeer, new NetworkNewPlayerHeroCreated(player.ControllerId, player, Array.Empty<byte>()));
            }
            foreach (var instance in environment.Clients.Prepend(server))
                instance.Call(() =>
                {
                    var journal = new JournalLogsCampaignBehavior();
                    journal.RegisterEvents();
                    var viewData = new ViewDataTrackerCampaignBehavior();
                    viewData.RegisterEvents();
                    Campaign.Current.AddCampaignBehaviorManager(new CampaignBehaviorManager(new CampaignBehaviorBase[] { journal, viewData }));
                    Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                    Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                    Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                    Assert.True(instance.ObjectManager.TryGetObject<Settlement>(villageId, out var village));
                    Assert.True(instance.ObjectManager.TryGetObject<Settlement>(destinationId, out var destination));
                    destination.IsReady = true;
                    quests[instance] = new QuestBase[2];
                    for (var i = 0; i < players.Length; i++)
                    {
                        Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverIds[i], out var giver));
                        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyIds[i], out var party));
                        using var scope = new MainHeroSubstitutionScope(party.LeaderHero, party);
                        using (new AllowedThread())
                        {
                            giver.IsReady = true;
                            giver.Occupation = Occupation.RuralNotable;
                            giver.StayingInSettlement = village;
                            party.ItemRoster.AddToCounts(item, 5);
                        }
                        var factory = instance.Resolve<IHeadmanNeedsToDeliverAHerdIssueInterface>();
                        var issue = factory.ConstructReplicated(giver, destination, giver, item);
                        factory.RegisterReplicated(giver, issue, $"personal-herd-{i}", CampaignTime.Now, CampaignTime.DaysFromNow(30));
                        instance.Resolve<IIssueGenerationRegistry>().SetGeneration(giver, 1);
                        instance.Resolve<IHeadmanNeedsToDeliverAHerdAcceptInterface>().MirrorQuestAccepted(giver,
                            new HeadmanHerdQuestAcceptFields($"personal-herd-{i}-quest", 5, 200, CampaignTime.DaysFromNow(20), 0.1f,
                                new HeadmanHerdJournalEntryData(new JournalLog(CampaignTime.Now, new TextObject("Herd accepted")))));
                        instance.Resolve<IIssueOwnershipRegistry>().SetOwner(giver, players[i].ControllerId);
                        quests[instance][i] = issue.IssueQuest;
                    }
                });
            environment.FlushCoalescer();
            foreach (var instance in environment.Clients.Prepend(server))
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                    foreach (var partyId in partyIds)
                    {
                        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                        Assert.Equal(5, party.ItemRoster.GetItemNumber(item));
                    }
                });
            for (var i = 0; i < clients.Length; i++)
            {
                var index = i;
                var client = clients[i];
                client.Call(() =>
                {
                    Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyIds[index], out var party));
                    Assert.True(client.ObjectManager.TryGetObject<Settlement>(destinationId, out var destination));
                    using var scope = new MainHeroSubstitutionScope(party.LeaderHero, party);
                    var before = new QuestsVM(() => { });
                    Assert.Same(quests[client][index], Assert.Single(before.ActiveQuestsList).Quest);
                    Assert.Empty(before.OldQuestsList);
                    before.OnFinalize();
                    var store = new Dictionary<string, object>();
                    var behavior = new IssuesCampaignBehavior();
                    behavior.SyncData(new Store(true, store));
                    var ownership = client.Resolve<IHeadmanHerdPersonalOwnership>();
                    var owners = ownership.Snapshot().OrderBy(entry => entry.Key).ToArray();
                    ownership.Restore(Array.Empty<KeyValuePair<string, string>>());
                    behavior.SyncData(new Store(false, store));
                    Assert.Equal(owners, ownership.Snapshot().OrderBy(entry => entry.Key).ToArray());
                    Campaign.Current.VisualTrackerManager.ResetTracker();
                    Campaign.Current.QuestManager.OnGameLoaded(null);
                    Assert.Equal(1, Campaign.Current.VisualTrackerManager._trackedObjects[destination].TrackerCount);
                    Assert.Equal(2, Campaign.Current.QuestManager.Quests.Count);
                    Assert.All(quests[client], quest => Assert.Single(quest.JournalEntries));
                    var after = new QuestsVM(() => { });
                    Assert.Same(quests[client][index], Assert.Single(after.ActiveQuestsList).Quest);
                    Assert.Empty(after.OldQuestsList);
                    after.OnFinalize();
                });
            }
            clients[0].Call(() =>
            {
                Assert.True(clients[0].ObjectManager.TryGetObject<Hero>(players[0].HeroId, out var hero));
                Assert.True(clients[0].ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var party));
                Campaign.Current.QuestManager.OnPlayerCharacterChanged(hero, hero, party, false);
                Assert.All(quests[clients[0]], quest => Assert.True(quest.IsOngoing));
            });
            server.Call(() =>
            {
                if (removePlayer)
                {
                    if (missingObject?.StartsWith("hero") == true || missingObject?.StartsWith("both") == true)
                        Assert.True(server.ObjectManager.Remove(server.GetRegisteredObject<Hero>(players[0].HeroId)));
                    if (missingObject?.StartsWith("party") == true || missingObject?.StartsWith("both") == true)
                        Assert.True(server.ObjectManager.Remove(server.GetRegisteredObject<MobileParty>(partyIds[0])));
                    if (missingObject?.EndsWith("-reconnect") == true)
                        ResolveMissingPlayer(server, clients[0], players[0]);
                    else
                        server.Resolve<GameInterface.Services.Players.Handlers.PlayerDeletionHandler>().CompleteGameOver(players[0]);
                    Assert.False(server.Resolve<IPlayerManager>().TryGetPlayer(players[0].ControllerId, out _));
                }
                else server.Resolve<IHeadmanHerdQuestAuthority>().CancelForPlayerChange(players[0].ControllerId);
            });
            environment.FlushCoalescer();
            Assert.Single(server.NetworkSentMessages.GetMessages<NetworkHeadmanHerdJournalAdded>());
            foreach (var instance in environment.Clients.Prepend(server))
                instance.Call(() =>
                {
                    Assert.True(instance.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
                    for (var i = 0; i < players.Length; i++)
                    {
                        Assert.True(instance.ObjectManager.TryGetObject<Hero>(giverIds[i], out var giver));
                        Assert.Equal(i == 0, quests[instance][i].IsFinalized);
                        Assert.Equal(i == 1, quests[instance][i].IsOngoing);
                        if (!removePlayer || i == 1)
                        {
                            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyIds[i], out var party));
                            Assert.Equal(i == 0 ? 0 : 5, party.ItemRoster.GetItemNumber(item));
                        }
                        Assert.Equal(i == 0, giver.Issue == null);
                        Assert.Equal(i == 1, instance.Resolve<IIssueOwnershipRegistry>().TryGetOwnerControllerId(giver, out _));
                        Assert.Equal(i == 0 ? 2 : 1, quests[instance][i].JournalEntries.Count);
                    }
                });
            for (var i = 0; i < clients.Length; i++)
            {
                var index = i;
                var client = clients[i];
                client.Call(() =>
                {
                    using var scope = removePlayer && index == 0 ? null : new MainHeroSubstitutionScope(
                        client.GetRegisteredObject<MobileParty>(partyIds[index]).LeaderHero, client.GetRegisteredObject<MobileParty>(partyIds[index]));
                    var journal = new QuestsVM(() => { });
                    if (index == 0)
                    {
                        Assert.Empty(journal.ActiveQuestsList);
                        Assert.True(Assert.Single(journal.OldQuestsList).QuestLogEntry.IsRelatedTo(quests[client][0]));
                    }
                    else
                    {
                        Assert.Same(quests[client][1], Assert.Single(journal.ActiveQuestsList).Quest);
                        Assert.Empty(journal.OldQuestsList);
                    }
                    journal.OnFinalize();
                });
            }
        });

    [Fact]
    public void SharedDestinationTrackingEndsWhenEachLocalOwnersQuestEnds()
    {
        using var environment = new E2ETestEnvironment(output);
        var destinationId = environment.CreateRegisteredObject<Settlement>();
        var index = 0;
        foreach (var client in environment.Clients)
        {
            var localOwner = $"owner-{index++}";
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Settlement>(destinationId, out var destination));
                Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
                Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
                client.Resolve<IControllerIdProvider>().SetControllerId(localOwner);
                var ownership = client.Resolve<IHeadmanHerdPersonalOwnership>();
                var owned = new HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest(
                    $"herd-{localOwner}", null, CampaignTime.Never, 1, null, destination, 1, null);
                var other = new HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest(
                    $"herd-other-{localOwner}", null, CampaignTime.Never, 1, null, destination, 1, null);
                ownership.SetOwner(owned.StringId, localOwner);
                ownership.SetOwner(other.StringId, "another-owner");
                var tracker = Campaign.Current.VisualTrackerManager;
                var quests = Campaign.Current.QuestManager;

                owned.AddTrackedObject(destination);
                other.AddTrackedObject(destination);
                Assert.True(tracker.CheckTracked(destination));
                Assert.Equal(1, tracker._trackedObjects[destination].TrackerCount);
                other.ToggleTrackedObjects();
                Assert.True(other.IsTrackEnabled);
                Assert.Equal(1, tracker._trackedObjects[destination].TrackerCount);
                owned.ToggleTrackedObjects();
                Assert.False(tracker.CheckTracked(destination));
                owned.ToggleTrackedObjects();
                Assert.Equal(1, tracker._trackedObjects[destination].TrackerCount);
                other.RemoveTrackedObject(destination);
                Assert.True(tracker.CheckTracked(destination));
                other.AddTrackedObject(destination);
                owned.RemoveTrackedObject(destination);

                Assert.False(tracker.CheckTracked(destination));
                Assert.Same(other, Assert.Single(quests.TrackedObjects[destination]));
                tracker.RegisterObject(destination);
                other.RemoveTrackedObject(destination);
                Assert.True(tracker.CheckTracked(destination));
                Assert.DoesNotContain(destination, quests.TrackedObjects.Keys);
            });
        }
    }

    [Fact]
    public void TogglingTheOwnedQuestPreservesASeparatelyTrackedGiver()
    {
        using var environment = new E2ETestEnvironment(output, numClients: 1);
        var client = environment.Clients.Single();
        var giverId = environment.CreateRegisteredObject<Hero>();
        var destinationId = environment.CreateRegisteredObject<Settlement>();
        client.Call(() =>
        {
            var giver = client.GetRegisteredObject<Hero>(giverId);
            var destination = client.GetRegisteredObject<Settlement>(destinationId);
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            client.Resolve<IControllerIdProvider>().SetControllerId("marker-owner");
            var quest = new HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest(
                "herd-manual-giver", giver, CampaignTime.Never, 1, null, destination, 1, null);
            client.Resolve<IHeadmanHerdPersonalOwnership>().SetOwner(quest.StringId, "marker-owner");
            quest.StartQuest();
            quest.AddTrackedObject(destination);
            var tracker = Campaign.Current.VisualTrackerManager;
            tracker.RegisterObject(giver);

            quest.ToggleTrackedObjects();

            Assert.True(tracker.CheckTracked(giver));
            Assert.Equal(1, tracker._trackedObjects[giver].TrackerCount);
            Assert.False(tracker.CheckTracked(destination));
            quest.ToggleTrackedObjects();
            Campaign.Current.QuestManager.RemoveAllTrackedObjectsForQuest(quest);
            Assert.True(tracker.CheckTracked(giver));
            Assert.Equal(1, tracker._trackedObjects[giver].TrackerCount);
        });
    }

    [Theory]
    [InlineData("active")]
    [InlineData("removed-cancel")]
    [InlineData("removed-accept")]
    [InlineData("replacement-cancel")]
    [InlineData("replacement-accept")]
    [InlineData("generation-accept")]
    [InlineData("removed-before-done")]
    [InlineData("accepted-before-done")]
    [InlineData("accepted-before-cancel")]
    [InlineData("accepted-before-reset-saved")]
    public void CompanionPickerCommitsWagesWithoutTransferringTroopsBeforeAcceptance(string ending)
    {
        using var environment = new E2ETestEnvironment(output, numClients: 1);
        var server = environment.Server;
        var client = environment.Clients.Single();
        var giverId = environment.CreateRegisteredObject<Hero>();
        var companionId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        string heroId = null!;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var hero = party.LeaderHero;
            hero.PartyBelongedTo = party;
            hero.Gold = 1000;
            Assert.True(server.ObjectManager.TryGetId(hero, out heroId));
            party.MemberRoster.AddToCounts(companion.CharacterObject, 1);
            party.MemberRoster.AddToCounts(troop, 10);
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("picker-owner", heroId, partyId, "", "")));
        });
        environment.ConnectRegisteredPlayer(client, "picker-owner");
        environment.FlushCoalescer();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var issues = client.Resolve<IHeadmanNeedsToDeliverAHerdIssueInterface>();
            var issue = issues.ConstructReplicated(giver, null, giver, null);
            issues.RegisterReplicated(giver, issue, "picker-herd", CampaignTime.Now, CampaignTime.DaysFromNow(30));
            var agent = new Mock<IAgent>();
            agent.SetupGet(value => value.Character).Returns(giver.CharacterObject);
            var conversation = Campaign.Current.ConversationManager;
            conversation._conversationAgents.Clear();
            conversation._conversationAgents.Add(agent.Object);
            conversation._lastSelectedDialogObject = companion;
            using var playerScope = new MainHeroSubstitutionScope(hero, party);
            Assert.Same(issue, GenericQuestTypeAlternativePickerPatch.CurrentIssue());
            IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence();
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            Assert.Equal(1, issue.AlternativeSolutionSentTroops.GetTroopCount(companion.CharacterObject));

            var logic = new PartyScreenLogic();
            var left = issue.AlternativeSolutionSentTroops;
            var leftPrisoners = TroopRoster.CreateDummyTroopRoster();
            logic.MemberRosters[0] = left;
            logic.PrisonerRosters[0] = leftPrisoners;
            logic.MemberRosters[1] = party.MemberRoster;
            logic.PrisonerRosters[1] = party.PrisonRoster;
            logic.RightOwnerParty = party.Party;
            logic._partyScreenMode = Helpers.PartyScreenHelper.PartyScreenMode.QuestTroopManage;
            logic.CurrentData.BindRostersFrom(party.MemberRoster, party.PrisonRoster, left, leftPrisoners, party.Party, null);
            logic._initialData.InitializeCopyFrom(party.Party, null);
            logic._initialData.CopyFromPartyAndRoster(party.MemberRoster, party.PrisonRoster, left, leftPrisoners, party.Party);
            logic.PartyPresentationDoneButtonDelegate = (_, _, _, _, _, _, _, _, _) => true;
            if (ending == "accepted-before-reset-saved") logic.SavePartyScreenData();
            using (new AllowedThread())
            {
                party.MemberRoster.AddToCounts(troop, -6);
                left.AddToCounts(troop, 6);
                logic.SetPartyGoldChangeAmount(-120);
            }
            void RemoveIssue()
                => client.SimulateMessage(server.NetPeer, new NetworkIssueRemoved(giverId, IssueFinalizeReason.IssueOnly));

            if (ending.StartsWith("accepted-before-"))
            {
                using var setup = new AllowedThread();
                issue._issueState = IssueBase.IssueState.SolvingWithAlternativeSolution;
                issue.AlternativeSolutionSentTroops.Clear();
                issue.AlternativeSolutionSentTroops.AddToCounts(companion.CharacterObject, 1);
                issue.AlternativeSolutionSentTroops.AddToCounts(troop, 30);
            }
            if (ending == "removed-before-done") RemoveIssue();
            if (ending == "accepted-before-cancel")
                logic.Reset(true);
            else if (ending == "accepted-before-reset-saved")
                logic.ResetToLastSavedPartyScreenData(true);
            else
                Assert.True(logic.DoneLogic(false));
            if (ending.StartsWith("accepted-before-"))
                Assert.Equal(30, issue.AlternativeSolutionSentTroops.GetTroopCount(troop));
            if (ending == "removed-before-done" || ending.StartsWith("accepted-before-"))
                Assert.Empty(client.NetworkSentMessages.GetMessages<NetworkCompleteDoneLogic>());
            else
                Assert.Single(client.NetworkSentMessages.GetMessages<NetworkCompleteDoneLogic>());
            Assert.Equal(10, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            if ((ending.StartsWith("removed-") && ending != "removed-before-done") || ending.StartsWith("replacement-"))
                RemoveIssue();
            IssueBase replacement = null!;
            if (ending.StartsWith("replacement-"))
            {
                replacement = issues.ConstructReplicated(giver, null, giver, null);
                issues.RegisterReplicated(giver, (HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue)replacement,
                    "replacement-picker-herd", CampaignTime.Now, CampaignTime.DaysFromNow(30));
                replacement.AlternativeSolutionSentTroops.AddToCounts(troop, 2);
            }
            if (ending == "generation-accept")
                client.Resolve<IIssueGenerationRegistry>().Bump(giver);
            if (ending != "active")
            {
                Assert.False(new IssuesCampaignBehavior().issue_offer_player_accept_alternative_5_a_condition());
                Assert.False(IssuesCampaignBehavior.DoTroopsSatisfyAlternativeSolutionInternal(left, out var explanation));
                Assert.NotNull(explanation);
                new IssuesCampaignBehavior().issue_offer_player_accept_alternative_4_consequence();
                IssuesCampaignBehavior.PartyScreenDoneClicked(null, left, leftPrisoners, party.Party,
                    party.MemberRoster, party.PrisonRoster, false);
            }
            if (ending.EndsWith("-accept"))
                IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_consequence();
            else
                IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence();
            Assert.Empty(client.InternalMessages.GetMessages<QuestTypeAlternativeAcceptTriggered>());
            Assert.Equal(10, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
            if (replacement != null) Assert.Equal(2, replacement.AlternativeSolutionSentTroops.GetTroopCount(troop));
            if (ending == "active") Assert.Equal(0, issue.AlternativeSolutionSentTroops.TotalManCount);
            if (ending == "replacement-cancel")
            {
                conversation.BeginConversation();
                Assert.Same(replacement, GenericQuestTypeAlternativePickerPatch.CurrentIssue());
                Assert.True(GenericQuestTypeAlternativePickerPatch.IsCurrentSelection(replacement));
            }
        });
        environment.FlushCoalescer();
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.Equal(ending == "removed-before-done" || ending.StartsWith("accepted-before-") ? 1000 : 880, hero.Gold);
            Assert.Equal(10, party.MemberRoster.GetTroopCount(troop));
            Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleCompanionDialogDoesNotDereferenceOrQueueAContinuation(bool rebuildOptions)
    {
        using var environment = new E2ETestEnvironment(output, numClients: 1);
        var client = environment.Clients.Single();
        var giverId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        client.Call(() =>
        {
            Campaign.Current.EncyclopediaManager ??= new EncyclopediaManager();
            Campaign.Current.EncyclopediaManager.CreateEncyclopediaPages();
            var giver = client.GetRegisteredObject<Hero>(giverId);
            var party = client.GetRegisteredObject<MobileParty>(partyId);
            using var scope = new MainHeroSubstitutionScope(party.LeaderHero, party);
            var factory = client.Resolve<IHeadmanNeedsToDeliverAHerdIssueInterface>();
            var issue = factory.ConstructReplicated(giver, null, giver, null);
            factory.RegisterReplicated(giver, issue, "stale-dialog-herd", CampaignTime.Now, CampaignTime.DaysFromNow(30));
            var agent = new Mock<IAgent>();
            agent.SetupGet(value => value.Character).Returns(giver.CharacterObject);
            var mainAgent = new Mock<IAgent>();
            mainAgent.SetupGet(value => value.Character).Returns(party.LeaderHero.CharacterObject);
            var conversation = Campaign.Current.ConversationManager;
            conversation._conversationAgents.Add(agent.Object);
            conversation._mainAgent = mainAgent.Object;
            conversation.AddDialogFlow(DialogFlow.CreateDialogFlow("stale_herd_picker")
                .PlayerLine(new TextObject("Select companion"))
                .Consequence(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence)
                .NpcLine(new TextObject("Select troops"))
                .CloseDialog(), this);
            var sentenceIndex = conversation._sentences.FindIndex(sentence => sentence.RelatedObject == this && sentence.IsPlayer);
            Assert.True(sentenceIndex >= 0);
            conversation.CurOptions = new List<ConversationSentenceOption>
            {
                new ConversationSentenceOption { SentenceNo = sentenceIndex, RepeatObject = giver },
            };
            conversation.BeginConversation();
            conversation.OnConversationActivate();
            client.SimulateMessage(environment.Server.NetPeer, new NetworkIssueRemoved(giverId, IssueFinalizeReason.IssueOnly));
            Assert.Null(giver.Issue);
            if (rebuildOptions)
            {
                Assert.False(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_condition());
            }
            else
            {
                conversation.DoOption(0);
                Assert.False(conversation._executeDoOptionContinue);
                Assert.False(conversation.IsConversationInProgress);
                Assert.Empty(client.InternalMessages.GetMessages<QuestTypeAlternativeAcceptTriggered>());
            }
        });
    }

    [Fact]
    public void CompanionReturnUsesTheServerRosterOnceAndReplicatesToBothClients()
    {
        using var environment = new E2ETestEnvironment(output);
        var server = environment.Server;
        var client = environment.Clients.First();
        var heroId = environment.CreateRegisteredObject<Hero>();
        var companionId = environment.CreateRegisteredObject<Hero>();
        var partyId = environment.CreateRegisteredObject<MobileParty>();
        var otherPartyId = environment.CreateRegisteredObject<MobileParty>();
        var troopId = environment.CreateRegisteredObject<CharacterObject>();
        var upgradeId = environment.CreateRegisteredObject<CharacterObject>();
        foreach (var instance in environment.Clients.Append(server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(upgradeId, out var upgrade));
                using (new AllowedThread())
                {
                    companion.ChangeState(Hero.CharacterStates.Disabled);
                    troop.Level = 6;
                    upgrade.Level = 11;
                    troop.UpgradeTargets = new[] { upgrade };
                }
            });
        }
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
            Assert.True(server.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var otherParty));
            Assert.True(server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("return-owner", heroId, partyId, "", "")));
            var pending = TroopRoster.CreateDummyTroopRoster();
            pending.AddToCounts(companion.CharacterObject, 1);
            pending.AddToCounts(troop, 6, false, 2, 120);
            Assert.Equal(120, pending.GetElementCopyAtIndex(pending.FindIndexOfTroop(troop)).Xp);
            server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit("return-owner", pending);
            server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().Deposit("other-owner", pending);
            otherParty.MemberRoster.AddToCounts(troop, 3);
        });
        environment.ConnectRegisteredPlayer(client, "return-owner");
        client.Resolve<IControllerIdProvider>().SetControllerId("return-owner");

        // An outdated local roster returns the authoritative pool; repeats cannot duplicate it.
        var changeState = AccessTools.Method(typeof(Hero), nameof(Hero.ChangeState));
        var fixturePrefix = Harmony.GetPatchInfo(changeState).Prefixes.Single(patch => patch.owner == "Coop.Testing");
        var testing = new Harmony(fixturePrefix.owner);
        testing.Unpatch(changeState, fixturePrefix.PatchMethod);
        try
        {
            server.SimulateMessage(client.NetPeer, new RequestAwaitingAlternativeSolutionTroopsDrain(default));
            server.SimulateMessage(client.NetPeer, new RequestAwaitingAlternativeSolutionTroopsDrain(default));
            environment.FlushCoalescer();
        }
        finally
        {
            testing.Patch(changeState, prefix: new HarmonyMethod(fixturePrefix.PatchMethod));
        }
        foreach (var instance in environment.Clients.Append(server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Hero>(companionId, out var companion));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(otherPartyId, out var otherParty));
                Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
                Assert.True(companion.IsActive);
                Assert.Equal(1, party.MemberRoster.GetTroopCount(companion.CharacterObject));
                var returned = party.MemberRoster.GetElementCopyAtIndex(party.MemberRoster.FindIndexOfTroop(troop));
                output.WriteLine($"Return instance server={instance == server} owner={instance == client}: count={returned.Number}, wounded={returned.WoundedNumber}, xp={returned.Xp}");
                Assert.Equal(6, returned.Number);
                Assert.Equal(2, returned.WoundedNumber);
                // Troop XP is private to the owning player; observers receive counts and wounds.
                Assert.Equal(instance == server || instance == client ? 120 : 0, returned.Xp);
                Assert.Equal(3, otherParty.MemberRoster.GetTroopCount(troop));
            });
        }
        server.Call(() =>
        {
            Assert.False(server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet("return-owner", out _));
            Assert.True(server.Resolve<IAwaitingAlternativeSolutionTroopsRegistry>().TryGet("other-owner", out _));
        });
    }

    [Fact]
    public void ReceivedIssueKeepsBaseInitializationWithoutRunningPlayerDependentGeneration()
    {
        using var environment = new E2ETestEnvironment(output);
        var client = environment.Clients.First();
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var targetId = environment.CreateRegisteredObject<Hero>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
            Assert.True(client.ObjectManager.TryGetObject<Hero>(targetId, out var target));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            Assert.True(client.ObjectManager.TryGetObject<ItemObject>(itemId, out var item));
            var previousParty = Campaign.Current.MainParty;
            using var allowed = new AllowedThread();
            try
            {
                Campaign.Current.MainParty = null;
                var issue = client.Resolve<IHeadmanNeedsToDeliverAHerdIssueInterface>()
                    .ConstructReplicated(owner, settlement, target, item);

                Assert.Same(owner, issue.IssueOwner);
                Assert.Same(settlement, issue._targetSettlement);
                Assert.Same(target, issue._targetHero);
                Assert.Same(item, issue._herdTypeToDeliver);
                Assert.True(issue.IsOngoingWithoutQuest);
                Assert.NotNull(issue.AlternativeSolutionSentTroops);
                Assert.False(HeadmanHerdIssueReplicaScope.IsActive);
            }
            finally
            {
                Campaign.Current.MainParty = previousParty;
            }
        });
    }
    [Fact]
    public void CreationMessageKeepsTheServerIdentityAndDeadlineOnBothClients()
    {
        using var environment = new E2ETestEnvironment(output);
        var ownerId = environment.CreateRegisteredObject<Hero>();
        var targetId = environment.CreateRegisteredObject<Hero>();
        var settlementId = environment.CreateRegisteredObject<Settlement>();
        var itemId = environment.CreateRegisteredObject<ItemObject>();
        var message = new NetworkHeadmanNeedsToDeliverAHerdIssueCreated(ownerId, settlementId,
            targetId, itemId, 3, "issue_4242", 12345, 987654);

        foreach (var client in environment.Clients)
        {
            client.SimulateMessage(environment.Server.NetPeer, message);
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Hero>(ownerId, out var owner));
                Assert.NotNull(owner.Issue);
                Assert.Equal("issue_4242", owner.Issue.StringId);
                Assert.Equal(12345, owner.Issue.IssueCreationTime.NumTicks);
                Assert.Equal(987654, owner.Issue.IssueDueTime.NumTicks);
                Assert.True(client.Resolve<IIssueGenerationRegistry>().TryGetGeneration(owner, out var generation));
                Assert.Equal(3, generation);
            });
        }
    }

    [Fact]
    public void UnacceptedExpiryCleansUpWithoutAServerPlayerHero()
        => ExerciseUnacceptedCompletion(false);

    [Fact]
    public void UnacceptedAiResolutionCleansUpWithoutAServerPlayerHero()
        => ExerciseUnacceptedCompletion(true);

    private void ExerciseUnacceptedCompletion(bool aiResolution)
        => WithHerdEnabled(() =>
        {
            using var environment = new E2ETestEnvironment(output);
            var server = environment.Server;
            server.Resolve<GameInterface.IGameInterface>().PatchGameStarted();
            var giverId = environment.CreateRegisteredObject<Hero>();
            var targetId = environment.CreateRegisteredObject<Hero>();
            var settlementId = environment.CreateRegisteredObject<Settlement>();
            var itemId = environment.CreateRegisteredObject<ItemObject>();
            foreach (var instance in environment.Clients.Prepend(server))
                instance.Call(() =>
                {
                    new JournalLogsCampaignBehavior().RegisterEvents();
                    new IssuesCampaignBehavior().RegisterEvents();
                    var factory = instance.Resolve<IHeadmanNeedsToDeliverAHerdIssueInterface>();
                    var giver = instance.GetRegisteredObject<Hero>(giverId);
                    var issue = factory.ConstructReplicated(giver, instance.GetRegisteredObject<Settlement>(settlementId),
                        instance.GetRegisteredObject<Hero>(targetId), instance.GetRegisteredObject<ItemObject>(itemId));
                    factory.RegisterReplicated(giver, issue, "unaccepted-expiry-herd", CampaignTime.Now, CampaignTime.Now - CampaignTime.Days(1));
                    instance.Resolve<IIssueGenerationRegistry>().SetGeneration(giver, 1);
                });
            server.Call(() =>
            {
                var previousHero = Game.Current.PlayerTroop;
                var previousParty = Campaign.Current.MainParty;
                try
                {
                    Game.Current.PlayerTroop = null;
                    Campaign.Current.MainParty = null;
                    var issue = server.GetRegisteredObject<Hero>(giverId).Issue;
                    if (aiResolution)
                        issue.CompleteIssueWithAiLord(server.GetRegisteredObject<Hero>(targetId));
                    else
                        issue.CompleteIssueWithTimedOut();
                }
                finally
                {
                    Game.Current.PlayerTroop = previousHero;
                    Campaign.Current.MainParty = previousParty;
                }
            });
            environment.FlushCoalescer();
            foreach (var instance in environment.Clients.Prepend(server))
                instance.Call(() =>
                {
                    Assert.Null(instance.GetRegisteredObject<Hero>(giverId).Issue);
                    Assert.Empty(Campaign.Current.LogEntryHistory.GetGameActionLogs<JournalLogEntry>(entry => true));
                    Assert.Empty(instance.Resolve<IHeadmanHerdPersonalOwnership>().Snapshot());
                });
        });

    [Fact]
    public void ServerGenerationRunsTheNativeFactoryInTheConnectedPlayersContextAndReplicatesTheResult()
        => WithHerdEnabled(() =>
        {
            using var environment = new E2ETestEnvironment(output);
            var server = environment.Server;
            var client = environment.Clients.First();
            var giverId = environment.CreateRegisteredObject<Hero>();
            var targetId = environment.CreateRegisteredObject<Hero>();
            var villageId = environment.CreateRegisteredObject<Village>();
            var settlementId = environment.CreateRegisteredObject<Settlement>();
            var destinationId = environment.CreateRegisteredObject<Settlement>();
            var partyId = environment.CreateRegisteredObject<MobileParty>();
            foreach (var itemName in new[] { "sheep", "cow", "hog" })
            {
                uint handle = 0;
                foreach (var instance in environment.Clients.Prepend(server))
                    instance.Call(() =>
                    {
                        using var setup = new AllowedThread();
                        var item = Campaign.Current.ObjectManager.GetObject<ItemObject>(itemName);
                        if (item == null)
                        {
                            item = new ItemObject(itemName);
                            Campaign.Current.ObjectManager.RegisterPresumedObject(item);
                        }
                        if (instance == server)
                        {
                            Assert.True(instance.ObjectManager.AddExisting(itemName, item));
                            Assert.True(instance.ObjectManager.TryGetHandle(item, out handle));
                        }
                        else Assert.True(instance.ObjectManager.AddExisting(itemName, item, handle));
                    });
            }
            string playerHeroId = null!;
            server.Call(() =>
            {
                using var setup = new AllowedThread();
                var giver = server.GetRegisteredObject<Hero>(giverId);
                var target = server.GetRegisteredObject<Hero>(targetId);
                var village = server.GetRegisteredObject<Village>(villageId);
                var settlement = server.GetRegisteredObject<Settlement>(settlementId);
                var destination = server.GetRegisteredObject<Settlement>(destinationId);
                var party = server.GetRegisteredObject<MobileParty>(partyId);
                settlement.SetSettlementComponent(village);
                settlement._position = new CampaignVec2(Vec2.Zero, true);
                party._position = new CampaignVec2(new Vec2(1, 0), true);
                village.Bound = destination;
                destination._notablesCache.Add(target);
                target.ChangeState(Hero.CharacterStates.Disabled);
                giver.StayingInSettlement = settlement;
                giver.Occupation = Occupation.RuralNotable;
                party.IsActive = true;
                party.LeaderHero.ChangeState(Hero.CharacterStates.Active);
                Assert.True(server.ObjectManager.TryGetId(party.LeaderHero, out playerHeroId));
                Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("generation-player", playerHeroId, partyId, "", "")));
            });
            environment.ConnectRegisteredPlayer(client, "generation-player");
            string issueId = null!, animalId = null!;
            long created = 0, due = 0;
            server.Call(() =>
            {
                var previousHero = Game.Current.PlayerTroop;
                var previousParty = Campaign.Current.MainParty;
                var previousResolved = ResolvedMainHeroContext.ResolvedMainHero;
                try
                {
                    Game.Current.PlayerTroop = null;
                    Campaign.Current.MainParty = null;
                    var behavior = new HeadmanNeedsToDeliverAHerdIssueBehavior();
                    IssueBase Factory(in PotentialIssueData data, Hero owner)
                    {
                        Assert.Same(server.GetRegisteredObject<Hero>(playerHeroId), Hero.MainHero);
                        Assert.Same(server.GetRegisteredObject<MobileParty>(partyId), MobileParty.MainParty);
                        return behavior.OnSelected(in data, owner);
                    }
                    var data = new PotentialIssueData(Factory,
                        typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue), IssueBase.IssueFrequency.VeryCommon);
                    var giver = server.GetRegisteredObject<Hero>(giverId);
                    Assert.True(Campaign.Current.IssueManager.CreateNewIssue(in data, giver));
                    Assert.Null(Game.Current.PlayerTroop);
                    Assert.Null(Campaign.Current.MainParty);
                    Assert.Same(previousResolved, ResolvedMainHeroContext.ResolvedMainHero);
                    var issue = Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue>(giver.Issue);
                    Assert.Same(server.GetRegisteredObject<Settlement>(destinationId), issue._targetSettlement);
                    Assert.Same(server.GetRegisteredObject<Hero>(targetId), issue._targetHero);
                    Assert.Contains(issue._herdTypeToDeliver.StringId, new[] { "sheep", "cow", "hog" });
                    issueId = issue.StringId;
                    animalId = issue._herdTypeToDeliver.StringId;
                    created = issue.IssueCreationTime.NumTicks;
                    due = issue.IssueDueTime.NumTicks;
                    Assert.Empty(server.Resolve<IHeadmanHerdPersonalOwnership>().Snapshot());
                }
                finally
                {
                    Game.Current.PlayerTroop = previousHero;
                    Campaign.Current.MainParty = previousParty;
                }
            });
            environment.FlushCoalescer();
            foreach (var peer in environment.Clients)
                peer.Call(() =>
                {
                    var issue = Assert.IsType<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue>(peer.GetRegisteredObject<Hero>(giverId).Issue);
                    Assert.Equal(issueId, issue.StringId);
                    Assert.Equal(animalId, issue._herdTypeToDeliver.StringId);
                    Assert.Equal(created, issue.IssueCreationTime.NumTicks);
                    Assert.Equal(due, issue.IssueDueTime.NumTicks);
                    Assert.Same(peer.GetRegisteredObject<Settlement>(destinationId), issue._targetSettlement);
                    Assert.Same(peer.GetRegisteredObject<Hero>(targetId), issue._targetHero);
                    Assert.Empty(peer.Resolve<IHeadmanHerdPersonalOwnership>().Snapshot());
                });
        });

    [Fact]
    public void ServerDefersHerdCreationBeforeCallingTheFactoryWhenNoPlayerIsAvailable()
    {
        using var environment = new E2ETestEnvironment(output);
        var giverId = environment.CreateRegisteredObject<Hero>();
        var server = environment.Server;
        server.Call(() =>
        {
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverId, out var giver));
            var factoryCalled = false;
            IssueBase Factory(in PotentialIssueData _, Hero owner)
            {
                factoryCalled = true;
                throw new InvalidOperationException("generation must wait for a real player");
            }
            var data = new PotentialIssueData(Factory,
                typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue), IssueBase.IssueFrequency.Common);

            Assert.False(Campaign.Current.IssueManager.CreateNewIssue(in data, giver));
            Assert.False(factoryCalled);
            Assert.Null(giver.Issue);
        });
    }

    [Fact]
    public void EligibilityProjectionUsesActualPlayerContextWithoutFilteringTheWorldIssueCollection()
    {
        using var environment = new E2ETestEnvironment(output);
        var playerId = environment.CreateRegisteredObject<Hero>();
        var otherPlayerId = environment.CreateRegisteredObject<Hero>();
        var giverAId = environment.CreateRegisteredObject<Hero>();
        var giverBId = environment.CreateRegisteredObject<Hero>();
        environment.Server.Call(() =>
        {
            var server = environment.Server;
            Assert.True(server.ObjectManager.TryGetObject<Hero>(playerId, out var player));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(otherPlayerId, out var otherPlayer));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverAId, out var giverA));
            Assert.True(server.ObjectManager.TryGetObject<Hero>(giverBId, out var giverB));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("A", playerId, "", "", "")));
            Assert.True(server.Resolve<IPlayerManager>().AddPlayer(new Player("B", otherPlayerId, "", "", "")));
            var issueA = ObjectHelper.SkipConstructor<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue>();
            var issueB = ObjectHelper.SkipConstructor<HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue>();
            using (new AllowedThread())
            {
                issueA.StringId = "owned-A";
                issueB.StringId = "owned-B";
            }
            var ownership = server.Resolve<IHeadmanHerdPersonalOwnership>();
            ownership.SetOwner(issueA.StringId, "A");
            ownership.SetOwner(issueB.StringId, "B");
            var issues = new MBReadOnlyDictionary<Hero, IssueBase>(new Dictionary<Hero, IssueBase>
            {
                [giverA] = issueA, [giverB] = issueB,
            });
            using (new MainHeroSubstitutionScope(player, ObjectHelper.SkipConstructor<MobileParty>()))
                Assert.Same(issueA, Assert.Single(HeadmanHerdEligibilityPatch.FilterIssues(issues, issueA)).Value);
            using (new MainHeroSubstitutionScope(otherPlayer, ObjectHelper.SkipConstructor<MobileParty>()))
                Assert.Same(issueB, Assert.Single(HeadmanHerdEligibilityPatch.FilterIssues(issues, issueA)).Value);
            Assert.Equal(2, issues.Count);
        });
    }

}
