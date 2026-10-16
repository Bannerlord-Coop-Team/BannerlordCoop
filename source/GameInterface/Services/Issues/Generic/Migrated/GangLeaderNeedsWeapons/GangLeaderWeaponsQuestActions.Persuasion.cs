using GameInterface.Services.Issues.Messages;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;

using Quest = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssueQuest;

internal sealed partial class GangLeaderWeaponsQuestActions
{
    private readonly ConditionalWeakTable<Quest, GuardPersuasion> persuasions = new();

    private sealed class GuardPersuasion
    {
        public Hero Owner;
        public MobileParty Guards;
        public Persuasion Progress;
    }

    private bool BeginPersuasion(Quest quest, Hero owner)
    {
        if (quest._persuasionTriedOnce) return false;
        var progress = new Persuasion(2f, 1f, 0f, 2f, 2f, 0f, PersuasionDifficulty.VeryHard);
        quest._task = quest.GetPersuasionTask();
        persuasions.Add(quest, new GuardPersuasion { Owner = owner, Guards = quest._guardsParty, Progress = progress });
        quest._persuasionTriedOnce = true;
        return true;
    }

    private bool TryGetPersuasion(Quest quest, Hero owner, out GuardPersuasion session) =>
        persuasions.TryGetValue(quest, out session) && session.Owner == owner && session.Guards == quest._guardsParty;

    private bool PersuasionSucceeded(Quest quest, Hero owner) =>
        TryGetPersuasion(quest, owner, out var session) && session.Progress.Progress >= session.Progress.GoalValue;

    private bool ChoosePersuasionOption(Quest quest, Hero owner, int optionIndex)
    {
        if (!TryGetPersuasion(quest, owner, out var session) || PersuasionSucceeded(quest, owner) ||
            optionIndex < 0 || optionIndex >= quest._task.Options.Count) return false;
        var option = quest._task.Options[optionIndex];
        if (option.IsBlocked) return false;

        session.Progress.CommitProgress(option);
        if (session.Progress.GetChosenOptions().Last().Item2 == PersuasionOptionResult.CriticalFailure)
            quest._task.BlockAllOptions();
        Campaign.Current.Models.PersuasionModel.GetEffectChances(option, out var nextStage, out var blockOption,
            session.Progress.DifficultyMultiplier);
        quest._task.ApplyEffects(nextStage, blockOption);
        return true;
    }

    public GangLeaderWeaponsPersuasionState GetPersuasionState(Quest quest)
    {
        if (!persuasions.TryGetValue(quest, out var session)) return null;
        var last = session.Progress.GetChosenOptions().LastOrDefault();
        return last == null ? null : new GangLeaderWeaponsPersuasionState(session.Progress.Progress,
            last.Item2, quest._task.Options.Select(option => option.IsBlocked).ToArray());
    }
}
