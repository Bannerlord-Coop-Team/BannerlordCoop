using GameInterface.Services.Tournaments.UI;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class TournamentCompletionPresentationTests
{
    [Fact]
    public void CompletedSnapshot_PresentsCanonicalWinnerAndIsOverExactlyOnce()
    {
        var presentation = new TournamentCompletionPresentation();
        int calls = 0;
        int notifications = 0;
        string winnerSlotId = null;
        bool isOver = false;
        presentation.Observe(true);

        bool first = presentation.TryPresent(() =>
        {
            calls++;
            notifications++;
            winnerSlotId = "winner-slot";
            isOver = true;
        });
        presentation.Observe(true);
        bool duplicate = presentation.TryPresent(() => calls++);

        Assert.True(first);
        Assert.False(duplicate);
        Assert.True(presentation.IsPresented);
        Assert.True(isOver);
        Assert.Equal("winner-slot", winnerSlotId);
        Assert.Equal(1, calls);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void IncompleteSnapshot_DoesNotPresentCompletion()
    {
        var presentation = new TournamentCompletionPresentation();
        int calls = 0;
        presentation.Observe(false);

        bool presented = presentation.TryPresent(() => calls++);

        Assert.False(presented);
        Assert.False(presentation.IsPresented);
        Assert.Equal(0, calls);
    }
}
