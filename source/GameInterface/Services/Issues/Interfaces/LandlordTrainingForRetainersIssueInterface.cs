using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.LandlordTrainingForRetainersAcceptancePatches"/>/
/// <see cref="Handlers.LandlordTrainingForRetainersIssueHandler"/> need to capture and authoritatively
/// force-write a genuine accept's terms onto every other peer's mirrored
/// <see cref="LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssueQuest"/> - the survey's
/// "flat formula reward, no MBRandom anywhere in file" characterization missed that the flat formula's INPUT,
/// <c>base.IssueDifficultyMultiplier</c>, is read INSIDE <c>GenerateIssueQuest</c> (called from
/// <c>IssueBase.StartIssueWithQuest</c>, i.e. at ACCEPT time) - and <c>StartIssueWithQuest</c> itself re-rolls
/// <c>_issueDifficultyMultiplier</c> fresh from <c>Campaign.Current.Models.IssueModel.GetIssueDifficultyMultiplier()</c>
/// immediately before calling it (see <c>IssueBase.StartIssueWithQuest</c> in the decompiled source) - a
/// genuinely per-client value, the exact same mechanism Village Needs Crafting Materials needed its own
/// accept-time force-write for. Both <c>RewardGold</c> AND the quest's own <c>_difficultyMultiplier</c> field
/// (which the quest re-derives <c>_borrowedTroopCount</c> from, controlling how many troops get borrowed AND
/// how many must be returned to succeed) are captured from whichever machine's accept is genuine and
/// force-written onto every other peer's mirrored quest object - a bare replay of <c>StartIssueQuest</c> alone
/// (the way e.g. Lord Needs Horses gets away with) would instead let each peer independently and differently
/// re-roll both.
/// </summary>
public interface ILandlordTrainingForRetainersIssueInterface : IGameAbstraction
{
    /// <summary>Replays <see cref="IssueManager.StartIssueQuest"/> for a hero whose issue is still
    /// <c>IsOngoingWithoutQuest</c>, under <see cref="AllowedThread"/>. Used ONLY by the server while
    /// arbitrating a client's accept request, mirroring <c>VillageNeedsCraftingMaterialsIssueInterface.ReplayQuestAccepted</c>'s
    /// reasoning exactly.</summary>
    void ReplayQuestAccepted(Hero owner);

    /// <summary>Reads back <c>_difficultyMultiplier</c>/<c>RewardGold</c> off <paramref name="owner"/>'s
    /// current quest right after a GENUINE accept, for broadcasting.</summary>
    bool TryCaptureQuestFields(Hero owner, out float difficultyMultiplier, out int rewardGold);

    /// <summary>Ensures <paramref name="owner"/> has a real quest object (replaying accept if needed), then
    /// force-writes <paramref name="difficultyMultiplier"/>/<paramref name="rewardGold"/> onto it. Idempotent.</summary>
    void MirrorQuestAccepted(Hero owner, float difficultyMultiplier, int rewardGold);
}

/// <inheritdoc cref="ILandlordTrainingForRetainersIssueInterface"/>
public class LandlordTrainingForRetainersIssueInterface : ILandlordTrainingForRetainersIssueInterface
{
    private static readonly FieldInfo DifficultyMultiplierField =
        AccessTools.Field(typeof(LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssueQuest), "_difficultyMultiplier");
    private static readonly FieldInfo RewardGoldField = AccessTools.Field(typeof(QuestBase), nameof(QuestBase.RewardGold));

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out float difficultyMultiplier, out int rewardGold)
    {
        difficultyMultiplier = 0f;
        rewardGold = 0;

        if (owner?.Issue?.IssueQuest is not LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssueQuest quest) return false;

        difficultyMultiplier = (float)DifficultyMultiplierField.GetValue(quest);
        rewardGold = quest.RewardGold;
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, float difficultyMultiplier, int rewardGold)
    {
        if (owner?.Issue is not LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssueQuest quest) return;

            DifficultyMultiplierField.SetValue(quest, difficultyMultiplier);
            RewardGoldField.SetValue(quest, rewardGold);
        }
    }
}
