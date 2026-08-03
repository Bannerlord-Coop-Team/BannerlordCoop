using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection access <see cref="Patches.BettingFraudAcceptancePatches"/>/
/// <see cref="Handlers.BettingFraudIssueHandler"/> need to capture and authoritatively force-write a genuine
/// accept's terms onto every other peer's mirrored <c>BettingFraudQuest</c>.
///
/// The Quest's own ctor rolls <c>_thug = MBObjectManager.Instance.GetObject&lt;CharacterObject&gt;(MBRandom.RandomFloat
/// &gt; 0.5f ? "betting_fraud_thug_male" : "betting_fraud_thug_female")</c> at ACCEPT time - captured and
/// force-written here for the same "every rolled field gets replicated" diligence this whole quest family
/// follows, even though (see below) it turns out to carry no real cross-peer risk on its own.
///
/// The survey's flagged concern - <c>SetCurrentDirective()</c>'s <c>MBRandom.RandomFloat</c> re-firing inside a
/// live <c>DialogFlow.Condition()</c> delegate, potentially once per tournament rather than once at accept -
/// genuinely does NOT fit the standard one-shot capture-at-accept pattern, but it ALSO needs no capture,
/// periodic re-sync, or even an explicit ownership gate at all. The reason: <c>BettingFraudQuest.RegisterEvents()</c>
/// (which subscribes <c>PlayerEliminatedFromTournament</c>/<c>TournamentFinished</c>/<c>GameMenuOpened</c> -
/// the ONLY code paths that ever open a conversation with <c>_thug</c> or <c>_counterOfferNotable</c>, populate
/// <c>_counterOfferNotable</c>, or advance <c>_fixedTournamentCount</c>/<c>_afterTournamentConversationState</c>)
/// is called EXCLUSIVELY from <c>QuestBase.StartQuest()</c>, which itself only ever runs inside
/// <c>OfferDialogFlowConsequence</c> - a live dialogue Consequence that fires ONLY on the machine whose own
/// conversation genuinely accepted (never replayed on a mirroring peer - see
/// <see cref="VillageNeedsToolsIssueInterface"/>'s own type doc comment for why mirror peers never re-run a
/// quest's "just accepted" consequence). So on every OTHER peer, this quest's mirrored
/// <c>BettingFraudQuest</c> instance NEVER subscribes to those events at all - the entire tournament-fixing
/// gameplay loop (directives, thug/counter-offer conversations, win/lose bookkeeping) is architecturally
/// confined to the one machine that actually accepted, with nothing for another peer to independently trigger.
/// The vanilla <c>DiscussDialogFlow</c> WITH the quest giver (the one dialogue every peer's mirror DOES
/// register, since <c>SetDialogs()</c> runs unconditionally from the ctor) has zero state-mutating
/// <c>Consequence</c> calls at all - pure flavor branches - so there is no exploit there either. This is the
/// "genuinely different mechanism" case: the fix that actually matters is just the accept-time <c>_thug</c>
/// capture below; the directive itself never crosses a peer boundary.
/// </summary>
public interface IBettingFraudIssueInterface : IGameAbstraction
{
    void ReplayQuestAccepted(Hero owner);

    bool TryCaptureQuestFields(Hero owner, out CharacterObject thug);

    void MirrorQuestAccepted(Hero owner, CharacterObject thug);
}

/// <inheritdoc cref="IBettingFraudIssueInterface"/>
public class BettingFraudIssueInterface : IBettingFraudIssueInterface
{
    private static readonly FieldInfo ThugField =
        AccessTools.Field(typeof(BettingFraudIssueBehavior.BettingFraudQuest), "_thug");

    public void ReplayQuestAccepted(Hero owner)
    {
        if (owner?.Issue is not BettingFraudIssueBehavior.BettingFraudIssue || !owner.Issue.IsOngoingWithoutQuest) return;

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.StartIssueQuest(owner);
        }
    }

    public bool TryCaptureQuestFields(Hero owner, out CharacterObject thug)
    {
        thug = null;

        if (owner?.Issue?.IssueQuest is not BettingFraudIssueBehavior.BettingFraudQuest quest) return false;

        thug = (CharacterObject)ThugField.GetValue(quest);
        return thug != null;
    }

    public void MirrorQuestAccepted(Hero owner, CharacterObject thug)
    {
        if (owner?.Issue is not BettingFraudIssueBehavior.BettingFraudIssue) return;

        using (new AllowedThread())
        {
            if (owner.Issue.IsOngoingWithoutQuest)
            {
                Campaign.Current.IssueManager.StartIssueQuest(owner);
            }

            if (owner.Issue.IssueQuest is not BettingFraudIssueBehavior.BettingFraudQuest quest) return;

            ThugField.SetValue(quest, thug);
        }
    }
}
