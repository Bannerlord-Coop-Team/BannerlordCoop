using Common;
using Common.Util;
using Coop.Core.Client.Services.Kingdoms.Handlers;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Kingdoms.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Stances.Messages;
using E2E.Tests.Environment;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Commands;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Patches;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Services.Stances.Messages;
using GameInterface.Services.UI.Notifications.Messages;
using HarmonyLib;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Policies;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Kingdoms;

public class PlayerKingdomCreationFlowTests : IDisposable
{
    private const string ControllerId = "Player";
    private const string SecondControllerId = "Player2";
    private const string KingdomName = "Real Kingdom";

    private E2ETestEnvironment TestEnvironment { get; }
    private EnvironmentInstance Server => TestEnvironment.Server;
    private IEnumerable<EnvironmentInstance> Clients => TestEnvironment.Clients;
    private static IKingdomDecisionVoteManager GetVoteManager(EnvironmentInstance instance) =>
        instance.Resolve<IKingdomDecisionVoteManager>();
    private static KingdomDecisionVoteManager GetConcreteVoteManager(EnvironmentInstance instance) =>
        instance.Resolve<KingdomDecisionVoteManager>();

    public PlayerKingdomCreationFlowTests(ITestOutputHelper output)
    {
        TestEnvironment = new E2ETestEnvironment(output);
    }

    public void Dispose()
    {
        TestEnvironment.Dispose();
    }

    [Fact]
    public void ClientGovernorFinalization_CreatesPlayerKingdomOnServerAndSyncsClients()
    {
        var player = CreateSyncedPlayerContext();
        var fiefId = CreateSyncedClanFief(player.ClanId);
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            var behavior = ObjectHelper.SkipConstructor<GovernorCampaignBehavior>();
            Assert.True(client.ObjectManager.TryGetObject<CultureObject>(player.CultureId, out var culture));

            AccessTools.Field(typeof(GovernorCampaignBehavior), "_kingdomCreationChosenName")
                .SetValue(behavior, new TextObject(KingdomName));
            AccessTools.Field(typeof(GovernorCampaignBehavior), "_kingdomCreationChosenCulture")
                .SetValue(behavior, culture);

            bool runOriginal = GovernorKingdomCreationPatches.FinalizationPrefix(behavior);

            Assert.False(runOriginal);
        });

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateKingdom>());
        Assert.Equal(ControllerId, request.ControllerId);
        Assert.Equal(KingdomName, request.KingdomName);
        Assert.False(string.IsNullOrWhiteSpace(request.CultureId));

        var created = Assert.Single(Server.InternalMessages.GetMessages<PlayerKingdomCreated>());
        Assert.Equal(ControllerId, created.ControllerId);
        Assert.Equal(KingdomName, created.KingdomName);
        Assert.Equal(player.ClanId, created.ClanId);
        Assert.Equal(request.CultureId, created.CultureId);
        Assert.False(string.IsNullOrWhiteSpace(created.KingdomId));

        AssertKingdomCreatedOnServer(created.KingdomId, player.ClanId, created.CultureId, fiefId);
        foreach (var environmentClient in Clients)
        {
            AssertKingdomSyncedToClient(environmentClient, created.KingdomId, player.ClanId, created.CultureId, fiefId);
            Assert.Contains(
                environmentClient.InternalMessages.GetMessages<PlayerKingdomCreated>(),
                message => message.KingdomId == created.KingdomId
                           && message.KingdomName == KingdomName
                           && message.ClanId == player.ClanId);
        }

        var notification = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkPlayerKingdomCreated>());
        Assert.Equal(created.ControllerId, notification.ControllerId);
        Assert.Equal(created.KingdomId, notification.KingdomId);
        Assert.Equal(created.KingdomName, notification.KingdomName);
        Assert.Equal(created.ClanId, notification.ClanId);
        Assert.Equal(created.CultureId, notification.CultureId);
    }

    [Fact]
    public void ForcePlayerJoinKingdom_UpdatesKingdomManagementCollections()
    {
        var player = CreateSyncedPlayerContext();
        var fiefId = CreateSyncedClanFief(player.ClanId);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var foundingClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        ConfigureClanInKingdom(foundingClanId, kingdomId);

        Server.Call(() =>
        {
            var result = KingdomDebugCommand.ForcePlayerJoinKingdom(new List<string> { ControllerId, kingdomId });

            Assert.Contains("Forced player", result);
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(foundingClanId, out var foundingClan));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

            Assert.Same(kingdom, clan.Kingdom);
            Assert.Same(kingdom, foundingClan.Kingdom);
            Assert.Contains(clan, kingdom.Clans);
            Assert.Contains(foundingClan, kingdom.Clans);
            Assert.Equal(2, kingdom.Clans.Count);
            Assert.Contains(fief, kingdom.Fiefs);
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(foundingClanId, out var foundingClan));

                Assert.Same(kingdom, clan.Kingdom);
                Assert.Same(kingdom, foundingClan.Kingdom);
                Assert.Contains(clan, kingdom.Clans);
                Assert.Contains(foundingClan, kingdom.Clans);
                Assert.Equal(2, kingdom.Clans.Count);
                AssertKingdomContainsFief(client.ObjectManager, kingdom, fiefId);
            });
        }
    }

    [Fact]
    public void VassalServiceAccepted_JoinsPlayerClanAuthoritativelyAndRejectsReplay()
    {
        var client = Clients.First();
        var player = CreateSyncedPlayerContext(ControllerId, client);
        var ruler = CreateSyncedPlayerContext("VassalRuler", _ => false);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        ConfigureClanInKingdom(ruler.ClanId, kingdomId);
        SetClanTierEverywhere(player.ClanId, 2);

        Server.Call(() => Server.Resolve<IPlayerManager>().SetPeer(ControllerId, client.NetPeer));

        Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
        client.SimulateMessage(this, new VassalServiceAccepted(kingdom, grantRewards: false));

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<RequestVassalService>());
        Assert.Equal(kingdomId, request.KingdomId);
        Assert.False(request.GrantRewards);

        var accepted = Assert.Single(Server.NetworkSentMessages.GetMessages<VassalServiceResult>());
        Assert.True(accepted.Accepted);
        Assert.False(accepted.GrantRewards);

        Server.Call(() => AssertVassalMembership(Server, player.ClanId, kingdomId));
        foreach (var instance in Clients)
        {
            instance.Call(() => AssertVassalMembership(instance, player.ClanId, kingdomId));
        }

        Server.NetworkSentMessages.Clear();
        client.SimulateMessage(this, new VassalServiceAccepted(kingdom, grantRewards: false));

        var rejected = Assert.Single(Server.NetworkSentMessages.GetMessages<VassalServiceResult>());
        Assert.False(rejected.Accepted);
    }

    [Fact]
    public void ClanChangedFactionNotification_AllowsNullKingdomEndpoints()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
        Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
        Server.SimulateMessage(
            this,
            new NotifyClanChangedFaction(
                clan,
                oldKingdom: null,
                newKingdom: kingdom,
                detail: ChangeKingdomAction.ChangeKingdomActionDetail.JoinKingdom,
                showNotification: true));

        var factionChanged = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkNotifyClanChangedFaction>(),
            message => message.ClanId == player.ClanId);
        Assert.Null(factionChanged.OldKingdomId);
        Assert.Equal(kingdomId, factionChanged.NewKingdomId);

        Server.NetworkSentMessages.Clear();
        Server.SimulateMessage(
            this,
            new NotifyClanChangedFaction(
                clan,
                oldKingdom: kingdom,
                newKingdom: null,
                detail: ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom,
                showNotification: true));

        factionChanged = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkNotifyClanChangedFaction>(),
            message => message.ClanId == player.ClanId);
        Assert.Equal(kingdomId, factionChanged.OldKingdomId);
        Assert.Null(factionChanged.NewKingdomId);
    }

    [Fact]
    public void ForcePlayerJoinKingdom_MovesClanOutOfPreviousKingdomCollections()
    {
        var player = CreateSyncedPlayerContext();
        var fiefId = CreateSyncedClanFief(player.ClanId);
        var previousKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var newKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(previousKingdomId, out var previousKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

            using (new AllowedThread())
            {
                clan._kingdom = previousKingdom;
                previousKingdom._clans ??= new MBList<Clan>();
                previousKingdom._fiefsCache ??= new MBList<Town>();
                previousKingdom._townsCache ??= new MBList<Town>();

                previousKingdom._clans.Add(clan);
                previousKingdom._fiefsCache.Add(fief);
                previousKingdom._townsCache.Add(fief);
            }

            Assert.Same(previousKingdom, clan.Kingdom);
            Assert.Contains(clan, previousKingdom.Clans);
            Assert.Contains(fief, previousKingdom.Fiefs);
        });

        Server.Call(() =>
        {
            var result = KingdomDebugCommand.ForcePlayerJoinKingdom(new List<string> { ControllerId, newKingdomId });

            Assert.Contains("Forced player", result);
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(previousKingdomId, out var previousKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(newKingdomId, out var newKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

            Assert.Same(newKingdom, clan.Kingdom);
            Assert.DoesNotContain(clan, previousKingdom.Clans);
            Assert.DoesNotContain(fief, previousKingdom.Fiefs);
            Assert.Contains(clan, newKingdom.Clans);
            Assert.Contains(fief, newKingdom.Fiefs);
        });
    }

    [Fact]
    public void KingdomDecisionVotes_WaitForEveryPlayerClanBeforeResolvingDeclareWar()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdom", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            var decision = new DeclareWarDecision(proposerClan, targetKingdom);
            kingdom.AddDecision(decision);

            Assert.Single(kingdom.UnresolvedDecisions);
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.Single(kingdom.UnresolvedDecisions);
                Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
            });
        }

        var player1Preview = CreateDeclareWarVote(kingdomId, isFinal: false);
        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(player1Preview));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());

        var player1Final = CreateDeclareWarVote(kingdomId, isFinal: true);
        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(player1Final));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.Single(kingdom.UnresolvedDecisions);
        });

        var player2Preview = CreateDeclareWarVote(kingdomId, isFinal: false);
        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(player2Preview));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());

        var player2Final = CreateDeclareWarVote(kingdomId, isFinal: true);
        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(player2Final));

        var resolvedMessage = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId
                       && message.DecisionIndex == 0
                       && message.OutcomeIndex == 0
                       && message.IsPlayerDecision);
        Assert.Contains("war", resolvedMessage.NotificationText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(Server.InternalMessages.GetMessages<FactionWarDeclared>());
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>(),
            message => message.Faction1Id == kingdomId
                       && message.Faction2Id == targetKingdomId
                       && message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);

        foreach (var client in Clients)
        {
            Assert.Contains(
                client.InternalMessages.GetMessages<ApplyKingdomDecisionResolved>(),
                message => message.KingdomId == kingdomId
                           && message.DecisionIndex == 0
                           && message.OutcomeIndex == 0
                           && message.IsPlayerDecision
                           && message.NotificationText == resolvedMessage.NotificationText);
            Assert.Contains(
                client.InternalMessages.GetMessages<SendInformationMessage>(),
                message => message.Text == resolvedMessage.NotificationText);
            Assert.Contains(
                client.InternalMessages.GetMessages<DeclareWarChanged>(),
                message => message.Faction1Id == kingdomId
                           && message.Faction2Id == targetKingdomId
                           && message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);
        }
    }

    [Fact]
    public void AddDecision_UsesRegisteredPlayerClanWhenPartyClanPointsElsewhere()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var unrelatedClanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var unrelatedKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        ConfigureClanInKingdom(unrelatedClanId, unrelatedKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);
        EnsureKingdomRegisteredEverywhere(unrelatedKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var playerClan));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(unrelatedClanId, out var unrelatedClan));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var playerParty));

            using (new AllowedThread())
            {
                playerParty.ActualClan = unrelatedClan;
            }

            kingdom.AddDecision(new DeclareWarDecision(playerClan, targetKingdom));

            Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
    }

    [Fact]
    public void KingdomDecisionVotes_MissingVoteTimesOutAfterVotingWindow()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        DeclareWarDecision decision = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var playerClan));

            decision = new DeclareWarDecision(playerClan, targetKingdom);
            kingdom.AddDecision(decision);

            CoopKingdomDecisionProposalBehaviorPatch.HourlyTickPrefix();

            Assert.Same(decision, Assert.Single(kingdom.UnresolvedDecisions));
        });

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            decision.TriggerTime = CampaignTime.HoursFromNow(-1);
            CoopKingdomDecisionProposalBehaviorPatch.HourlyTickPrefix();

            Assert.Empty(kingdom.UnresolvedDecisions);
        });

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId && message.DecisionIndex == 0);
    }

    [Fact]
    public void KingdomDecisionVoteArrivingBeforeDecision_ReplaysWhenDecisionIsAdded()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomReplay", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        var player1NoVote = new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: 1,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: true);
        client2.SimulateMessage(this, new NetworkChangeKingdomDecisionVote(player1.ClanId, player1NoVote));
        Assert.Contains(
            client2.InternalMessages.GetMessages<ApplyKingdomDecisionVote>(),
            message => message.ClanId == player1.ClanId
                       && message.VoteData.OutcomeIndex == 1
                       && message.VoteData.IsFinal);
        client2.Call(() =>
        {
            var voteManager = GetConcreteVoteManager(client2);
            var pendingVotes = (System.Collections.ICollection)AccessTools
                .Field(typeof(KingdomDecisionVoteManager), "PendingRemoteVotes")
                .GetValue(voteManager);

            Assert.Single(pendingVotes);
        });

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client2.Call(() =>
        {
            var voteManager = GetConcreteVoteManager(client2);
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            voteManager.RegisterDecision(decision);
            var states = (System.Collections.IDictionary)AccessTools
                .Field(typeof(KingdomDecisionVoteManager), "DecisionStates")
                .GetValue(voteManager);

            Assert.True(states.Contains(decision));
            object state = states[decision];
            var votes = (System.Collections.IDictionary)AccessTools
                .Property(state.GetType(), "Votes")
                .GetValue(state);
            var pendingVotes = (System.Collections.ICollection)AccessTools
                .Field(typeof(KingdomDecisionVoteManager), "PendingRemoteVotes")
                .GetValue(voteManager);

            object appliedVote = Assert.Single(votes.Values.Cast<object>());
            var voteData = (KingdomDecisionVoteData)AccessTools
                .Property(appliedVote.GetType(), "VoteData")
                .GetValue(appliedVote);

            Assert.Equal(1, voteData.OutcomeIndex);
            Assert.True(voteData.IsFinal);
            Assert.Empty(pendingVotes);

            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            var noOption = decisionItem.DecisionOptionsList.Single(option =>
                option.Option == decisionItem.KingdomDecisionMaker._possibleOutcomes[1]);
            Assert.True(client2.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var player1Clan));

            var supporter = Assert.Single(noOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player1Clan));

            Assert.False(string.IsNullOrWhiteSpace(supporter.SupportWeightImagePath));
            Assert.NotNull(supporter.Visual);
        });
    }

    [Fact]
    public void KingdomDecisionVoteState_RemainsWithDecisionWhenEarlierDecisionIsRemoved()
    {
        var client1 = Clients.First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, Clients.Skip(1).First());
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var firstTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var secondTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(firstTargetKingdomId);
        EnsureKingdomRegisteredEverywhere(secondTargetKingdomId);

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(firstTargetKingdomId, out var firstTargetKingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(secondTargetKingdomId, out var secondTargetKingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, firstTargetKingdom));
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, secondTargetKingdom));
            }

            var firstDecision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
            var secondDecision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[1]);
            var voteManager = GetVoteManager(client1);
            voteManager.RegisterDecision(firstDecision);
            voteManager.RegisterDecision(secondDecision);

            var secondDecisionVote = new KingdomDecisionVoteData(
                kingdomId,
                decisionIndex: 1,
                outcomeIndex: 0,
                supportWeight: (int)Supporter.SupportWeights.FullyPush,
                isAbstain: false,
                isFinal: true);
            voteManager.ApplyRemoteVote(player1.ClanId, secondDecisionVote);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(secondDecision));

            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions.RemoveAt(0);
            }

            var remainingDecision = Assert.Single(kingdom.UnresolvedDecisions);
            Assert.Same(secondDecision, remainingDecision);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(secondDecision));
            var debugInfo = Assert.Single(voteManager.GetDecisionDebugInfo(kingdom));
            Assert.Equal(0, debugInfo.DecisionIndex);
            Assert.Contains(debugInfo.ClientVotes, vote =>
                vote.ClanId == player1.ClanId &&
                vote.HasVote &&
                vote.IsFinal);
        });
    }

    [Fact]
    public void KingdomDecisionVoteState_RemainsWithNextDecisionWhenEarlierDecisionResolves()
    {
        var player1 = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var firstTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var secondTargetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(firstTargetKingdomId);
        EnsureKingdomRegisteredEverywhere(secondTargetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(firstTargetKingdomId, out var firstTargetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(secondTargetKingdomId, out var secondTargetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            using (new AllowedThread())
            {
                kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, firstTargetKingdom));
                kingdom._unresolvedDecisions.Add(new DeclareWarDecision(proposerClan, secondTargetKingdom));
            }

            var firstDecision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
            var secondDecision = Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[1]);
            var voteManager = GetVoteManager(Server);
            voteManager.RegisterDecision(firstDecision);
            voteManager.RegisterDecision(secondDecision);

            var firstDecisionVote = CreateDeclareWarVote(kingdomId, isFinal: true);
            var secondDecisionVote = new KingdomDecisionVoteData(
                kingdomId,
                decisionIndex: 1,
                outcomeIndex: 0,
                supportWeight: (int)Supporter.SupportWeights.FullyPush,
                isAbstain: false,
                isFinal: true);
            voteManager.ApplyRemoteVote(player1.ClanId, firstDecisionVote);
            voteManager.ApplyRemoteVote(player1.ClanId, secondDecisionVote);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(secondDecision));

            Assert.True(voteManager.TryResolveDecision(firstDecision, force: true));

            var remainingDecision = Assert.Single(kingdom.UnresolvedDecisions);
            Assert.Same(secondDecision, remainingDecision);
            var debugInfo = Assert.Single(voteManager.GetDecisionDebugInfo(kingdom));
            Assert.Equal(0, debugInfo.DecisionIndex);
            Assert.Contains(debugInfo.ClientVotes, vote =>
                vote.ClanId == player1.ClanId &&
                vote.HasVote &&
                vote.IsFinal);
        });
    }

    [Fact]
    public void KingdomDecisionPreview_DoesNotShowUnvotedPlayerClanAsSupporter()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomPreview", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var player1Clan));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player2.ClanId, out var player2Clan));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            var yesOption = decisionItem.DecisionOptionsList.Single(option =>
                IsDeclareWarOutcome(option.Option, true));

            var stalePlayerSupport = new Supporter(player2Clan);
            stalePlayerSupport.SupportWeight = Supporter.SupportWeights.FullyPush;
            yesOption.Option.AddSupport(stalePlayerSupport);
            Assert.Contains(yesOption.Option.SupporterList, supporter =>
                ReferenceEquals(supporter.Clan, player2Clan));

            var voteManager = GetVoteManager(client1);
            voteManager.UnregisterDecisionItem(decisionItem);
            voteManager.RegisterDecisionItem(decisionItem);

            Assert.DoesNotContain(yesOption.Option.SupporterList, supporter =>
                ReferenceEquals(supporter.Clan, player2Clan));
            Assert.DoesNotContain(yesOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player2Clan));

            yesOption.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;
            Assert.True(voteManager.TryPublishVote(yesOption));

            Assert.Contains(yesOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player1Clan));
            Assert.DoesNotContain(yesOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player2Clan));
        });
    }

    [Fact]
    public void KingdomDecisionResolveTabs_DisableDiplomacyResolveOnlyForClientsThatAlreadyVoted()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomResolveTabs", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionVoteData player1FinalVote = CreateDeclareWarVote(kingdomId, isFinal: true);

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client1);
            voteManager.RegisterDecision(decision);

            var diplomacyVm = CreateDiplomacyResolveVm(out var resolveAction);
            var truceItem = CreateTruceItem(decision.FactionToDeclareWarOn);

            KingdomDiplomacyVMPatches.DisableDiplomacyResolveActionsIfAlreadyVoted(diplomacyVm, truceItem);
            Assert.True(resolveAction.IsEnabled);
            Assert.True(KingdomDiplomacyProposalActionItemVMPatches.ExecuteActionPrefix(resolveAction));

            voteManager.ApplyRemoteVote(player1.ClanId, player1FinalVote);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(decision));

            KingdomDiplomacyVMPatches.DisableDiplomacyResolveActionsIfAlreadyVoted(diplomacyVm, truceItem);
            Assert.False(resolveAction.IsEnabled);
            Assert.False(KingdomDiplomacyProposalActionItemVMPatches.ExecuteActionPrefix(resolveAction));
        });

        client2.Call(() =>
        {
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client2);
            voteManager.RegisterDecision(decision);
            voteManager.ApplyRemoteVote(player1.ClanId, player1FinalVote);
            Assert.False(voteManager.HasLocalPlayerSubmittedVote(decision));

            var diplomacyVm = CreateDiplomacyResolveVm(out var resolveAction);
            var truceItem = CreateTruceItem(decision.FactionToDeclareWarOn);

            KingdomDiplomacyVMPatches.DisableDiplomacyResolveActionsIfAlreadyVoted(diplomacyVm, truceItem);
            Assert.True(resolveAction.IsEnabled);
            Assert.True(KingdomDiplomacyProposalActionItemVMPatches.ExecuteActionPrefix(resolveAction));
        });
    }

    [Fact]
    public void KingdomDecisionResolveTabs_DisablePolicyResolveOnlyForClientsThatAlreadyVoted()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.True(client.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

                PolicyObject policy = PolicyObject.All.First();
                Assert.NotNull(policy);
                using (new AllowedThread())
                {
                    kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                    kingdom._unresolvedDecisions.Add(new KingdomPolicyDecision(proposerClan, policy, false));
                }
            });
        }

        var player1FinalVote = new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: 0,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: true);

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client1);
            voteManager.RegisterDecision(decision);

            KingdomPoliciesVM policiesVm = CreatePolicyResolveVm(decision);
            KingdomPoliciesVMPatches.DisablePolicyResolveIfAlreadyVoted(policiesVm);
            Assert.True(policiesVm.CanProposeOrDisavowPolicy);
            Assert.True(KingdomPoliciesVMPatches.ExecuteProposeOrDisavowPrefix(policiesVm));

            voteManager.ApplyRemoteVote(player1.ClanId, player1FinalVote);
            Assert.True(voteManager.HasLocalPlayerSubmittedVote(decision));

            policiesVm = CreatePolicyResolveVm(decision);
            KingdomPoliciesVMPatches.DisablePolicyResolveIfAlreadyVoted(policiesVm);
            Assert.False(policiesVm.CanProposeOrDisavowPolicy);
            Assert.False(KingdomPoliciesVMPatches.ExecuteProposeOrDisavowPrefix(policiesVm));
        });

        client2.Call(() =>
        {
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<KingdomPolicyDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var voteManager = GetVoteManager(client2);
            voteManager.RegisterDecision(decision);
            voteManager.ApplyRemoteVote(player1.ClanId, player1FinalVote);
            Assert.False(voteManager.HasLocalPlayerSubmittedVote(decision));

            KingdomPoliciesVM policiesVm = CreatePolicyResolveVm(decision);
            KingdomPoliciesVMPatches.DisablePolicyResolveIfAlreadyVoted(policiesVm);
            Assert.True(policiesVm.CanProposeOrDisavowPolicy);
            Assert.True(KingdomPoliciesVMPatches.ExecuteProposeOrDisavowPrefix(policiesVm));
        });
    }

    [Fact]
    public void KingdomDecisionVoteBroadcast_ReplaysRemoteNoVoteInDecisionUi()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomBroadcast", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionVoteData player1NoVote = CreateDeclareWarVoteFromUi(client1, shouldWarBeDeclared: false);
        Assert.Equal((int)Supporter.SupportWeights.FullyPush, player1NoVote.SupportWeight);

        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(player1NoVote));

        var broadcastVote = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>(),
            message => message.VoteData.KingdomId == kingdomId
                       && message.VoteData.DecisionIndex == 0
                       && message.VoteData.OutcomeIndex == 1
                       && message.VoteData.OutcomeKey == player1NoVote.OutcomeKey
                       && message.VoteData.IsFinal);
        Assert.Equal(player1.ClanId, broadcastVote.ClanId);
        Assert.Equal((int)Supporter.SupportWeights.FullyPush, broadcastVote.VoteData.SupportWeight);
        Assert.False(string.IsNullOrWhiteSpace(broadcastVote.VoteData.OutcomeKey));

        client2.SimulateMessage(this, broadcastVote);

        client2.Call(() =>
        {
            Assert.True(client2.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            var yesOption = decisionItem.DecisionOptionsList.Single(option =>
                IsDeclareWarOutcome(option.Option, true));
            var noOption = decisionItem.DecisionOptionsList.Single(option =>
                IsDeclareWarOutcome(option.Option, false));

            Assert.True(client2.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var player1Clan));

            var voteManager = GetVoteManager(client2);
            voteManager.UnregisterDecisionItem(decisionItem);
            voteManager.RegisterDecisionItem(decisionItem);

            Assert.Equal(0, yesOption.WinPercentage);
            Assert.Equal(100, noOption.WinPercentage);
            Assert.DoesNotContain(yesOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player1Clan));

            var supporter = Assert.Single(noOption.SupportersOfThisOption, supporterVm =>
                AccessTools.Field(typeof(DecisionSupporterVM), "_clan").GetValue(supporterVm) is Clan clan &&
                ReferenceEquals(clan, player1Clan));

            Assert.False(string.IsNullOrWhiteSpace(supporter.SupportWeightImagePath));
            Assert.NotNull(supporter.Visual);
        });
    }

    [Fact]
    public void KingdomDecisionDebugCommand_ListsClientVoteState()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetPlayer = CreateSyncedPlayerContext("TargetKingdomDebugCommand", _ => false);

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        ConfigureClanInKingdom(targetPlayer.ClanId, targetKingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionVoteData player1NoVote = CreateDeclareWarVoteFromUi(client1, shouldWarBeDeclared: false);
        Assert.Equal((int)Supporter.SupportWeights.FullyPush, player1NoVote.SupportWeight);
        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(player1NoVote));

        var broadcastVote = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkChangeKingdomDecisionVote>(),
            message => message.VoteData.KingdomId == kingdomId
                       && message.VoteData.DecisionIndex == 0
                       && message.VoteData.OutcomeIndex == 1
                       && message.VoteData.OutcomeKey == player1NoVote.OutcomeKey
                       && message.VoteData.IsFinal);
        Assert.Equal((int)Supporter.SupportWeights.FullyPush, broadcastVote.VoteData.SupportWeight);
        client2.SimulateMessage(this, broadcastVote);

        string output = null;
        client2.Call(() =>
        {
            output = KingdomDebugCommand.ListKingdomDecisionVotes(new List<string> { kingdomId });
        });

        Assert.Contains("DeclareWarDecision", output);
        Assert.Contains(ControllerId, output);
        Assert.Contains("Voted No", output);
        Assert.Contains("Support: FullyPush", output);
        Assert.Contains(SecondControllerId, output);
        Assert.Contains("Not Voted", output);
        Assert.DoesNotContain("Voted Yes", output);
    }

    [Fact]
    public void ClientKingdomDecisionProposal_AddsPendingDecisionOnServerAndClients()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        var addDecisionMessage = Assert.Single(client1.NetworkSentMessages.GetMessages<NetworkAddDecision>());
        Assert.Equal(kingdomId, addDecisionMessage.KingdomId);
        Assert.Equal(kingdomId, addDecisionMessage.Data.KingdomId);
        Assert.Equal(player1.ClanId, addDecisionMessage.Data.ProposerClanId);
        Assert.IsType<DeclareWarDecisionData>(addDecisionMessage.Data);
        Assert.Contains(Server.InternalMessages.GetMessages<NetworkAddDecision>(), message => message.KingdomId == kingdomId);
        Assert.Contains(Server.InternalMessages.GetMessages<AddDecision>(), message => message.KingdomId == kingdomId);
        Assert.DoesNotContain(client1.InternalMessages.GetMessages<AddDecision>(), message => message.KingdomId == kingdomId);
        Assert.Contains(client2.InternalMessages.GetMessages<AddDecision>(), message => message.KingdomId == kingdomId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.Single(kingdom.UnresolvedDecisions);
            Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
                Assert.Single(kingdom.UnresolvedDecisions);
                Assert.IsType<DeclareWarDecision>(kingdom.UnresolvedDecisions[0]);
            });
        }

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
    }

    [Fact]
    public void ClientKingdomDecisionProposal_SpendsInfluenceOnlyOnServer()
    {
        var client1 = Clients.First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        const float initialInfluence = 500f;

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
                using (new AllowedThread())
                {
                    proposerClan._influence = initialInfluence;
                }
            });
        }

        int influenceCost = 0;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            influenceCost = new DeclareWarDecision(proposerClan, targetKingdom).GetInfluenceCost(proposerClan);
            Assert.True(influenceCost > 0);
        });

        KingdomDecisionData decisionData = null;
        float clientInfluenceAfterProposal = 0;
        client1.Call(() =>
        {
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(client1.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));

            var decision = new DeclareWarDecision(proposerClan, targetKingdom);
            kingdom.AddDecision(decision);
            decisionData = client1.Resolve<IKingdomDecisionDataConverter>().Convert(decision);
            clientInfluenceAfterProposal = proposerClan.Influence;
        }, new[] { AccessTools.Method(typeof(ClientKingdomHandler), "HandleLocalDecisionAdded") });

        Assert.Equal(initialInfluence, clientInfluenceAfterProposal, precision: 4);
        Assert.NotNull(decisionData);
        Server.SimulateMessage(
            this,
            new NetworkAddDecision(kingdomId, decisionData, ignoreInfluenceCost: false, randomNumber: 0.5f));
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
            Assert.Equal(initialInfluence - influenceCost, proposerClan.Influence, precision: 4);
        });
    }

    [Fact]
    public void KingdomDecisionVotes_WaitForPlayerClanWhenLeaderHeroMappingIsMissing()
    {
        var client1 = Clients.First();
        var client2 = Clients.Skip(1).First();
        client1.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);
        client2.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        var player1 = CreateSyncedPlayerContext(ControllerId, client1);
        var player2 = CreateSyncedPlayerContext(SecondControllerId, client2);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player1.ClanId, kingdomId);
        ConfigureClanInKingdom(player2.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player1.ClanId, out var proposerClan));
            var playerManager = Server.Resolve<IPlayerManager>();
            var players = (HashSet<Player>)AccessTools.Field(playerManager.GetType(), "_players").GetValue(playerManager);
            var oldPlayer = players.Single(player => player.ControllerId == SecondControllerId);

            Assert.True(players.Remove(oldPlayer));
            Assert.True(players.Add(new Player(
                SecondControllerId,
                "missingHero",
                player2.PartyId,
                player2.ClanId,
                player2.CharacterId)));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        client1.SimulateMessage(this, new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>());
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>());
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.Single(kingdom.UnresolvedDecisions);
        });

        client2.SimulateMessage(this, new KingdomDecisionVoteRequested(CreateDeclareWarVote(kingdomId, isFinal: true)));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId
                       && message.DecisionIndex == 0
                       && message.OutcomeIndex == 0
                       && message.IsPlayerDecision);
        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkDeclareWar>(),
            message => message.Faction1Id == kingdomId
                       && message.Faction2Id == targetKingdomId
                       && message.Detail == (int)DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision);
    }

    [Fact]
    public void KingdomDecisionVoteData_CreatedFromDecisionVmUsesRegisteredKingdomId()
    {
        var client = Clients.First();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        var player = CreateSyncedPlayerContext(ControllerId, client);
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var targetKingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();

        ConfigureClanInKingdom(player.ClanId, kingdomId);
        EnsureKingdomRegisteredEverywhere(kingdomId);
        EnsureKingdomRegisteredEverywhere(targetKingdomId);
        SetKingdomStringIdEverywhere(kingdomId, "native_created_kingdom");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(targetKingdomId, out var targetKingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(player.ClanId, out var proposerClan));

            kingdom.AddDecision(new DeclareWarDecision(proposerClan, targetKingdom));
        });

        KingdomDecisionVoteData voteData = null;
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var election = new KingdomElection(decision);
            election.Setup();
            election.DetermineSupport(election._possibleOutcomes, false);
            decision.DetermineSponsors(election._possibleOutcomes);
            election.UpdateSupport(election._possibleOutcomes);

            var option = ObjectHelper.SkipConstructor<DecisionOptionVM>();
            AccessTools.Field(typeof(DecisionOptionVM), "<Option>k__BackingField")
                .SetValue(option, election._possibleOutcomes[0]);
            AccessTools.Field(typeof(DecisionOptionVM), "<Decision>k__BackingField")
                .SetValue(option, decision);
            AccessTools.Field(typeof(DecisionOptionVM), "_kingdomDecisionMaker")
                .SetValue(option, election);
            option.CurrentSupportWeight = Supporter.SupportWeights.FullyPush;

            Assert.Same(decision, option.Decision);
            Assert.Same(election._possibleOutcomes[0], option.Option);
            Assert.Same(kingdom, Clan.PlayerClan.Kingdom);
            Assert.Same(Hero.MainHero, Clan.PlayerClan.Leader);
            Assert.Contains(option.Option, election._possibleOutcomes);

            Assert.True(GetVoteManager(client).TryCreateVoteData(option, out voteData, isFinal: true));
        });

        Assert.Equal(kingdomId, voteData.KingdomId);
        Assert.NotEqual("native_created_kingdom", voteData.KingdomId);

        client.SimulateMessage(this, new KingdomDecisionVoteRequested(voteData));

        Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkKingdomDecisionResolved>(),
            message => message.KingdomId == kingdomId
                       && message.DecisionIndex == 0
                       && message.OutcomeIndex == 0
                       && message.IsPlayerDecision);
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_RelinksClientClanWhenFieldSyncHasNotArrived()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));

            using (new AllowedThread())
            {
                kingdom._rulingClan = null;
                clan.Kingdom = null;
            }
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));

            Assert.Same(kingdom, clan.Kingdom);
            Assert.Same(clan, kingdom.RulingClan);
            AssertKingdomReadyForManagementUi(kingdom, clan);
            Assert.Contains(kingdom, Campaign.Current.CampaignObjectManager.Kingdoms);
        });
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_InitializesClientKingdomManagementState()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));

            using (new AllowedThread())
            {
                kingdom._activePolicies = null;
                kingdom._armies = null;
                kingdom._clans = null;
                kingdom._unresolvedDecisions = null;
                kingdom._rulingClan = null;
                clan.Kingdom = null;
            }
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(player.ClanId, out var clan));

            Assert.Same(kingdom, clan.Kingdom);
            Assert.Same(clan, kingdom.RulingClan);
            AssertKingdomReadyForManagementUi(kingdom, clan);
        });
    }

    [Fact]
    public void ClientKingdomCreationRequest_PreservesSettlementContextWhenNotificationReturnsImmediately()
    {
        var player = CreateSyncedPlayerContext();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(this, new KingdomCreationRequested(KingdomName, player.CultureId));

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateKingdom>());
        Assert.Equal(ControllerId, request.ControllerId);
        Assert.Equal(player.PartyId, request.PartyId);
        Assert.Equal(settlementId, request.SettlementId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });

        Assert.Contains(
            client.InternalMessages.GetMessages<PlayerKingdomCreated>(),
            message => message.ControllerId == ControllerId
                       && message.ClanId == player.ClanId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Fact]
    public void ClientKingdomCreationRequest_DoesNotUseSettlementStringIdWhenReverseLookupIsMissing()
    {
        var player = CreateSyncedPlayerContext();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            RemoveReverseObjectManagerId(client, settlement);

            Assert.False(client.ObjectManager.TryGetId(settlement, out _));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var _));
            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(this, new KingdomCreationRequested(KingdomName, player.CultureId));

        var request = Assert.Single(client.NetworkSentMessages.GetMessages<NetworkRequestCreateKingdom>());
        Assert.Equal(ControllerId, request.ControllerId);
        Assert.Null(request.PartyId);
        Assert.Null(request.SettlementId);
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_RestoresCreatingClientSettlementContextAfterLocalExit()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        var handler = client.Resolve<ClientKingdomHandler>();
        var pending = AccessTools.Method(typeof(ClientKingdomHandler), "CapturePendingSettlementRestore")
            .Invoke(handler, Array.Empty<object>());
        Assert.NotNull(pending);
        AccessTools.Field(typeof(ClientKingdomHandler), "pendingKingdomCreationSettlement")
            .SetValue(handler, pending);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId, player.PartyId, settlementId));

        Assert.Contains(
            client.InternalMessages.GetMessages<StartSettlementEncounterAttempted>(),
            message => ReferenceEquals(message.Party, GetObject<MobileParty>(client, player.PartyId))
                       && ReferenceEquals(message.Settlement, GetObject<Settlement>(client, settlementId)));

        Assert.Contains(
            client.NetworkSentMessages.GetMessages<NetworkRequestStartSettlementEncounter>(),
            message => message.PartyId == player.PartyId && message.SettlementId == settlementId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_RestoresSettlementWithoutPublishingLeave()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        var handler = client.Resolve<ClientKingdomHandler>();
        var pending = AccessTools.Method(typeof(ClientKingdomHandler), "CapturePendingSettlementRestore")
            .Invoke(handler, Array.Empty<object>());
        Assert.NotNull(pending);
        AccessTools.Field(typeof(ClientKingdomHandler), "pendingKingdomCreationSettlement")
            .SetValue(handler, pending);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId, player.PartyId, settlementId));

        Assert.DoesNotContain(
            client.NetworkSentMessages.GetMessages<NetworkRequestEndSettlementEncounter>(),
            message => message.PartyId == player.PartyId);
        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>(),
            message => message.PartyId == player.PartyId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
        AssertCompletedSettlementProtectionDisarms(client, player.PartyId, settlementId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_RemoteClientAllowsLaterSettlementLeave()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.Skip(1).First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(SecondControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Null(party.CurrentSettlement);
            Assert.NotNull(settlement);
        });

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId, player.PartyId, settlementId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });

        client.SimulateMessage(this, new NetworkPartyEnterSettlement(settlementId, player.PartyId));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Null(party.CurrentSettlement);
        });
    }

    [Fact]
    public void ClientKingdomCreationRequest_ServerSuppressesAccidentalSettlementLeaveConsequence()
    {
        var player = CreateSyncedPlayerContext();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }
        });

        client.SimulateMessage(this, new KingdomCreationRequested(KingdomName, player.CultureId));
        client.SimulateMessage(this, new EndSettlementEncounterAttempted(GetObject<MobileParty>(client, player.PartyId)));

        var leaveRequest = Assert.Single(
            client.NetworkSentMessages.GetMessages<NetworkRequestEndSettlementEncounter>(),
            message => message.PartyId == player.PartyId);
        Assert.Equal(player.PartyId, leaveRequest.PartyId);
        var leaveResult = Assert.Single(
            client.InternalMessages.GetMessages<NetworkSettlementEncounterLeaveResult>(),
            message => message.PartyId == player.PartyId);
        Assert.Equal(SettlementEncounterLeaveOutcome.Suppressed, leaveResult.Outcome);
        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>(),
            message => message.PartyId == player.PartyId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    [Fact]
    public void ServerKingdomCreationRequest_SuppressesAccidentalSettlementLeaveRequest()
    {
        var player = CreateSyncedPlayerContext();
        var settlementId = CreateSyncedSettlement();

        Server.SimulateMessage(
            this,
            new NetworkRequestCreateKingdom(ControllerId, KingdomName, player.CultureId, player.PartyId, settlementId));
        Server.SimulateMessage(this, new NetworkRequestEndSettlementEncounter(player.PartyId));
        Server.SimulateMessage(this, new PartyLeaveSettlementAttempted(GetObject<MobileParty>(Server, player.PartyId)));

        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkSettlementEncounterLeaveResult>(),
            message => true);
        Assert.DoesNotContain(
            Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>(),
            message => message.PartyId == player.PartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
        AssertCompletedSettlementProtectionDisarms(Server, player.PartyId, settlementId);
    }

    [Fact]
    public void PlayerKingdomCreatedNotification_SuppressesStaleEndEncounterAfterCompletion()
    {
        var player = CreateSyncedPlayerContext();
        var kingdomId = TestEnvironment.CreateRegisteredObject<Kingdom>();
        var client = Clients.First();
        var settlementId = CreateSyncedSettlement();
        client.Resolve<IControllerIdProvider>().SetControllerId(ControllerId);

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = settlement;
            }

            Assert.Same(settlement, party.CurrentSettlement);
        });

        var handler = client.Resolve<ClientKingdomHandler>();
        var pending = AccessTools.Method(typeof(ClientKingdomHandler), "CapturePendingSettlementRestore")
            .Invoke(handler, Array.Empty<object>());
        Assert.NotNull(pending);
        AccessTools.Field(typeof(ClientKingdomHandler), "pendingKingdomCreationSettlement")
            .SetValue(handler, pending);

        client.SimulateMessage(
            this,
            new NetworkPlayerKingdomCreated(ControllerId, kingdomId, KingdomName, player.ClanId, player.PartyId, settlementId));
        client.SimulateMessage(
            this,
            new NetworkSettlementEncounterLeaveResult(
                player.PartyId,
                SettlementEncounterLeaveOutcome.Suppressed));

        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(player.PartyId, out var party));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            Assert.Same(settlement, party.CurrentSettlement);
        });
    }

    private static T GetObject<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private static void AssertCompletedSettlementProtectionDisarms(
        EnvironmentInstance instance,
        string partyId,
        string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Same(settlement, party.CurrentSettlement);

            using (new AllowedThread())
            {
                party.CurrentSettlement = null;
            }

            Assert.Null(party.CurrentSettlement);
        });
    }

    private string CreateSyncedSettlement()
    {
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        ConfigureSettlementParty(Server, settlementId);
        foreach (var client in Clients)
        {
            ConfigureSettlementParty(client, settlementId);
        }

        return settlementId;
    }

    private static void ConfigureSettlementParty(EnvironmentInstance instance, string settlementId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));
            if (settlement.Party != null) return;

            var party = GameObjectCreator.CreateInitializedObject<MobileParty>();
            using (new AllowedThread())
            {
                settlement.Party = new PartyBase(party, settlement);
            }
        });
    }

    private void SetKingdomStringIdEverywhere(string kingdomId, string stringId)
    {
        SetKingdomStringId(Server, kingdomId, stringId);
        foreach (var client in Clients)
        {
            SetKingdomStringId(client, kingdomId, stringId);
        }
    }

    private static void SetKingdomStringId(EnvironmentInstance instance, string kingdomId, string stringId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            kingdom.StringId = stringId;
        });
    }

    private static void RemoveReverseObjectManagerId(EnvironmentInstance instance, object obj)
    {
        var table = (ConditionalWeakTable<object, string>)AccessTools
            .Field(typeof(ObjectManager), "objsIds")
            .GetValue(instance.ObjectManager);

        Assert.True(table.Remove(obj));
    }

    private PlayerContext CreateSyncedPlayerContext()
    {
        return CreateSyncedPlayerContext(ControllerId, _ => true);
    }

    private PlayerContext CreateSyncedPlayerContext(string controllerId, EnvironmentInstance localPlayerClient)
    {
        return CreateSyncedPlayerContext(
            controllerId,
            instance => ReferenceEquals(instance, localPlayerClient));
    }

    private PlayerContext CreateSyncedPlayerContext(
        string controllerId,
        Func<EnvironmentInstance, bool> shouldSetMainHero)
    {
        var clanId = TestEnvironment.CreateRegisteredObject<Clan>();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        var partyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var characterId = TestEnvironment.CreateRegisteredObject<CharacterObject>();
        var cultureId = TestEnvironment.CreateRegisteredObject<CultureObject>();

        ConfigurePlayerContext(Server, controllerId, clanId, heroId, partyId, characterId, cultureId, shouldSetMainHero(Server));
        foreach (var client in Clients)
        {
            ConfigurePlayerContext(client, controllerId, clanId, heroId, partyId, characterId, cultureId, shouldSetMainHero(client));
        }

        return new PlayerContext(clanId, heroId, partyId, characterId, cultureId);
    }

    private string CreateSyncedClanFief(string clanId)
    {
        var fiefId = TestEnvironment.CreateRegisteredObject<Town>();
        ConfigureClanFief(Server, clanId, fiefId);
        foreach (var client in Clients)
        {
            ConfigureClanFief(client, clanId, fiefId);
        }

        return fiefId;
    }

    private static KingdomDecisionVoteData CreateDeclareWarVote(string kingdomId, bool isFinal)
    {
        return new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: 0,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: isFinal);
    }

    private static KingdomDecisionVoteData CreateDeclareWarNoVote(string kingdomId, bool isFinal)
    {
        return new KingdomDecisionVoteData(
            kingdomId,
            decisionIndex: 0,
            outcomeIndex: 1,
            supportWeight: (int)Supporter.SupportWeights.FullyPush,
            isAbstain: false,
            isFinal: isFinal);
    }

    private static KingdomDecisionVoteData CreateDeclareWarVoteFromUi(EnvironmentInstance instance, bool shouldWarBeDeclared)
    {
        KingdomDecisionVoteData voteData = null;
        instance.Call(() =>
        {
            var kingdom = Clan.PlayerClan.Kingdom;
            Assert.NotNull(kingdom);
            var decision = Assert.IsType<DeclareWarDecision>(Assert.Single(kingdom.UnresolvedDecisions));
            var decisionsVm = new KingdomDecisionsVM(() => { });
            decisionsVm.RefreshWith(decision);
            var decisionItem = decisionsVm.CurrentDecision;
            var selectedOption = decisionItem.DecisionOptionsList.Single(option =>
                IsDeclareWarOutcome(option.Option, shouldWarBeDeclared));

            selectedOption.CurrentSupportWeight = Supporter.SupportWeights.Choose;
            selectedOption.IsSelected = true;
            AccessTools.Field(typeof(DecisionItemBaseVM), "_currentSelectedOption")
                .SetValue(decisionItem, selectedOption);

            Assert.True(GetVoteManager(instance).TryCreateVoteData(decisionItem, out voteData, true));
        });
        return voteData;
    }

    private static bool IsDeclareWarOutcome(DecisionOutcome outcome, bool shouldWarBeDeclared)
    {
        FieldInfo fieldInfo = outcome?.GetType().GetField(
            "ShouldWarBeDeclared",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return fieldInfo?.FieldType == typeof(bool) &&
               (bool)fieldInfo.GetValue(outcome) == shouldWarBeDeclared;
    }

    private static KingdomDiplomacyVM CreateDiplomacyResolveVm(out KingdomDiplomacyProposalActionItemVM resolveAction)
    {
        var diplomacyVm = ObjectHelper.SkipConstructor<KingdomDiplomacyVM>();
        var actions = new MBBindingList<KingdomDiplomacyProposalActionItemVM>();
        resolveAction = new KingdomDiplomacyProposalActionItemVM(
            GameTexts.FindText("str_resolve"),
            GameTexts.FindText("str_resolve_explanation"),
            0,
            true,
            TextObject.GetEmpty(),
            () => { });
        actions.Add(resolveAction);
        AccessTools.Field(typeof(KingdomDiplomacyVM), "_actions").SetValue(diplomacyVm, actions);
        return diplomacyVm;
    }

    private static KingdomTruceItemVM CreateTruceItem(IFaction faction)
    {
        var truceItem = ObjectHelper.SkipConstructor<KingdomTruceItemVM>();
        AccessTools.Field(typeof(KingdomDiplomacyItemVM), "Faction2").SetValue(truceItem, faction);
        return truceItem;
    }

    private static KingdomPoliciesVM CreatePolicyResolveVm(KingdomDecision decision)
    {
        var policiesVm = ObjectHelper.SkipConstructor<KingdomPoliciesVM>();
        AccessTools.Field(typeof(KingdomPoliciesVM), "_currentItemsUnresolvedDecision").SetValue(policiesVm, decision);
        AccessTools.Field(typeof(KingdomPoliciesVM), "_canProposeOrDisavowPolicy").SetValue(policiesVm, true);
        AccessTools.Field(typeof(KingdomPoliciesVM), "_doneHint").SetValue(policiesVm, new HintViewModel());
        return policiesVm;
    }

    private static void ConfigureClanFief(EnvironmentInstance instance, string clanId, string fiefId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(instance.ObjectManager.TryGetObject<Town>(fiefId, out var fief));

            using (new AllowedThread())
            {
                fief._ownerClan = clan;
                clan._fiefsCache ??= new MBList<Town>();
                if (!clan._fiefsCache.Contains(fief))
                {
                    clan._fiefsCache.Add(fief);
                }
            }

            Assert.Contains(fief, clan.Fiefs);
        });
    }

    private void ConfigureClanInKingdom(string clanId, string kingdomId)
    {
        ConfigureClanInKingdom(Server, clanId, kingdomId);
        foreach (var client in Clients)
        {
            ConfigureClanInKingdom(client, clanId, kingdomId);
        }
    }

    private void SetClanTierEverywhere(string clanId, int tier)
    {
        SetClanTier(Server, clanId, tier);
        foreach (var client in Clients)
        {
            SetClanTier(client, clanId, tier);
        }
    }

    private static void AssertVassalMembership(
        EnvironmentInstance instance,
        string clanId,
        string kingdomId)
    {
        Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
        Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
        Assert.Same(kingdom, clan.Kingdom);
        Assert.Contains(clan, kingdom.Clans);
        Assert.False(clan.IsUnderMercenaryService);
    }

    private static void SetClanTier(EnvironmentInstance instance, string clanId, int tier)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            using (new AllowedThread())
            {
                clan._tier = tier;
            }
        });
    }

    private static void ConfigureClanInKingdom(EnvironmentInstance instance, string clanId, string kingdomId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                clan._kingdom = kingdom;
                kingdom._rulingClan ??= clan;
                kingdom._clans ??= new MBList<Clan>();
                if (!kingdom._clans.Contains(clan))
                {
                    kingdom._clans.Add(clan);
                }
            }
        });
    }

    private static void ConfigurePlayerContext(
        EnvironmentInstance instance,
        string controllerId,
        string clanId,
        string heroId,
        string partyId,
        string characterId,
        string cultureId,
        bool setAsMainHero)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(instance.ObjectManager.TryGetObject<Hero>(heroId, out var hero));
            Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            Assert.True(instance.ObjectManager.TryGetObject<CharacterObject>(characterId, out var character));
            Assert.True(instance.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));

            using (new AllowedThread())
            {
                clan.Name = new TextObject("realclan");
                hero.Clan = clan;
                clan.SetLeader(hero);
                character.HeroObject = hero;
                if (setAsMainHero)
                {
                    Game.Current.PlayerTroop = character;
                    Campaign.Current.PlayerDefaultFaction = clan;
                }
                hero.PartyBelongedTo = party;
                party.ActualClan = clan;
                character.Culture = culture;
                if (!Campaign.Current.CampaignObjectManager._mobileParties.Contains(party))
                {
                    Campaign.Current.CampaignObjectManager.AddMobileParty(party);
                }
            }

            var registry = instance.Resolve<IPlayerManager>();
            Assert.True(registry.AddPlayer(new Player(controllerId, heroId, partyId, clanId, characterId)));
            Assert.True(registry.TryGetPlayer(controllerId, out var _));
        });
    }

    private void EnsureKingdomRegisteredEverywhere(string kingdomId)
    {
        EnsureKingdomRegistered(Server, kingdomId);
        foreach (var client in Clients)
        {
            EnsureKingdomRegistered(client, kingdomId);
        }
    }

    private static void EnsureKingdomRegistered(EnvironmentInstance instance, string kingdomId)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));

            using (new AllowedThread())
            {
                kingdom._activePolicies ??= new MBList<PolicyObject>();
                kingdom._armies ??= new MBList<Army>();
                kingdom._clans ??= new MBList<Clan>();
                kingdom._unresolvedDecisions ??= new MBList<KingdomDecision>();
                kingdom._factionsAtWarWith ??= new MBList<IFaction>();
                kingdom._alliedKingdoms ??= new MBList<Kingdom>();

                if (!Campaign.Current.CampaignObjectManager.Kingdoms.Contains(kingdom))
                {
                    Campaign.Current.CampaignObjectManager.AddKingdom(kingdom);
                }
            }
        });
    }

    private void AssertKingdomCreatedOnServer(string kingdomId, string clanId, string cultureId, string? fiefId = null)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(Server.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(Server.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));

            Assert.Equal(KingdomName, kingdom.Name.ToString());
            Assert.Same(clan, kingdom.RulingClan);
            Assert.Same(culture, kingdom.Culture);
            Assert.Same(kingdom, clan.Kingdom);
            Assert.Contains(clan, kingdom.Clans);
            AssertKingdomContainsFief(Server.ObjectManager, kingdom, fiefId);
            Assert.Contains(kingdom, Campaign.Current.CampaignObjectManager.Kingdoms);
        });
    }

    private void AssertKingdomSyncedToClient(
        EnvironmentInstance client,
        string kingdomId,
        string clanId,
        string cultureId,
        string? fiefId = null)
    {
        client.Call(() =>
        {
            Assert.True(client.ObjectManager.TryGetObject<Kingdom>(kingdomId, out var kingdom));
            Assert.True(client.ObjectManager.TryGetObject<Clan>(clanId, out var clan));
            Assert.True(client.ObjectManager.TryGetObject<CultureObject>(cultureId, out var culture));

            Assert.Equal(KingdomName, kingdom.Name.ToString());
            Assert.Same(clan, kingdom.RulingClan);
            Assert.Same(culture, kingdom.Culture);
            Assert.Same(kingdom, clan.Kingdom);
            AssertKingdomReadyForManagementUi(kingdom, clan);
            AssertKingdomContainsFief(client.ObjectManager, kingdom, fiefId);
            Assert.Contains(kingdom, Campaign.Current.CampaignObjectManager.Kingdoms);
        });
    }

    private static void AssertKingdomContainsFief(IObjectManager objectManager, Kingdom kingdom, string? fiefId)
    {
        if (string.IsNullOrWhiteSpace(fiefId)) return;

        Assert.True(objectManager.TryGetObject<Town>(fiefId, out var fief));
        Assert.Contains(fief, kingdom._fiefsCache);
        Assert.Contains(fief, kingdom.Fiefs);
        if (fief.IsTown)
        {
            Assert.Contains(fief, kingdom._townsCache);
            Assert.Contains(fief, kingdom.Towns);
        }
    }

    private static void AssertKingdomReadyForManagementUi(Kingdom kingdom, Clan clan)
    {
        Assert.NotNull(kingdom._activePolicies);
        Assert.NotNull(kingdom._armies);
        Assert.NotNull(kingdom._clans);
        Assert.NotNull(kingdom._unresolvedDecisions);
        Assert.NotNull(kingdom._factionsAtWarWith);
        Assert.NotNull(kingdom._alliedKingdoms);
        Assert.NotNull(kingdom._fiefsCache);
        Assert.NotNull(kingdom._townsCache);
        Assert.NotNull(kingdom._settlementsCache);
        Assert.NotNull(kingdom._villagesCache);
        Assert.NotNull(kingdom._heroesCache);
        Assert.NotNull(kingdom._aliveLordsCache);
        Assert.NotNull(kingdom._deadLordsCache);
        Assert.NotNull(kingdom._warPartyComponentsCache);
        Assert.Contains(clan, kingdom.Clans);
        Assert.Empty(kingdom.UnresolvedDecisions);
        _ = kingdom.ActivePolicies.Count;
    }

    private record PlayerContext(
        string ClanId,
        string HeroId,
        string PartyId,
        string CharacterId,
        string CultureId);
}
