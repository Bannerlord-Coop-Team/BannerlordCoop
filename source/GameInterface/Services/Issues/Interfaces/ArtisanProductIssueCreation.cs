using Common.Util;
using GameInterface.Services.Issues.Generic.CreationCapture;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue;

internal interface IArtisanProductIssueCreation
{
    bool TryCapture(Issue issue, out ArtisanProductIssueFields fields);
    void Apply(Hero owner, ArtisanProductIssueFields fields);
}

internal sealed class ArtisanProductIssueCreation : IArtisanProductIssueCreation,
    ICreationCaptureStrategy<Issue, ArtisanProductIssueFields>
{
    public bool TryCapture(Issue issue, out ArtisanProductIssueFields fields)
    {
        fields = default;
        if (issue == null || issue._targetSettlement == null || issue._targetHero == null ||
            issue._rawMaterialsToBeDelivered == null || issue.CounterOfferHero == null) return false;

        fields = new ArtisanProductIssueFields(issue._targetSettlement, issue._targetHero,
            issue._rawMaterialsToBeDelivered, issue.CounterOfferHero, issue.IssueDueTime,
            issue.StringId, Campaign.Current.IssueManager._nextIssueUniqueIndex);
        return true;
    }

    bool ICreationCaptureStrategy<Issue, ArtisanProductIssueFields>.TryCaptureFields(
        Issue issue, out ArtisanProductIssueFields fields) => TryCapture(issue, out fields);

    public Issue ConstructReplicated(Hero owner, ArtisanProductIssueFields fields)
    {
        // The receive path supplies the destination before the vanilla constructor dereferences it.
        using (new ArtisanProductDestinationScope(fields.TargetSettlement))
        {
            return new Issue(owner)
            {
                _targetSettlement = fields.TargetSettlement,
                _targetHero = fields.TargetHero,
                _rawMaterialsToBeDelivered = fields.Item,
                CounterOfferHero = fields.CounterOfferHero,
                IssueDueTime = fields.DueTime,
            };
        }
    }

    public void Apply(Hero owner, ArtisanProductIssueFields fields)
    {
        var capture = new CreationCaptureRunner<Issue, ArtisanProductIssueFields>(this, IssueBase.IssueFrequency.Common);
        capture.ConstructAndRegisterReplicated(owner, fields, (issue, data) =>
        {
            issue.StringId = data.IssueId;
            Campaign.Current.IssueManager._nextIssueUniqueIndex =
                Math.Max(Campaign.Current.IssueManager._nextIssueUniqueIndex, data.NextIssueIndex);
        });
    }
}

internal readonly struct ArtisanProductIssueFields
{
    public readonly Settlement TargetSettlement;
    public readonly Hero TargetHero;
    public readonly ItemObject Item;
    public readonly Hero CounterOfferHero;
    public readonly CampaignTime DueTime;
    public readonly string IssueId;
    public readonly int NextIssueIndex;

    public ArtisanProductIssueFields(Settlement targetSettlement, Hero targetHero, ItemObject item,
        Hero counterOfferHero, CampaignTime dueTime, string issueId, int nextIssueIndex)
    {
        TargetSettlement = targetSettlement;
        TargetHero = targetHero;
        Item = item;
        CounterOfferHero = counterOfferHero;
        DueTime = dueTime;
        IssueId = issueId;
        NextIssueIndex = nextIssueIndex;
    }
}

internal sealed class ArtisanProductDestinationScope : IDisposable
{
    [ThreadStatic]
    private static Settlement destination;
    private readonly Settlement previous;

    internal static Settlement Destination => destination;

    internal ArtisanProductDestinationScope(Settlement target)
    {
        previous = destination;
        destination = target;
    }

    public void Dispose() => destination = previous;
}
