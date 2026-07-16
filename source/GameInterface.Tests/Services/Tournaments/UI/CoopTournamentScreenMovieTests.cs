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

    [Fact]
    public void Movie_UsesNativeWagerSliderLayoutAndHitTesting()
    {
        var movie = File.ReadAllText(FindRepositoryFile("UIMovies", "CoopTournamentScreen.xml"));
        var document = XDocument.Parse(movie);

        var slider = Assert.Single(document.Descendants("SliderWidget"));
        Assert.Equal("SliderWidget", slider.Attribute("Id")?.Value);
        Assert.Equal("Bottom", slider.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("Filler", slider.Attribute("Filler")?.Value);
        Assert.Equal("SliderHandle", slider.Attribute("Handle")?.Value);

        var canvas = Assert.Single(slider.Descendants("Widget"), element =>
            element.Attribute("Sprite")?.Value == @"SPGeneral\SPOptions\standart_slider_canvas");
        Assert.Equal("Center", canvas.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("false", canvas.Attribute("IsEnabled")?.Value);

        var frame = Assert.Single(slider.Descendants("Widget"), element =>
            element.Attribute("Sprite")?.Value == @"SPGeneral\SPOptions\standart_slider_frame");
        Assert.Equal("Center", frame.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("false", frame.Attribute("IsEnabled")?.Value);

        var filler = Assert.Single(slider.Descendants("Widget"), element =>
            element.Attribute("Id")?.Value == "Filler");
        Assert.Single(filler.Descendants("Widget"), element =>
            element.Attribute("Sprite")?.Value == @"SPGeneral\SPOptions\standart_slider_fill");

        var handle = Assert.Single(slider.Descendants("ImageWidget"), element =>
            element.Attribute("Id")?.Value == "SliderHandle");
        Assert.Equal("Center", handle.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("true", handle.Attribute("DoNotAcceptEvents")?.Value);
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
