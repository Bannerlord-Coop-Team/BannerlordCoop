using Common;
using Common.Util;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.Issues.Interfaces;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

public interface IArtisanAlternativeSelection
{
    void Begin(Issue issue, TroopRoster party);
    void Restore(Hero giver);
    void Finish(Hero giver);
}

internal sealed class ArtisanAlternativeSelection : IArtisanAlternativeSelection
{
    private readonly Dictionary<Hero, (Issue Issue, TroopRoster Party)> selections = new();

    public void Begin(Issue issue, TroopRoster party)
    {
        if (ModInformation.IsServer || issue == null || party == null) return;
        Restore(issue.IssueOwner);
        selections[issue.IssueOwner] = (issue, party);
    }

    public void Restore(Hero giver)
    {
        if (ModInformation.IsServer || giver == null || !selections.TryGetValue(giver, out var selection)) return;
        selections.Remove(giver);
        // Undo the local selection before the authoritative roster removal arrives.
        using (new AllowedThread()) selection.Party.Add(selection.Issue.AlternativeSolutionSentTroops);
    }

    public void Finish(Hero giver)
    {
        Restore(giver);
        if (ModInformation.IsServer || giver?.Issue is not Issue issue || !issue.IsOngoingWithoutQuest) return;
        using (new AllowedThread()) issue.AlternativeSolutionSentTroops.Clear();
    }
}
