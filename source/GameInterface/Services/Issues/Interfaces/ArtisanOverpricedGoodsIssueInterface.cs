using GameInterface.Services.Issues.Generic.CreationCapture;
using GameInterface.Services.Issues.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

public interface IArtisanOverpricedGoodsIssueInterface :
    ICreationCaptureStrategy<Issue, (ItemObject Item, Hero CounterOfferHero, ArtisanOverpricedGoodsIssueValues Values)>
{
    ArtisanOverpricedGoodsIssueValues CaptureValues(Issue issue);
    void ApplyValues(Issue issue, ArtisanOverpricedGoodsIssueValues values);
}

internal sealed class ArtisanOverpricedGoodsIssueInterface : IArtisanOverpricedGoodsIssueInterface
{
    public bool TryCaptureFields(Issue issue,
        out (ItemObject Item, Hero CounterOfferHero, ArtisanOverpricedGoodsIssueValues Values) fields)
    {
        fields = default;
        if (issue?._requestedTradeGood == null || issue.CounterOfferHero == null) return false;

        fields = (issue._requestedTradeGood, issue.CounterOfferHero, CaptureValues(issue));
        return true;
    }

    public ArtisanOverpricedGoodsIssueValues CaptureValues(Issue issue) => new(
        issue.RequestedTradeGoodAmount, issue._goldReward, issue.IssueDifficultyMultiplier,
        issue.IssueCreationTime, issue.IssueDueTime, issue.StringId);

    public void ApplyValues(Issue issue, ArtisanOverpricedGoodsIssueValues values)
    {
        // The reward depends on world prices, which may differ while a client catches up.
        issue.RequestedTradeGoodAmount = values.RequestedAmount;
        issue._goldReward = values.RewardGold;
        issue._issueDifficultyMultiplier = values.Difficulty;
        issue.IssueCreationTime = values.CreationTime;
        issue.IssueDueTime = values.DueTime;
        issue.StringId = values.IssueId;
    }

    public Issue ConstructReplicated(Hero owner,
        (ItemObject Item, Hero CounterOfferHero, ArtisanOverpricedGoodsIssueValues Values) fields)
    {
        var issue = new Issue(owner, fields.CounterOfferHero, fields.Item);
        ApplyValues(issue, fields.Values);
        return issue;
    }
}
