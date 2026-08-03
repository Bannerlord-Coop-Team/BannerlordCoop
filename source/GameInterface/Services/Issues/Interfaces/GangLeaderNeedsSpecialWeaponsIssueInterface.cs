using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.GangLeaderNeedsSpecialWeaponsAcceptancePatches"/>/
/// <see cref="Handlers.GangLeaderNeedsSpecialWeaponsIssueHandler"/> need to capture and authoritatively
/// force-write a genuine accept's terms onto every other peer's mirrored
/// <c>GangLeaderNeedsSpecialWeaponsIssueQuest</c>.
///
/// The survey's flag - <c>GetCraftingDifficulty()</c> rolling an unseeded <c>MBRandom.RandomInt(-10, 10)</c> on
/// EVERY crafting order, not just once at accept - is real, but needs no capture/re-sync mechanism at all:
/// <c>GetDaggerCraftingOrder()</c> (the only caller) is reached exclusively from <c>QuestAcceptedConsequences</c>
/// (a live dialogue Consequence, only ever runs on the real accepter's machine - never replayed) and
/// <c>OnCraftingOrderCompleted</c> (a <c>RegisterEvents</c>-subscribed listener, and <c>RegisterEvents</c> is
/// only ever called from <c>QuestBase.StartQuest()</c>, itself only reachable from that same live dialogue
/// Consequence - see <see cref="IBettingFraudIssueInterface"/>'s doc comment for the full derivation of why
/// this project's mirror mechanism never re-runs a quest's "just accepted" side effects on a non-accepting
/// peer). So every crafting-order roll, no matter how many times it fires over the quest's life, only ever
/// happens on the one machine that actually owns this quest - there is no cross-peer divergence to fix.
///
/// What DOES need capturing: <c>_numberOfDaggersRequested</c> (the Quest ctor PARAMETER, supplied by
/// <c>GangLeaderNeedsSpecialWeaponsIssue.GenerateIssueQuest</c> from its own <c>NumberOfDaggersRequested</c>
/// computed property, which reads <c>base.IssueDifficultyMultiplier</c>) - re-derived at ACCEPT time the same
/// way Gang Leader Needs Recruits'/Landowner Needs Manual Laborers' equivalents are (see those types' own doc
/// comments), since <c>IssueBase.StartIssueWithQuest</c> unconditionally re-rolls
/// <c>_issueDifficultyMultiplier</c> immediately before calling <c>GenerateIssueQuest</c>. This quota gates
/// <c>CheckPlayerHasCompletedEnoughOrdersClickableCondition</c> (the turn-in check) - but that check ALSO needs
/// no explicit ownership gate, for the same reason as above: <c>_completedCraftingOrders</c> only ever
/// increments via the owner-only <c>OnCraftingOrderCompleted</c> listener, so it stays 0 forever on every other
/// peer's mirror and the Condition is self-gating without one. <c>RewardGold</c> is not overridden by this
/// issue type (base default, always 0 - confirmed by <c>SucceedQuest()</c> never calling
/// <c>GiveGoldAction</c>), so there is no reward-divergence risk either.
/// </summary>
public interface IGangLeaderNeedsSpecialWeaponsIssueInterface : IGameAbstraction
{
    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(Hero owner, out int numberOfDaggersRequested);

    void MirrorQuestAccepted(Hero owner, int numberOfDaggersRequested);
}

/// <inheritdoc cref="IGangLeaderNeedsSpecialWeaponsIssueInterface"/>
public class GangLeaderNeedsSpecialWeaponsIssueInterface : IGangLeaderNeedsSpecialWeaponsIssueInterface
{
    private static readonly FieldInfo NumberOfDaggersRequestedField =
        AccessTools.Field(typeof(GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssueQuest), "_numberOfDaggersRequested");

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out int numberOfDaggersRequested)
    {
        numberOfDaggersRequested = 0;

        if (owner?.Issue?.IssueQuest is not GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssueQuest quest) return false;

        numberOfDaggersRequested = (int)NumberOfDaggersRequestedField.GetValue(quest);
        return true;
    }

    public void MirrorQuestAccepted(Hero owner, int numberOfDaggersRequested)
    {
        if (owner?.Issue is not GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssueQuest quest) return;

            NumberOfDaggersRequestedField.SetValue(quest, numberOfDaggersRequested);
        }
    }
}
