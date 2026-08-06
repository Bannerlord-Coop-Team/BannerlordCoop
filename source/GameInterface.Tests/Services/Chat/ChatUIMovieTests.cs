using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Chat;

public class ChatUIMovieTests
{
    [Fact]
    public void Movie_BindsInputAndChannelSelectionToChatViewModels()
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
        Assert.Contains(closeButton.Ancestors(),
            element => element.Attribute("Id")?.Value == "CoopChatHeader");
        Assert.Equal("Right", closeButton.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Top", closeButton.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("CloseButton.Flat", closeButton.Attribute("Brush")?.Value);
        Assert.Equal("ActionClose", closeButton.Attribute("Command.Click")?.Value);
        Assert.Null(closeButton.Attribute("Parameter.Text"));

        var muteButton = FindById(document, "CoopChatMuteButton");
        Assert.Contains(muteButton.Ancestors(),
            element => element.Attribute("Id")?.Value == "CoopChatHeader");
        Assert.Equal("ActionToggleMute", muteButton.Attribute("Command.Click")?.Value);
        Assert.Equal("@MuteButtonText", muteButton.Attribute("Parameter.Text")?.Value);
        Assert.Equal("@IsMuteButtonVisible", muteButton.Attribute("IsVisible")?.Value);

        var ribbon = FindById(document, "CoopChatRibbon");
        Assert.Equal("Right", ribbon.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Bottom", ribbon.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("101", ribbon.Attribute("MarginBottom")?.Value);
        Assert.Null(ribbon.Attribute("PositionYOffset"));
        Assert.Equal("@IsRibbonVisible", ribbon.Attribute("IsVisible")?.Value);

        var ribbonButton = FindById(document, "CoopChatRibbonButton");
        Assert.Equal("ActionOpen", ribbonButton.Attribute("Command.Click")?.Value);

        var unreadBadge = FindById(document, "CoopChatUnreadBadge");
        Assert.Equal("@HasUnreadNotification", unreadBadge.Attribute("IsVisible")?.Value);
        Assert.Contains(unreadBadge.Descendants("TextWidget"),
            element => element.Attribute("Text")?.Value == "@UnreadNotificationText");

        var chatPanel = FindById(document, "CoopChatRoot");
        Assert.Equal("Right", chatPanel.Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("@IsOpen", chatPanel.Attribute("IsVisible")?.Value);
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
