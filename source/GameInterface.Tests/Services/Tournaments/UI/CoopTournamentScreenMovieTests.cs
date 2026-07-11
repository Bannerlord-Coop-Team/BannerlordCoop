using System;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Tournaments.UI;

public class CoopTournamentScreenMovieTests
{
    [Fact]
    public void Movie_PreservesNativePresentationAndUsesOnlyCoopControls()
    {
        var movie = File.ReadAllText(FindRepositoryFile("UIMovies", "CoopTournamentScreen.xml"));
        _ = XDocument.Parse(movie);

        Assert.Contains("Tournament.Round", movie);
        Assert.Contains("TournamentMatchWidget", movie);
        Assert.Contains("State=\"@State\"", movie);
        Assert.DoesNotContain("TournamentMatchWidget DataSource=\"{CurrentMatch}\" Brush=", movie);
        Assert.Contains("DataSource=\"{CurrentMatch}\"", movie);
        Assert.Contains("IsVisible=\"@IsCurrentMatchActive\"", movie);
        Assert.Contains("IsHidden=\"@IsCurrentMatchActive\"", movie);
        Assert.Contains("{PrizeVisual}", movie);
        Assert.Contains("@IsOver", movie);
        Assert.Contains("@WinnerIntro", movie);
        Assert.Contains("DataSource=\"{TournamentWinner\\Character}\"", movie);
        Assert.Contains("DataSource=\"{WinnerBanner}\"", movie);
        Assert.Contains("DataSource=\"{BattleRewards}\"", movie);
        Assert.Contains("CharacterContainer=\"MainContents\\ScrollablePanel", movie);
        Assert.Contains("ScoreboardBattleRewardsWidget=\"MainContents\\ScrollablePanel", movie);
        Assert.Contains("@WageredDenars", movie);
        Assert.Contains("@ExpectedBetDenars", movie);
        Assert.Contains("@CanJoin", movie);
        Assert.Contains("@CanWatch", movie);
        Assert.Contains("@CanSkip", movie);
        Assert.Contains("@CanLeave", movie);
        Assert.Contains("@ReadyCountText", movie);
        Assert.Contains("@SkipCountText", movie);
        Assert.Contains("@SelectedChoiceText", movie);
        Assert.Contains("DataSource=\"{PrizeVisual}\" WidthSizePolicy=\"StretchToParent\"", movie);
        Assert.DoesNotContain("VerticalAlignment=\"Bottom\" Sprite=\"BlankWhiteSquare_9\" Color=\"#000000FF\" AlphaFactor=\"0.88\"", movie);
        Assert.DoesNotContain("SkipAll", movie);
        Assert.DoesNotContain("ExecuteSkipAllRounds", movie);
        Assert.DoesNotContain("ExecuteJoinTournament", movie);
        Assert.DoesNotContain("ExecuteWatchRound", movie);
        Assert.DoesNotContain("ExecuteSkipRound", movie);
    }

    private static string FindRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, Path.Combine(pathParts));
            if (File.Exists(path)) return path;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Unable to find {Path.Combine(pathParts)} from {AppContext.BaseDirectory}");
    }
}
