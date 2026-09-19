using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatUIMovieTests
{
    [Fact]
    public void Movie_BindsBottomLeftFeedAndChannelChrome()
    {
        var document = XDocument.Load(FindMoviePath());

        var input = FindById(document, "CoopChatMessageInput");
        Assert.Equal("@WrittenText", input.Attribute("Text")?.Value);
        Assert.Equal("@MaxMessageLength", input.Attribute("MaxLength")?.Value);

        var channelList = FindById(document, "ChatChannelList");
        Assert.Equal("{Channels}", channelList.Attribute("DataSource")?.Value);
        var channelButton = Assert.Single(channelList.Descendants("ButtonWidget"));
        Assert.Equal("ExecuteSelection", channelButton.Attribute("Command.Click")?.Value);
        Assert.Equal("@IsSelected", channelButton.Attribute("IsSelected")?.Value);
        Assert.Contains(channelButton.Descendants("TextWidget"),
            element => element.Attribute("Text")?.Value == "@Name");

        Assert.Contains(document.Descendants(),
            element => element.Attribute("Command.Click")?.Value == "ActionSend");
        var closeButton = FindById(document, "CoopChatCloseButton");
        Assert.Equal("ActionClose", closeButton.Attribute("Command.Click")?.Value);
        Assert.Null(closeButton.Attribute("Parameter.Text"));

        var muteButton = FindById(document, "CoopChatMuteButton");
        Assert.Equal("ActionToggleMute", muteButton.Attribute("Command.Click")?.Value);
        Assert.Equal("@MuteButtonText", muteButton.Attribute("Parameter.Text")?.Value);
        Assert.Equal("@IsMuteButtonVisible", muteButton.Attribute("IsVisible")?.Value);

        var unreadBadge = FindById(document, "CoopChatUnreadBadge");
        Assert.Equal("@HasUnreadNotification", unreadBadge.Attribute("IsVisible")?.Value);
        Assert.Contains(unreadBadge.Descendants("TextWidget"),
            element => element.Attribute("Text")?.Value == "@UnreadNotificationText");

        var chatPanel = FindById(document, "CoopChatRoot");
        Assert.Equal("Left", chatPanel.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Bottom", chatPanel.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("27", chatPanel.Attribute("MarginLeft")?.Value);
        Assert.Equal("100", chatPanel.Attribute("MarginBottom")?.Value);

        var feedList = FindById(document, "ChatFeedList");
        Assert.Equal("{VisibleLines}", feedList.Attribute("DataSource")?.Value);
        Assert.Contains(feedList.Descendants("RichTextWidget"),
            element => element.Attribute("Text")?.Value == "@Text" &&
                       element.Attribute("Brush.FontColor")?.Value == "@Color" &&
                       element.Attribute("Brush.GlobalAlphaFactor")?.Value == "@Alpha");

        var feedScroll = FindById(document, "ChatFeedScrollablePanel");
        Assert.Equal(
            @"..\ChatFeedScrollbarHolder\ChatFeedScrollbar",
            feedScroll.Attribute("VerticalScrollbar")?.Value);

        Assert.DoesNotContain(document.Descendants(),
            element => element.Attribute("Id")?.Value == "CoopChatRibbon");
    }

    private static XElement FindById(XDocument document, string id)
    {
        return Assert.Single(document.Descendants(),
            element => element.Attribute("Id")?.Value == id);
    }

    private static string FindMoviePath([CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        return Path.GetFullPath(Path.Combine(sourceDirectory!,
            "..", "..", "..", "..", "UIMovies", "CoopChatUIMovie.xml"));
    }
}