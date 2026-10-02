using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.MapEvents.Messages.Conversation;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using LiteNetLib;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Handlers;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;
using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

internal sealed class GangLeaderWeaponsIssueHandler : IHandler
{
    private readonly IMessageBroker broker;
    private readonly INetwork network;
    private readonly IObjectManager objects;
    private readonly IPlayerManager players;
    private readonly IIssueGenerationRegistry generations;
    private readonly IIssueOwnershipRegistry ownership;
    private readonly IGangLeaderWeaponsAcceptance acceptance;
    private readonly IGangLeaderWeaponsQuestActions actions;
    private readonly HashSet<Quest> pendingGuardEntries = new();
    private Quest pendingBattle;
    private string pendingBattleRequestId;

    public GangLeaderWeaponsIssueHandler(IMessageBroker broker, INetwork network, IObjectManager objects,
        IPlayerManager players, IIssueGenerationRegistry generations, IIssueOwnershipRegistry ownership,
        IGangLeaderWeaponsAcceptance acceptance, IGangLeaderWeaponsQuestActions actions)
    {
        this.broker = broker;
        this.network = network;
        this.objects = objects;
        this.players = players;
        this.generations = generations;
        this.ownership = ownership;
        this.acceptance = acceptance;
        this.actions = actions;
        broker.Subscribe<GangLeaderWeaponsIssueCreated>(HandleCreated);
        broker.Subscribe<NetworkGangLeaderWeaponsIssueCreated>(HandleNetworkCreated);
        broker.Subscribe<GangLeaderWeaponsActionRequested>(HandleActionRequested);
        broker.Subscribe<RequestGangLeaderWeaponsAction>(HandleNetworkAction);
        broker.Subscribe<NetworkGangLeaderWeaponsState>(HandleState);
        broker.Subscribe<ConversationRestartApproved>(HandleEncounterApproved);
        broker.Subscribe<ConversationRestartRejected>(HandleEncounterRejected);
        broker.Subscribe<GangLeaderWeaponsLogAdded>(HandleLogAdded);
        broker.Subscribe<NetworkGangLeaderWeaponsLog>(HandleNetworkLog);
        broker.Subscribe<GangLeaderWeaponsStateChanged>(HandleStateChanged);
        broker.Subscribe<MapEventResultsCommitted>(HandleBattleResults);
        broker.Subscribe<GangLeaderWeaponsQuestEnded>(HandleQuestEnded);
    }

    public void Dispose()
    {
        broker.Unsubscribe<GangLeaderWeaponsIssueCreated>(HandleCreated);
        broker.Unsubscribe<NetworkGangLeaderWeaponsIssueCreated>(HandleNetworkCreated);
        broker.Unsubscribe<GangLeaderWeaponsActionRequested>(HandleActionRequested);
        broker.Unsubscribe<RequestGangLeaderWeaponsAction>(HandleNetworkAction);
        broker.Unsubscribe<NetworkGangLeaderWeaponsState>(HandleState);
        broker.Unsubscribe<ConversationRestartApproved>(HandleEncounterApproved);
        broker.Unsubscribe<ConversationRestartRejected>(HandleEncounterRejected);
        broker.Unsubscribe<GangLeaderWeaponsLogAdded>(HandleLogAdded);
        broker.Unsubscribe<NetworkGangLeaderWeaponsLog>(HandleNetworkLog);
        broker.Unsubscribe<GangLeaderWeaponsStateChanged>(HandleStateChanged);
        broker.Unsubscribe<MapEventResultsCommitted>(HandleBattleResults);
        broker.Unsubscribe<GangLeaderWeaponsQuestEnded>(HandleQuestEnded);
        pendingGuardEntries.Clear();
        pendingBattle = null;
        pendingBattleRequestId = null;
    }

    private void HandleCreated(MessagePayload<GangLeaderWeaponsIssueCreated> payload)
    {
        if (ModInformation.IsClient) return;
        var issue = payload.What.Issue;
        if (!acceptance.TryCaptureFields(issue, out var fields) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId)) return;
        network.SendAll(new NetworkGangLeaderWeaponsIssueCreated(giverId, generations.Bump(issue.IssueOwner), fields));
    }

    private void HandleNetworkCreated(MessagePayload<NetworkGangLeaderWeaponsIssueCreated> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objects.TryGetObjectWithLogging<Hero>(data.GiverId, out var giver)) return;
            if (generations.TryGetGeneration(giver, out var current) && data.Generation <= current) return;
            if (giver.Issue != null) return;
            var runner = new CreationCaptureRunner<Issue, GangLeaderWeaponsCreationFields>(acceptance, IssueBase.IssueFrequency.Common);
            runner.ConstructAndRegisterReplicated(giver, data.Fields);
            generations.SetGeneration(giver, data.Generation);
        });
    }

    private void HandleActionRequested(MessagePayload<GangLeaderWeaponsActionRequested> payload)
    {
        if (ModInformation.IsServer) return;
        var quest = payload.What.Quest;
        if (quest == null || !quest.IsOngoing || !ownership.IsLocalPeerOwner(quest.QuestGiver) ||
            !objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId) ||
            !generations.TryGetGeneration(quest.QuestGiver, out var generation)) return;
        if (payload.What.Action == GangLeaderWeaponsAction.EnterTown && !pendingGuardEntries.Add(quest)) return;
        if (payload.What.Action == GangLeaderWeaponsAction.LeaveTown) pendingGuardEntries.Remove(quest);
        if (payload.What.Action == GangLeaderWeaponsAction.BeginBattle)
        {
            if (pendingBattle != null && pendingBattle.IsOngoing) return;
            pendingBattle = quest;
            pendingBattleRequestId = null;
        }
        network.SendAll(new RequestGangLeaderWeaponsAction(giverId, generation, quest.StringId, payload.What.Action));
    }

    private void HandleNetworkAction(MessagePayload<RequestGangLeaderWeaponsAction> payload)
    {
        if (ModInformation.IsClient) return;
        var data = payload.What;
        var peer = payload.Who as NetPeer;
        GameThread.RunSafe(() =>
        {
            if (peer == null || !players.TryGetPlayer(peer, out var player) ||
                !TryGetQuest(data.GiverId, data.Generation, data.QuestId, out var quest) ||
                !ownership.TryGetOwnerControllerId(quest.QuestGiver, out var controllerId) ||
                controllerId != player.ControllerId || !TryGetOwner(player, out var hero, out var party)) return;
            var accepted = actions.Apply(quest, hero, party, data.Action);
            if (quest.IsOngoing) PublishState(quest, data.GiverId, data.Generation, data.Action, accepted);
        });
    }

    private bool TryGetOwner(Player player, out Hero hero, out MobileParty party)
    {
        party = null;
        return objects.TryGetObjectWithLogging(player.HeroId, out hero) &&
            objects.TryGetObjectWithLogging(player.MobilePartyId, out party);
    }

    private bool TryGetQuest(string giverId, int generation, string questId, out Quest quest)
    {
        quest = null;
        if (!objects.TryGetObjectWithLogging<Hero>(giverId, out var giver) ||
            !generations.TryGetGeneration(giver, out var current) || current != generation ||
            giver.Issue?.IssueQuest is not Quest candidate || candidate.StringId != questId || !candidate.IsOngoing) return false;
        quest = candidate;
        return true;
    }

    private void PublishState(Quest quest, string giverId, int generation, GangLeaderWeaponsAction action, bool accepted)
    {
        string guardsId = null;
        if (quest._guardsParty != null && quest._guardsParty.IsActive &&
            !objects.TryGetIdWithLogging(quest._guardsParty, out guardsId)) return;
        var confiscated = quest._weaponsThatGuardTook.Select(pair => new ItemRosterElement(pair.Key, pair.Value)).ToArray();
        network.SendAll(new NetworkGangLeaderWeaponsState(giverId, generation, quest.StringId, guardsId,
            quest._collectedItemAmount, quest._playerDodgedGuards, quest._lowCrimeRatingWillBeApplied,
            quest._highCrimeRatingWillBeApplied, quest._persuasionTriedOnce, confiscated, action, accepted));
    }

    private void HandleState(MessagePayload<NetworkGangLeaderWeaponsState> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!TryGetQuest(data.GiverId, data.Generation, data.QuestId, out var quest)) return;
            MobileParty guards = null;
            if (data.GuardsPartyId != null && !objects.TryGetObjectWithLogging(data.GuardsPartyId, out guards)) return;
            using (new AllowedThread())
            {
                quest._guardsParty = guards;
                quest._playerDodgedGuards = data.DodgedGuards;
                quest._lowCrimeRatingWillBeApplied = data.LowCrime;
                quest._highCrimeRatingWillBeApplied = data.HighCrime;
                quest._persuasionTriedOnce = data.PersuasionTried;
                if (data.Action == GangLeaderWeaponsAction.BattleWon) quest._checkForBattleResult = false;
                quest._weaponsThatGuardTook.Clear();
                foreach (var item in data.ConfiscatedWeapons ?? System.Array.Empty<ItemRosterElement>())
                {
                    quest._weaponsThatGuardTook[item.EquipmentElement] = item.Amount;
                }
                quest.SetCurrentItemAmount(data.CollectedAmount);
                if (data.Action == GangLeaderWeaponsAction.EnterTown && pendingGuardEntries.Remove(quest) && data.ActionAccepted &&
                    ownership.IsLocalPeerOwner(quest.QuestGiver) &&
                    MobileParty.MainParty.CurrentSettlement == quest.QuestGiver.CurrentSettlement)
                {
                    quest.OnSettlementEnter(MobileParty.MainParty, quest.QuestGiver.CurrentSettlement, Hero.MainHero);
                }
            }
            if (data.Action == GangLeaderWeaponsAction.BeginBattle && pendingBattle == quest && pendingBattleRequestId == null)
            {
                if (!data.ActionAccepted || !ownership.IsLocalPeerOwner(quest.QuestGiver) || guards == null ||
                    MobileParty.MainParty.CurrentSettlement != quest.QuestGiver.CurrentSettlement)
                {
                    pendingBattle = null;
                    if (data.ActionAccepted)
                        broker.Publish(quest, new GangLeaderWeaponsActionRequested(quest, GangLeaderWeaponsAction.CancelBattleStart));
                    return;
                }
                pendingBattleRequestId = System.Guid.NewGuid().ToString("N");
                broker.Publish(quest, new ConversationRequested(guards.Party, PartyBase.MainParty, false,
                    ConversationRestartSource.PlayerEncounter, false, pendingBattleRequestId));
            }
        });
    }

    private void HandleEncounterApproved(MessagePayload<ConversationRestartApproved> payload)
    {
        if (ModInformation.IsServer || pendingBattleRequestId == null ||
            payload.What.RequestId != pendingBattleRequestId || pendingBattle == null) return;
        var quest = pendingBattle;
        pendingBattle = null;
        pendingBattleRequestId = null;
        var started = false;
        try
        {
            if (quest.IsOngoing && ownership.IsLocalPeerOwner(quest.QuestGiver) &&
                payload.What.Defender == quest._guardsParty?.Party && payload.What.Attacker == PartyBase.MainParty)
                started = actions.StartApprovedBattle(quest);
        }
        finally
        {
            if (!started)
                broker.Publish(quest, new GangLeaderWeaponsActionRequested(quest, GangLeaderWeaponsAction.CancelBattleStart));
        }
    }

    private void HandleEncounterRejected(MessagePayload<ConversationRestartRejected> payload)
    {
        if (ModInformation.IsServer || pendingBattle == null || payload.What.RequestId != pendingBattleRequestId) return;
        var quest = pendingBattle;
        pendingBattle = null;
        pendingBattleRequestId = null;
        broker.Publish(quest, new GangLeaderWeaponsActionRequested(quest, GangLeaderWeaponsAction.CancelBattleStart));
    }

    private void HandleLogAdded(MessagePayload<GangLeaderWeaponsLogAdded> payload)
    {
        if (ModInformation.IsClient) return;
        var data = payload.What;
        if (!objects.TryGetIdWithLogging(data.Quest.QuestGiver, out var giverId) ||
            !generations.TryGetGeneration(data.Quest.QuestGiver, out var generation)) return;
        network.SendAll(new NetworkGangLeaderWeaponsLog(giverId, generation, data.Quest.StringId,
            data.Quest._journalEntries.IndexOf(data.Log), data.Log.LogTime, data.Log.LogText, data.HideInformation));
    }

    private void HandleQuestEnded(MessagePayload<GangLeaderWeaponsQuestEnded> payload)
    {
        pendingGuardEntries.Remove(payload.What.Quest);
        if (pendingBattle != payload.What.Quest) return;
        pendingBattle = null;
        pendingBattleRequestId = null;
    }

    private void HandleNetworkLog(MessagePayload<NetworkGangLeaderWeaponsLog> payload)
    {
        if (ModInformation.IsServer) return;
        var data = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!TryGetQuest(data.GiverId, data.Generation, data.QuestId, out var quest) ||
                quest._journalEntries.Count != data.Index) return;
            using (new AllowedThread())
            {
                quest._journalEntries.Add(new JournalLog(data.Time, data.Text));
                CampaignEventDispatcher.Instance.OnQuestLogAdded(quest,
                    data.HideInformation || !ownership.IsLocalPeerOwner(quest.QuestGiver));
            }
        });
    }

    private void HandleStateChanged(MessagePayload<GangLeaderWeaponsStateChanged> payload)
    {
        if (ModInformation.IsClient) return;
        var quest = payload.What.Quest;
        if (!quest.IsOngoing || !objects.TryGetIdWithLogging(quest.QuestGiver, out var giverId) ||
            !generations.TryGetGeneration(quest.QuestGiver, out var generation)) return;
        PublishState(quest, giverId, generation, payload.What.Action, true);
    }

    private void HandleBattleResults(MessagePayload<MapEventResultsCommitted> payload)
    {
        if (ModInformation.IsClient) return;
        foreach (var entry in ownership.Snapshot())
        {
            if (entry.Key.Issue?.IssueQuest is not Quest quest || !quest.IsOngoing ||
                !players.TryGetPlayer(entry.Value, out var player) || !TryGetOwner(player, out var hero, out var party)) continue;
            if (actions.CompleteBattle(quest, hero, party, payload.What.Battle) && quest.IsOngoing &&
                objects.TryGetIdWithLogging(entry.Key, out var giverId) && generations.TryGetGeneration(entry.Key, out var generation))
                PublishState(quest, giverId, generation, GangLeaderWeaponsAction.BattleWon, true);
        }
    }
}
