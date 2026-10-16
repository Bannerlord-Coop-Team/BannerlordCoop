using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Messages;
using System;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Handlers;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

internal sealed partial class GangLeaderWeaponsIssueHandler
{
    private GangLeaderWeaponsPersuasionOptionRequested? pendingPersuasion;
    private Persuasion pendingPersuasionProgress;
    private GangLeaderWeaponsAction pendingPersuasionAction;

    private void HandlePersuasionOption(MessagePayload<GangLeaderWeaponsPersuasionOptionRequested> payload)
    {
        var request = payload.What;
        var quest = request.Quest;
        if (ModInformation.IsServer || !quest.IsOngoing || !ownership.IsLocalPeerOwner(quest.QuestGiver) ||
            !ConversationManager.GetPersuasionIsActive() ||
            (pendingPersuasion.HasValue && pendingPersuasionProgress == ConversationManager._persuasion)) return;
        var option = request.Conversation.CurOptions[request.OptionIndex].PersuationOptionArgs;
        var optionIndex = quest._task.Options.IndexOf(option);
        if (optionIndex < 0 || optionIndex > 2 || option.IsBlocked) return;

        pendingPersuasion = request;
        pendingPersuasionProgress = ConversationManager._persuasion;
        pendingPersuasionAction = GangLeaderWeaponsAction.PersuasionOption1 + optionIndex;
        broker.Publish(quest, new GangLeaderWeaponsActionRequested(quest, pendingPersuasionAction));
    }

    private void ApplyPersuasionState(Quest quest, NetworkGangLeaderWeaponsState state)
    {
        if (!pendingPersuasion.HasValue || pendingPersuasion.Value.Quest != quest ||
            state.Action != pendingPersuasionAction) return;
        var request = pendingPersuasion.Value;
        pendingPersuasion = null;
        if (pendingPersuasionProgress != ConversationManager._persuasion ||
            !ownership.IsLocalPeerOwner(quest.QuestGiver) || !request.Conversation.IsConversationInProgress ||
            request.OptionIndex >= request.Conversation.CurOptions.Count) return;
        var option = request.Conversation.CurOptions[request.OptionIndex].PersuationOptionArgs;
        var expected = (int)state.Action - (int)GangLeaderWeaponsAction.PersuasionOption1;
        if (quest._task.Options.IndexOf(option) != expected) return;
        if (!state.ActionAccepted || state.Persuasion == null)
        {
            PlayerEncounter.LeaveEncounter = false;
            request.Conversation.EndConversation();
            return;
        }

        pendingPersuasionProgress.Progress = state.Persuasion.Progress;
        pendingPersuasionProgress._chosenOptions.Add(Tuple.Create(option, state.Persuasion.Result));
        for (var index = 0; index < quest._task.Options.Count; index++)
            quest._task.Options[index].BlockTheOption(state.Persuasion.BlockedOptions[index]);
        using (new GangLeaderWeaponsActionScope(quest))
            request.Conversation.DoOption(request.OptionIndex);
    }
}
