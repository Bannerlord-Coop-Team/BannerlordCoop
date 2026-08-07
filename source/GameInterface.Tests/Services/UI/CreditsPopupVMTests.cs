using GameInterface.Services.UI.Credits;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class CreditsPopupVMTests
{
    [Fact]
    public void Constructor_RequiresCloseAction()
    {
        Assert.Throws<ArgumentNullException>(() => new CreditsPopupVM(null!));
    }

    [Fact]
    public void ActionClose_InvokesCloseOnce()
    {
        int closeCount = 0;
        var viewModel = new CreditsPopupVM(() => closeCount++);

        viewModel.ActionClose();

        Assert.Equal(1, closeCount);
    }

    [Fact]
    public void SectionTexts_ListRosterNamesOrFallBackWhenEmpty()
    {
        var viewModel = new CreditsPopupVM(() => { });

        Assert.Equal(ExpectedSectionText(CreditsRoster.Sponsors), viewModel.SponsorsText);
        Assert.Equal(ExpectedSectionText(CreditsRoster.Supporters), viewModel.SupportersText);
        Assert.Equal(ExpectedSectionText(CreditsRoster.Contributors), viewModel.ContributorsText);
        Assert.Equal(ExpectedSectionText(CreditsRoster.Community), viewModel.CommunityText);
    }

    [Fact]
    public void ContributorsRoster_HasCleanDistinctNames()
    {
        Assert.NotEmpty(CreditsRoster.Contributors);
        Assert.All(CreditsRoster.Contributors, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(CreditsRoster.Contributors.Count, CreditsRoster.Contributors
            .Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static string ExpectedSectionText(IReadOnlyList<string> names)
    {
        return names.Count == 0 ? CreditsPopupVM.EmptySectionText : string.Join("\n", names);
    }
}
