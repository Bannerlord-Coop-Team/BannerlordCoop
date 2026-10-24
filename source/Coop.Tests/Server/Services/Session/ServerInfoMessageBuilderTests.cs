using Common.Logging;
using Coop.Core.Server.Services.Session;
using GameInterface.Configuration;
using GameInterface.Services.UI.ServerInfo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coop.Tests.Server.Services.Session;

/// <summary>
/// Serializes the tests that read the server info log lines, so a line from one class cannot land in another's capture.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ServerInfoLogCollection
{
    public const string Name = nameof(ServerInfoLogCollection);
}

/// <summary>Tests the caps and the link rules the server applies to the serverInfo block before anything is sent.</summary>
[Collection(ServerInfoLogCollection.Name)]
public sealed class ServerInfoMessageBuilderTests : IDisposable
{
    private readonly ConcurrentQueue<string> logs = new();
    private readonly Action<string> capture;
    private readonly ServerInfoMessageBuilder builder = new(new ServerInfoLinkRules());

    public ServerInfoMessageBuilderTests()
    {
        capture = logs.Enqueue;
        OutputSinkManager.AddLogCallback(capture);
    }

    [Fact]
    public void EveryKeyIsCarriedAsWritten()
    {
        var data = new ServerInfoConfigData
        {
            Motd = new[] { "Welcome to EU-1", "Restart 06:00 UTC" },
            Rules = new[] { "No griefing" },
            Links = new[] { new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" } },
            News = new[] { new ServerInfoNews { Date = "28 Sep 2026", Title = "Siege weekend", Text = "Double renown." } },
        };

        var info = builder.Build(data);

        Assert.Equal(data.Motd, info.Motd);
        Assert.Equal(data.Rules, info.Rules);
        Assert.Equal(("Discord", "https://discord.gg/example"), (info.Links[0].Label, info.Links[0].Url));
        Assert.Equal(("28 Sep 2026", "Siege weekend", "Double renown."), (info.News[0].Date, info.News[0].Title, info.News[0].Text));
        Assert.Contains(logs, log => log.Contains("Server info loaded from mod-config.json (motd 2 paragraph(s), rules 1, links 1, news 1)"));
    }

    // No block, an empty block or one with only blank or refused entries sends nothing.
    [Fact]
    public void NothingToShow_GivesNull()
    {
        Assert.Null(builder.Build(null));
        Assert.Null(builder.Build(new ServerInfoConfigData()));
        Assert.Null(builder.Build(new ServerInfoConfigData
        {
            Motd = new[] { "", "   ", null! },
            Rules = Array.Empty<string>(),
            Links = new[] { new ServerInfoLink { Label = "Script", Url = "javascript:void(0)" }, null },
            News = new[] { new ServerInfoNews { Date = "28 Sep 2026" }, null },
        }));
        Assert.DoesNotContain(logs, log => log.Contains("Server info loaded"));
    }

    [Theory]
    [InlineData("motd", ServerInfoLimits.MaxMotdParagraphs)]
    [InlineData("rules", ServerInfoLimits.MaxRules)]
    public void TextsOverTheCountCap_AreCutWithOneWarning(string key, int maxCount)
    {
        string[] texts = Enumerable.Range(1, maxCount + 5).Select(index => "Entry " + index).ToArray();
        var data = key == "motd" ? new ServerInfoConfigData { Motd = texts } : new ServerInfoConfigData { Rules = texts };

        var info = builder.Build(data);

        Assert.Equal(texts.Take(maxCount), key == "motd" ? info.Motd : info.Rules);
        Assert.Single(logs, log => log.Contains("serverInfo." + key + " was cut to " + maxCount + " entries"));
    }

    [Fact]
    public void LongTextsAreCutWithoutSplittingACharacter()
    {
        string longText = new string('a', ServerInfoLimits.MaxTextLength - 1) + "\U0001F600tail";
        string atCap = new string('b', ServerInfoLimits.MaxTextLength);

        var info = builder.Build(new ServerInfoConfigData { Motd = new[] { longText, atCap } });

        Assert.Equal(new string('a', ServerInfoLimits.MaxTextLength - 1), info.Motd[0]);
        Assert.Equal(atCap, info.Motd[1]);
        Assert.Single(logs, log => log.Contains("serverInfo.motd was cut"));
    }

    [Fact]
    public void ControlCharactersBecomeSpacesAndBlankEntriesAreDropped()
    {
        var info = builder.Build(new ServerInfoConfigData
        {
            Motd = new[] { " Welcome\tto\nEU-1 ", "", "   ", null, "\u0007" },
            Rules = new[] { "{PLAYER} <b>stays</b>" },
        });

        Assert.Equal(new[] { "Welcome to EU-1" }, info.Motd);
        Assert.Equal(new[] { "{PLAYER} <b>stays</b>" }, info.Rules);
        Assert.DoesNotContain(logs, log => log.Contains("was cut"));
    }

    // Each refused link is logged by its index, never by its address, and a good one after it is kept.
    [Fact]
    public void LinksAreNormalizedAndBadOnesAreSkippedByIndex()
    {
        string marker = Guid.NewGuid().ToString("N");
        var info = builder.Build(new ServerInfoConfigData
        {
            Links = new[]
            {
                new ServerInfoLink { Label = "Discord", Url = " https://Discord.gg/example " },
                new ServerInfoLink { Label = "Script", Url = "javascript:void('" + marker + "')" },
                null,
                new ServerInfoLink { Label = "Login", Url = "https://user:password@example.com/" + marker },
                new ServerInfoLink { Label = null, Url = "https://bücher.example/" },
                new ServerInfoLink { Label = new string('L', ServerInfoLimits.MaxLinkLabelLength + 10), Url = "https://example.com/rules" },
            },
        });

        Assert.Equal(new[]
            {
                ("Discord", "https://discord.gg/example"),
                ("", "https://xn--bcher-kva.example/"),
                (new string('L', ServerInfoLimits.MaxLinkLabelLength), "https://example.com/rules"),
            },
            info.Links.Select(link => (link.Label, link.Url)));
        Assert.Equal(new[] { 1, 2, 3 }, LoggedLinkIndexes());
        Assert.DoesNotContain(logs, log => log.Contains(marker));
        Assert.Single(logs, log => log.Contains("serverInfo.links was cut"));
    }

    [Fact]
    public void LinksOverTheCap_AreCut()
    {
        var links = Enumerable.Range(0, ServerInfoLimits.MaxLinks + 2)
            .Select(index => new ServerInfoLink { Label = "Link " + index, Url = "https://example.com/" + index })
            .ToArray();

        var info = builder.Build(new ServerInfoConfigData { Links = links });

        Assert.Equal(Enumerable.Range(0, ServerInfoLimits.MaxLinks).Select(index => "https://example.com/" + index), info.Links.Select(link => link.Url));
        Assert.Single(logs, log => log.Contains("serverInfo.links was cut to " + ServerInfoLimits.MaxLinks));
    }

    [Fact]
    public void NewsNeedsATitleOrATextAndIsCutToItsCaps()
    {
        var entries = new[]
        {
            new ServerInfoNews { Date = "28 Sep 2026", Title = "Siege weekend", Text = "Double renown." },
            new ServerInfoNews { Date = "27 Sep 2026" },
            null,
            new ServerInfoNews { Title = null, Text = "Text only" },
            new ServerInfoNews { Date = new string('d', 50), Title = new string('t', 100), Text = new string('x', ServerInfoLimits.MaxTextLength + 1) },
        }.Concat(Enumerable.Range(0, ServerInfoLimits.MaxNews).Select(index => new ServerInfoNews { Title = "More " + index })).ToArray();

        var info = builder.Build(new ServerInfoConfigData { News = entries });

        Assert.Equal(ServerInfoLimits.MaxNews, info.News.Length);
        Assert.Equal(("28 Sep 2026", "Siege weekend", "Double renown."), (info.News[0].Date, info.News[0].Title, info.News[0].Text));
        Assert.Equal(("", "", "Text only"), (info.News[1].Date, info.News[1].Title, info.News[1].Text));
        Assert.Equal(
            (ServerInfoLimits.MaxNewsDateLength, ServerInfoLimits.MaxNewsTitleLength, ServerInfoLimits.MaxTextLength),
            (info.News[2].Date.Length, info.News[2].Title.Length, info.News[2].Text.Length));
        Assert.Equal("More 0", info.News[3].Title);
        Assert.Single(logs, log => log.Contains("serverInfo.news was cut"));
    }

    public void Dispose()
    {
        OutputSinkManager.RemoveLogCallback(capture);
    }

    // The indexes this builder logged as refused, in order.
    private IEnumerable<int> LoggedLinkIndexes()
    {
        const string prefix = "mod-config.json: serverInfo.links[";
        return logs.Where(log => log.StartsWith(prefix, StringComparison.Ordinal))
            .Select(log => int.Parse(log.Substring(prefix.Length, log.IndexOf(']', prefix.Length) - prefix.Length)))
            .ToArray();
    }
}
