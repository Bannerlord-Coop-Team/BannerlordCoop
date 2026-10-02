using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Messages;
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
    }

    public void Dispose()
    {
        broker.Unsubscribe<GangLeaderWeaponsIssueCreated>(HandleCreated);
        broker.Unsubscribe<NetworkGangLeaderWeaponsIssueCreated>(HandleNetworkCreated);
        broker.Unsubscribe<GangLeaderWeaponsActionRequested>(HandleActionRequested);
        broker.Unsubscribe<RequestGangLeaderWeaponsAction>(HandleNetworkAction);
        broker.Unsubscribe<NetworkGangLeaderWeaponsState>(HandleState);
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
            if (!actions.Apply(quest, hero, party, data.Action) || !quest.IsOngoing) return;
            PublishState(quest, data.GiverId, data.Generation, data.Action);
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

    private void PublishState(Quest quest, string giverId, int generation, GangLeaderWeaponsAction action)
    {
        string guardsId = null;
        if (quest._guardsParty != null && quest._guardsParty.IsActive &&
            !objects.TryGetIdWithLogging(quest._guardsParty, out guardsId)) return;
        var confiscated = quest._weaponsThatGuardTook.Select(pair => new ItemRosterElement(pair.Key, pair.Value)).ToArray();
        network.SendAll(new NetworkGangLeaderWeaponsState(giverId, generation, quest.StringId, guardsId,
            quest._collectedItemAmount, quest._playerDodgedGuards, quest._lowCrimeRatingWillBeApplied,
            quest._highCrimeRatingWillBeApplied, quest._persuasionTriedOnce, confiscated, action));
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
                quest._weaponsThatGuardTook.Clear();
                foreach (var item in data.ConfiscatedWeapons ?? System.Array.Empty<ItemRosterElement>())
                {
                    quest._weaponsThatGuardTook[item.EquipmentElement] = item.Amount;
                }
                quest.SetCurrentItemAmount(data.CollectedAmount);
                if (data.Action == GangLeaderWeaponsAction.EnterTown && pendingGuardEntries.Remove(quest) &&
                    ownership.IsLocalPeerOwner(quest.QuestGiver) &&
                    MobileParty.MainParty.CurrentSettlement == quest.QuestGiver.CurrentSettlement)
                {
                    quest.OnSettlementEnter(MobileParty.MainParty, quest.QuestGiver.CurrentSettlement, Hero.MainHero);
                }
            }
        });
    }
}
