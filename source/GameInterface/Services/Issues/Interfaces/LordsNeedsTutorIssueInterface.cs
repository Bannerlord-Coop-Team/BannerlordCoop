using Common.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the access <see cref="Patches.LordsNeedsTutorIssueCreationPatch"/>/
/// <see cref="Handlers.LordsNeedsTutorIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue"/>.
///
/// CREATION: the Issue ctor takes <c>(Hero issueOwner, Hero youngHero)</c> DIRECTLY, and <c>_youngHero</c> is a
/// plain (Publicize-accessible) private field - the same "no reflection needed at all" shape as
/// <c>ArtisanOverpricedGoodsIssueInterface</c>/<c>SmugglersIssueInterface</c>. The pick itself
/// (<c>LordsNeedsTutorIssueBehavior.ConditionsHold</c>: <c>issueGiver.Clan.AliveLords.FirstOrDefault(SuitableCondition)</c>)
/// is a real, order-dependent roll against live per-clan hero state - NOT a pure function of the owner alone
/// (unlike e.g. <c>LandLordTheArtOfTheTradeIssue</c>'s primary-production derivation in
/// <c>SimpleIssueFactoryRegistry</c>), so this type keeps its own bespoke Interfaces/Messages/Patches/Handler
/// file set instead of that registry, exactly like every other type whose creation needs a captured, not
/// re-derived, field.
///
/// ACCEPT TIME: <c>GenerateIssueQuest</c> forwards <c>IssueOwner</c>/a fixed 200-day duration/<c>_youngHero</c> -
/// all already frozen at creation (captured/forced above) - into
/// <c>LordsNeedsTutorIssueQuest</c>'s own ctor. That ctor DOES roll one fresh value of its own,
/// <c>_randomForQuestReward = MBRandom.RandomInt(2, 5)</c> (how many jewelry pieces the success reward grants) -
/// a genuine per-client-divergent roll if every peer's own bare replay of <c>IssueManager.StartIssueQuest</c>
/// independently re-executes this ctor. Verified this is nonetheless SAFE to leave as a bare, uncorrected
/// replay (this type IS in <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>) because
/// <c>_randomForQuestReward</c> is read in exactly one place, <c>QuestCompletedWithSuccessAfterConversation</c> -
/// and that method is gated to the recorded owner only (see
/// <see cref="Patches.LordsNeedsTutorOwnershipGatePatches"/>), so no OTHER peer's own (differently-rolled) copy
/// of this field is ever consulted or acted upon; it never surfaces in any <c>Title</c>/<c>Description</c>/
/// journal text either (confirmed against the decompiled source). This type has no alternative solution
/// (<c>IsThereAlternativeSolution == false</c>, confirmed against the decompiled source), so it is NOT added to
/// <see cref="GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible"/> at all.
///
/// THE ACTUAL BESPOKE MECHANIC this type needs (the task's core bug, independent of the above): two more
/// private, non-<c>[SaveableField]</c> booleans on the Quest - <c>_doNotForceYoungHeroOutFromClan</c> (set only
/// inside <c>OnHeroesMarried</c>) and <c>_showQuestFailedConversation</c> (set only inside <c>OnTimedOut</c>) -
/// have no sync/persistence mechanism at all, yet <c>OnFinalize</c> reads the former to decide whether to force
/// the (AutoSync'd) <c>_youngHero.Clan</c> back to the quest giver's clan. See
/// <see cref="LordsNeedsTutorQuestFlags"/> for the shadow-table this converges every peer (and every
/// save/reload, and every late-joining peer) onto.
/// </summary>
public interface ILordsNeedsTutorIssueInterface : IGameAbstraction
{
    /// <summary>Reads the one ctor-arg field off an already-constructed issue (direct field read - see the
    /// type doc comment on why no reflection is needed here). Returns false if it's missing, which should
    /// never happen for a real instance.</summary>
    bool TryCaptureFields(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue issue, out Hero youngHero);

    /// <summary>
    /// Builds a <see cref="LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue"/> via its real public ctor, which
    /// takes the captured young hero directly - no field-forcing/reflection needed (see the type doc comment).
    /// Does not register it with the <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue ConstructReplicated(Hero owner, Hero youngHero);

    /// <summary>
    /// Registers an already-built, already-correct issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping via a custom
    /// <see cref="PotentialIssueData"/> whose <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of
    /// constructing (and re-rolling the young hero on) a new one - same technique as every other type in this
    /// family.
    /// </summary>
    void RegisterReplicated(Hero owner, LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue issue);
}

/// <inheritdoc cref="ILordsNeedsTutorIssueInterface"/>
public class LordsNeedsTutorIssueInterface : ILordsNeedsTutorIssueInterface
{
    public bool TryCaptureFields(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue issue, out Hero youngHero)
    {
        youngHero = null;
        if (issue == null) return false;

        youngHero = issue._youngHero;
        return youngHero != null;
    }

    public LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue ConstructReplicated(Hero owner, Hero youngHero)
    {
        return new LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue(owner, youngHero);
    }

    public void RegisterReplicated(Hero owner, LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue), IssueBase.IssueFrequency.Common);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }
}
