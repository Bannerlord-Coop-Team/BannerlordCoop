using GameInterface.Services.Kingdoms;
using GameInterface.Services.Kingdoms.Data;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Kingdoms;

public class KingdomDecisionRoundPresentationTests
{
    private readonly KingdomDecisionRoundPresentation presentation = new();

    [Fact]
    public void FormatTitle_AddsCountdownOnceAndRestoresBaseTitle()
    {
        const string baseTitle = "Vote for the new owner of Danustica";

        string titled = presentation.FormatTitle(baseTitle, 54);
        string titledAgain = presentation.FormatTitle(titled, 54);

        Assert.Equal("Vote for the new owner of Danustica. (Voting ends in 54s)", titled);
        Assert.Equal(titled, titledAgain);
        Assert.Equal(1, CountOccurrences(titledAgain, "Voting ends in"));
        Assert.Equal(baseTitle + ".", presentation.GetBaseTitle(titledAgain));
        Assert.Equal(baseTitle, presentation.FormatTitle(baseTitle, null));
    }

    [Fact]
    public void FormatWaitingFeedback_ExcludesCountdownAndSplitsNamesIntoFourColumns()
    {
        var waitingClans = new[]
        {
            new KingdomDecisionRoundClanStatusData("clan_a", "Clan A", "Alice", false, true),
            new KingdomDecisionRoundClanStatusData("clan_b", "Clan B", "Bob", false, false),
            new KingdomDecisionRoundClanStatusData("clan_c", "Clan C", "Cara", false, true),
            new KingdomDecisionRoundClanStatusData("clan_d", "Clan D", "Cara", false, true),
            new KingdomDecisionRoundClanStatusData("clan_e", "Clan E", "Eve", false, true),
        };

        KingdomDecisionWaitingFeedback feedback = presentation.FormatWaitingFeedback(true, waitingClans);

        Assert.Contains("Vote submitted", feedback.Header);
        Assert.Contains("Waiting for", feedback.Header);
        Assert.DoesNotContain("Voting ends in", feedback.Header);
        Assert.DoesNotContain("54s", feedback.Header);
        Assert.Equal(4, feedback.Columns.Count);
        Assert.Equal("Alice (Clan A)\nBob (Clan B, disconnected)", feedback.Columns[0]);
        Assert.Equal("Cara (Clan C)\nCara (Clan D)", feedback.Columns[1]);
        Assert.Equal("Eve (Clan E)", feedback.Columns[2]);
        Assert.Equal(string.Empty, feedback.Columns[3]);
        Assert.Equal(5, feedback.Columns.Sum(column => string.IsNullOrEmpty(column) ? 0 : column.Split('\n').Length));
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
