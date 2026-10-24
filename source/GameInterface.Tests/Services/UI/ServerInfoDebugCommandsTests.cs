#if DEBUG
using Common.Commands;
using GameInterface.Services.UI.ServerInfo;
using System;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects the DEBUG samples a live run uses to show every tab and the link dialog.</summary>
[Collection(ViewModelCollection.Name)]
public class ServerInfoDebugCommandsTests
{
    private readonly ServerInfoLinkRules rules = new();

    // A preview after the join's own open behaves like info from the server: the closed panel opens once the map is free.
    [Fact]
    public void Preview_OpensTheSampleLikeInfoFromTheServer()
    {
        var popup = new ServerInfoServiceTests.FakePopup();
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(ServerInfoDebugCommands.Samples["motd-only"]);
        service.Update();
        popup.ViewModel!.ExecuteClose();

        var result = ServerInfoDebugCommands.ServerInfoPreviewCoopCommand.Preview(service, ServerInfoDebugCommands.Samples["full"]);
        service.Update();

        Assert.True(result.Succeeded);
        Assert.StartsWith("Open: False\nPending: True\nTab: Motd\nTabs: Motd, Rules, Links, News", result.Output);
        Assert.True(popup.ViewModel.IsOpen);
        Assert.Equal(2, popup.Opened);
    }

    // The toggle command is F3 for a live run: it opens the last info and closes the open panel through the same guards.
    [Fact]
    public void Toggle_OpensAndClosesLikeF3()
    {
        var popup = new ServerInfoServiceTests.FakePopup();
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(ServerInfoDebugCommands.Samples["full"]);
        service.Update();
        popup.ViewModel!.ExecuteSelectTab((int)ServerInfoTab.News);
        popup.ViewModel.ExecuteClose();

        Assert.True(service.Toggle());
        Assert.True(popup.ViewModel.IsOpen);
        Assert.Equal(ServerInfoTab.Motd, popup.ViewModel.SelectedTab);

        Assert.True(service.Toggle());
        Assert.False(popup.ViewModel.IsOpen);
        Assert.Equal(2, popup.Opened);
    }

    [Fact]
    public void CommandsAreClientSideUnderTheUiPrefix()
    {
        ICoopCommand[] commands =
        {
            new ServerInfoDebugCommands.ServerInfoPreviewCoopCommand(),
            new ServerInfoDebugCommands.ServerInfoStateCoopCommand(),
            new ServerInfoDebugCommands.ServerInfoToggleCoopCommand(),
        };

        Assert.Equal(
            new[] { "coop.debug.ui.server_info_preview", "coop.debug.ui.server_info_state", "coop.debug.ui.server_info_toggle" },
            commands.Select(command => command.Prefix + "." + command.Name));
        Assert.All(commands, command => Assert.Equal(CoopCommandSide.Client, command.Side));
    }

    [Fact]
    public void OffersTheFourSamples()
    {
        Assert.Equal(new[] { "full", "long", "motd-only", "bad-links" }, ServerInfoDebugCommands.Samples.Keys);
    }

    // The samples stay inside what a server may send, so the preview looks like a real join.
    [Theory]
    [InlineData("full")]
    [InlineData("long")]
    [InlineData("motd-only")]
    [InlineData("bad-links")]
    public void SamplesFitTheServerCaps(string sample)
    {
        var info = ServerInfoDebugCommands.Samples[sample];
        var motd = info.Motd ?? Array.Empty<string>();
        var ruleTexts = info.Rules ?? Array.Empty<string>();
        var links = info.Links ?? Array.Empty<ServerInfoLink>();
        var news = info.News ?? Array.Empty<ServerInfoNews>();

        Assert.InRange(motd.Length, 1, ServerInfoLimits.MaxMotdParagraphs);
        Assert.All(motd, text => Assert.InRange(text.Length, 1, ServerInfoLimits.MaxTextLength));
        Assert.InRange(ruleTexts.Length, 0, ServerInfoLimits.MaxRules);
        Assert.All(ruleTexts, text => Assert.InRange(text.Length, 1, ServerInfoLimits.MaxTextLength));
        Assert.InRange(links.Length, 0, ServerInfoLimits.MaxLinks);
        Assert.All(links, link => Assert.InRange(link.Label.Length, 0, ServerInfoLimits.MaxLinkLabelLength));
        Assert.All(links, link => Assert.InRange(link.Url.Length, 1, ServerInfoLimits.MaxLinkUrlLength));
        Assert.InRange(news.Length, 0, ServerInfoLimits.MaxNews);
        Assert.All(news, item => Assert.True(
            item.Date.Length <= ServerInfoLimits.MaxNewsDateLength &&
            item.Title.Length <= ServerInfoLimits.MaxNewsTitleLength &&
            item.Text.Length <= ServerInfoLimits.MaxTextLength));
    }

    [Fact]
    public void LongSample_FillsEveryCountCapSoEveryTabScrolls()
    {
        var info = ServerInfoDebugCommands.Samples["long"];

        Assert.Equal(
            (ServerInfoLimits.MaxMotdParagraphs, ServerInfoLimits.MaxRules, ServerInfoLimits.MaxLinks, ServerInfoLimits.MaxNews),
            (info.Motd.Length, info.Rules.Length, info.Links.Length, info.News.Length));
    }

    [Theory]
    [InlineData("full")]
    [InlineData("long")]
    public void RegularSamples_HaveOnlyLinksTheRulesAccept(string sample)
    {
        Assert.All(ServerInfoDebugCommands.Samples[sample].Links, link => Assert.True(rules.TryNormalize(link.Url, out _), link.Url));
    }

    // The bad-links preview proves the client check: only the Discord link and the rules address survive.
    [Fact]
    public void BadLinksSample_KeepsOnlyTwoLinks()
    {
        var accepted = ServerInfoDebugCommands.BadSampleLinks.Where(link => rules.TryNormalize(link.Url, out _)).Select(link => link.Url);

        Assert.Equal(new[] { "https://discord.gg/example", "https://example.com/rules" }, accepted);
        Assert.Equal(8, ServerInfoDebugCommands.BadSampleLinks.Length);
    }

    [Fact]
    public void UnknownSample_IsRefusedBeforeAnythingOpens()
    {
        var command = new ServerInfoDebugCommands.ServerInfoPreviewCoopCommand();

        var result = command.ProcessCommand(new CoopCommandArgsFactory().FromValues(new[] { "huge" }));

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_sample", result.ErrorCode);
        Assert.Contains("full, long, motd-only, bad-links", result.Output);
    }
}
#endif
