using GameInterface.Services.UI.BugReporting;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Tests the in-game bug-report form.</summary>
public class BugReportVMTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, false, false)]
    public void OverlayVisibility_OnlyRequiresUnblockedGameplay(
        bool isGameplayScreen,
        bool isLoading,
        bool isConversationActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            BugReportOverlay.ShouldShowPresentation(
                isGameplayScreen,
                isLoading,
                isConversationActive));
    }

    [Fact]
    public void Submit_WithMissingDescription_ShowsValidationAndDoesNotSend()
    {
        var submitted = false;
        var viewModel = new BugReportVM((_, _) => submitted = true)
        {
            Summary = "Party is stuck",
        };

        viewModel.ActionOpen();
        viewModel.ActionSubmit();

        Assert.False(submitted);
        Assert.True(viewModel.IsFormVisible);
        Assert.Contains("description", viewModel.ValidationMessage);
    }

    [Fact]
    public void Submit_WithSummaryAndDescription_SendsAndClosesForm()
    {
        string submittedSummary = null;
        string submittedDescription = null;
        var viewModel = new BugReportVM((summary, description) =>
        {
            submittedSummary = summary;
            submittedDescription = description;
            return true;
        })
        {
            Summary = "  Party is stuck  ",
            Description = "  Leaving Danustica keeps reopening the town menu.  ",
        };
        viewModel.ActionOpen();

        viewModel.ActionSubmit();

        Assert.Equal("Party is stuck", submittedSummary);
        Assert.Equal("Leaving Danustica keeps reopening the town menu.", submittedDescription);
        Assert.False(viewModel.IsFormVisible);
        Assert.Equal(string.Empty, viewModel.Summary);
        Assert.Equal(string.Empty, viewModel.Description);
    }

    [Fact]
    public void Submit_WhenConsentIsPending_KeepsFormUntilDeclined()
    {
        var submitted = false;
        var viewModel = new BugReportVM((_, _) => false)
        {
            Summary = "Party is stuck",
            Description = "Leaving Danustica keeps reopening the town menu.",
        };
        viewModel.Submitted += () => submitted = true;
        viewModel.ActionOpen();

        viewModel.ActionSubmit();

        Assert.True(viewModel.IsFormVisible);
        Assert.False(submitted);

        viewModel.DiscardSubmission();

        Assert.False(viewModel.IsFormVisible);
        Assert.False(submitted);
        Assert.Equal(string.Empty, viewModel.Summary);
        Assert.Equal(string.Empty, viewModel.Description);
    }
}
