using Common.Util;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Wraps the reflection/publicized-field access <see cref="Patches.IssueManagerCreateNewIssuePatches"/> and
/// <see cref="Handlers.VillageNeedsToolsIssueHandler"/> need to capture and authoritatively replicate a
/// <see cref="VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue"/> without letting each client's own
/// constructor call re-roll its terms.
/// </summary>
public interface IVillageNeedsToolsIssueInterface : IGameAbstraction
{
    /// <summary>
    /// Reads the four rolled fields off an already-constructed issue (direct field access - see
    /// TaleWorlds.CampaignSystem's whole-assembly Publicize in GameInterface.csproj). Returns false if the
    /// issue has no requested item, which should never happen for a real instance.
    /// </summary>
    bool TryCaptureFields(
        VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue,
        out ItemObject requestedItem,
        out ItemObject exchangeItem,
        out int numberOfExchangeItem,
        out int numberOfRequestedItem,
        out int payment);

    /// <summary>
    /// Builds a <see cref="VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue"/> via its normal
    /// constructor, then overwrites its four derived (and otherwise client-divergent) readonly fields with
    /// the server's authoritative values via reflection. Does not register it with the
    /// <see cref="IssueManager"/> - see <see cref="RegisterReplicated"/>.
    /// </summary>
    VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue ConstructReplicated(
        Hero owner,
        ItemObject requestedItem,
        ItemObject exchangeItem,
        int numberOfExchangeItem,
        int numberOfRequestedItem,
        int payment);

    /// <summary>
    /// Registers an already-built, already-forced issue instance with <see cref="Campaign.Current"/>'s
    /// <see cref="IssueManager"/>, replaying <c>IssueManager.CreateNewIssue</c>'s own bookkeeping (StringId
    /// assignment, <c>AfterCreation</c>, dictionary add, <c>Hero.OnIssueCreatedForHero</c>, tracked-object
    /// registration, the <c>OnNewIssueCreated</c> event) via a custom <see cref="PotentialIssueData"/> whose
    /// <c>OnStartIssue</c> hands back <paramref name="issue"/> instead of constructing (and re-rolling) a
    /// new one.
    /// </summary>
    void RegisterReplicated(Hero owner, VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue);

    /// <summary>
    /// Mirrors a received issue-removal broadcast (or replays a locally-requested one on the server) by
    /// calling the owner's current issue's <c>IssueFinalized</c> under an <see cref="AllowedThread"/> scope.
    /// A no-op if the hero has no issue (already finalized locally, or a stale/duplicate message).
    /// </summary>
    void FinalizeMirror(Hero owner);
}

/// <inheritdoc cref="IVillageNeedsToolsIssueInterface"/>
public class VillageNeedsToolsIssueInterface : IVillageNeedsToolsIssueInterface
{
    private static readonly FieldInfo ExchangeItemField =
        AccessTools.Field(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), "_exchangeItem");
    private static readonly FieldInfo NumberOfExchangeItemField =
        AccessTools.Field(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), "_numberOfExchangeItem");
    private static readonly FieldInfo NumberOfRequestedItemField =
        AccessTools.Field(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), "_numberOfRequestedItem");
    private static readonly FieldInfo PaymentField =
        AccessTools.Field(typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), "_payment");

    public bool TryCaptureFields(
        VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue,
        out ItemObject requestedItem,
        out ItemObject exchangeItem,
        out int numberOfExchangeItem,
        out int numberOfRequestedItem,
        out int payment)
    {
        requestedItem = null;
        exchangeItem = null;
        numberOfExchangeItem = 0;
        numberOfRequestedItem = 0;
        payment = 0;
        if (issue == null) return false;

        requestedItem = issue._requestedItem;
        exchangeItem = issue._exchangeItem;
        numberOfExchangeItem = issue._numberOfExchangeItem;
        numberOfRequestedItem = issue._numberOfRequestedItem;
        payment = issue._payment;

        return requestedItem != null;
    }

    public VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue ConstructReplicated(
        Hero owner,
        ItemObject requestedItem,
        ItemObject exchangeItem,
        int numberOfExchangeItem,
        int numberOfRequestedItem,
        int payment)
    {
        // The public ctor is the only way to build one, and it independently re-derives these same four
        // fields from IssueDifficultyMultiplier (Campaign.Current.PlayerProgress - a per-client value) and
        // the village's live Hearth. Build it the normal way for everything else it sets up (SaveableField
        // wiring, base IssueBase state), then force these four `readonly` fields (hence the reflection) to
        // the server's authoritative rolls so every client ends up with byte-identical quest terms.
        var issue = new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(owner, requestedItem);

        ExchangeItemField.SetValue(issue, exchangeItem);
        NumberOfExchangeItemField.SetValue(issue, numberOfExchangeItem);
        NumberOfRequestedItemField.SetValue(issue, numberOfRequestedItem);
        PaymentField.SetValue(issue, payment);

        return issue;
    }

    public void RegisterReplicated(Hero owner, VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue issue)
    {
        PotentialIssueData.StartIssueDelegate factory = (in PotentialIssueData _, Hero _owner) => issue;
        var pid = new PotentialIssueData(factory, typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue), IssueBase.IssueFrequency.VeryCommon);

        using (new AllowedThread())
        {
            Campaign.Current.IssueManager.CreateNewIssue(in pid, owner);
        }
    }

    public void FinalizeMirror(Hero owner)
    {
        if (owner?.Issue == null) return;

        using (new AllowedThread())
        {
            owner.Issue.IssueFinalized();
        }
    }
}
