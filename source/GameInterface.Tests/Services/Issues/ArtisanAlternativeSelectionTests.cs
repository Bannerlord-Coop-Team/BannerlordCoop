using Common;
using Common.Util;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Issues;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;

[Collection(ModInformationRoleCollection.Name)]
public class ArtisanAlternativeSelectionTests : IDisposable
{
    private readonly bool previousRole = ModInformation.IsServer;

    public ArtisanAlternativeSelectionTests() => ModInformation.IsServer = false;

    public void Dispose() => ModInformation.IsServer = previousRole;

    private static Issue CreateIssue(Hero giver, TroopRoster selected)
    {
        var issue = ObjectHelper.SkipConstructor<Issue>();
        issue.IssueOwner = giver;
        AccessTools.Field(typeof(IssueBase), nameof(IssueBase.AlternativeSolutionSentTroops)).SetValue(issue, selected);
        return issue;
    }

    [Fact]
    public void RestoringSelectionBeforeServerRemovalDoesNotReturnTroopsTwice()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var troop = new CharacterObject();
        var party = TroopRoster.CreateDummyTroopRoster();
        var selected = TroopRoster.CreateDummyTroopRoster();
        party.AddToCounts(troop, 10);
        var issue = CreateIssue(giver, selected);
        var subject = new ArtisanAlternativeSelection();
        subject.Begin(issue, party);
        party.AddToCounts(troop, -4);
        selected.AddToCounts(troop, 4);

        subject.Restore(giver);
        subject.Restore(giver);
        Assert.Equal(10, party.GetTroopCount(troop));
        party.AddToCounts(troop, -4);
        Assert.Equal(6, party.GetTroopCount(troop));
    }

    [Fact]
    public void ForeignAcceptRestoresOnlyLocalSelectionBeforeReplacingIssueRoster()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var localTroop = new CharacterObject();
        var otherTroop = new CharacterObject();
        var party = TroopRoster.CreateDummyTroopRoster();
        var selected = TroopRoster.CreateDummyTroopRoster();
        var issue = CreateIssue(giver, selected);
        var subject = new ArtisanAlternativeSelection();
        subject.Begin(issue, party);
        selected.AddToCounts(localTroop, 3);

        subject.Restore(giver);
        selected.Clear();
        selected.AddToCounts(otherTroop, 7);
        subject.Restore(giver);

        Assert.Equal(3, party.GetTroopCount(localTroop));
        Assert.Equal(0, party.GetTroopCount(otherTroop));
    }

    [Fact]
    public void AnotherIssueCannotDrainThePendingSelection()
    {
        var giver = ObjectHelper.SkipConstructor<Hero>();
        var otherGiver = ObjectHelper.SkipConstructor<Hero>();
        var troop = new CharacterObject();
        var party = TroopRoster.CreateDummyTroopRoster();
        var selected = TroopRoster.CreateDummyTroopRoster();
        var subject = new ArtisanAlternativeSelection();
        subject.Begin(CreateIssue(giver, selected), party);
        selected.AddToCounts(troop, 2);

        subject.Restore(otherGiver);
        Assert.Equal(0, party.GetTroopCount(troop));
        subject.Restore(giver);
        Assert.Equal(2, party.GetTroopCount(troop));
    }
}
