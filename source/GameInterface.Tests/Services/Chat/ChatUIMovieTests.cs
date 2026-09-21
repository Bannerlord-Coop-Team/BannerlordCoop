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
        Assert.Equal("CoverChildren", channelList.Attribute("WidthSizePolicy")?.Value);
        var channelButton = Assert.Single(channelList.Descendants("ButtonWidget"));
        Assert.Equal("ExecuteSelection", channelButton.Attribute("Command.Click")?.Value);
        Assert.Equal("@IsSelected", channelButton.Attribute("IsSelected")?.Value);
        Assert.Contains(channelButton.Descendants("TextWidget"),
            element => element.Attribute("Text")?.Value == "@Name");

        var channelScroll = FindById(document, "ChatChannelScrollablePanel");
        Assert.Equal(@"..\ChatChannelScrollbar", channelScroll.Attribute("HorizontalScrollbar")?.Value);
        Assert.Equal("Horizontal", channelScroll.Attribute("MouseScrollAxis")?.Value);
        Assert.Null(channelScroll.Attribute("VerticalScrollbar"));
        Assert.Equal("true", channelScroll.Attribute("AutoHideScrollBars")?.Value);

        Assert.DoesNotContain(document.Descendants(),
            element => element.Attribute("Command.Click")?.Value == "ActionSend");
        Assert.DoesNotContain(document.Descendants(),
            element => element.Attribute("Command.Click")?.Value == "ActionClose");
        Assert.DoesNotContain(document.Descendants(),
            element => element.Attribute("Id")?.Value == "CoopChatCloseButton");
        Assert.DoesNotContain(document.Descendants("TextWidget"),
            element => element.Attribute("Text")?.Value == "@InputHintText");

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
        Assert.Equal("75", chatPanel.Attribute("MarginBottom")?.Value);
        Assert.Equal("@ChatBoxSizeX", chatPanel.Attribute("SuggestedWidth")?.Value);
        Assert.Equal("@ChatBoxSizeY", chatPanel.Attribute("SuggestedHeight")?.Value);
        Assert.Equal("425", chatPanel.Attribute("MinWidth")?.Value);
        Assert.Equal("650", chatPanel.Attribute("MaxWidth")?.Value);
        Assert.Equal("170", chatPanel.Attribute("MinHeight")?.Value);
        Assert.Equal("470", chatPanel.Attribute("MaxHeight")?.Value);

        var resizer = FindById(document, "CoopChatResizer");
        Assert.Equal("SPChatlog.Resizer", resizer.Attribute("Brush")?.Value);
        Assert.Equal("@IsOpen", resizer.Attribute("IsVisible")?.Value);
        Assert.Equal("DiagonalRightResize", resizer.Attribute("HoveredCursorState")?.Value);
        Assert.NotNull(FindById(document, "CoopChatResizeFrame"));

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
        Assert.Equal("true", feedScroll.Attribute("ReverseInitialScrollBarAlignment")?.Value);

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