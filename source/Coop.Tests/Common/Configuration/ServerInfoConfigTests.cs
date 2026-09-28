using Common.Logging;
using Coop.Core.Common.Configuration;
using GameInterface.Services.UI.ServerInfo;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Coop.Tests.Configuration;

/// <summary>Tests the operator's server-info.json: its four keys, their caps and the link rules.</summary>
public sealed class ServerInfoConfigTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "BannerlordCoop-ServerInfoConfigTests-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> logs = new();

    public ServerInfoConfigTests()
    {
        OutputSinkManager.AddLogCallback(CaptureLog);
    }

    private string InfoFilePath => Path.Combine(directory, "server-info.json");

    [Fact]
    public void MissingFile_GivesNothingAndNoLog()
    {
        Directory.CreateDirectory(directory);

        var config = Create();

        AssertEmpty(config);
        Assert.Empty(LogsForThisTest());
    }

    [Fact]
    public void MissingFolder_GivesNothingAndNoLog()
    {
        var config = Create();

        AssertEmpty(config);
        Assert.Empty(LogsForThisTest());
    }

    [Fact]
    public void AsksTheDataPathForTheInfoFile()
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\"]}");
        var dataPath = new Mock<IServerDataPath>();
        dataPath.Setup(path => path.Resolve("COOP_SERVER_INFO_FILE", "server-info.json")).Returns(InfoFilePath);

        var config = new ServerInfoConfig(dataPath.Object, new ServerInfoLinkRules());

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
    }

    [Fact]
    public void DataPathThatThrows_LogsAnErrorAndIsNotFatal()
    {
        var dataPath = new Mock<IServerDataPath>();
        dataPath.Setup(path => path.Resolve(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new ArgumentException("Illegal characters in path " + directory));

        var config = new ServerInfoConfig(dataPath.Object, new ServerInfoLinkRules());

        AssertEmpty(config);
        // The log carries the exception, which names this test's folder.
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    // Files written for the first MOTD build keep working unchanged.
    [Fact]
    public void MotdOnlyFile_ReadsParagraphsInOrderAndLogsOnlyTheCounts()
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\",\"Restart 06:00 UTC\"]}");

        var config = Create();

        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, config.Motd);
        Assert.Empty(config.Rules);
        Assert.Empty(config.Links);
        Assert.Empty(config.News);
        string loaded = Assert.Single(LogsForThisTest());
        Assert.Contains("motd 2 paragraph(s), rules 0, links 0, news 0", loaded);
        Assert.DoesNotContain("Welcome", loaded);
    }

    [Fact]
    public void AllKeys_AreReadInTheOperatorsOrder()
    {
        WriteInfoFile(Json(new
        {
            motd = new[] { "Welcome to EU-1", "Restart 06:00 UTC" },
            rules = new[] { "Be kind in chat", "No griefing" },
            links = new[]
            {
                new { label = "Discord", url = "https://discord.gg/example" },
                new { label = "Website", url = "https://example.com/" },
            },
            news = new[]
            {
                new { date = "28 Sep 2026", title = "Siege weekend", text = "Castle sieges give double renown." },
                new { date = "25 Sep 2026", title = "Server updated", text = "Update your mod to join." },
            },
        }));

        var config = Create();

        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, config.Motd);
        Assert.Equal(new[] { "Be kind in chat", "No griefing" }, config.Rules);
        Assert.Equal(new[] { ("Discord", "https://discord.gg/example"), ("Website", "https://example.com/") },
            config.Links.Select(link => (link.Label, link.Url)));
        Assert.Equal(new[] { ("28 Sep 2026", "Siege weekend", "Castle sieges give double renown."), ("25 Sep 2026", "Server updated", "Update your mod to join.") },
            config.News.Select(item => (item.Date, item.Title, item.Text)));
        string loaded = Assert.Single(LogsForThisTest());
        Assert.Contains("motd 2 paragraph(s), rules 2, links 2, news 2", loaded);
        Assert.DoesNotContain("discord", loaded);
        Assert.DoesNotContain("Siege", loaded);
    }

    [Theory]
    [InlineData("{\"rules\":[\"No griefing\"]}", 0, 1, 0, 0)]
    [InlineData("{\"links\":[{\"label\":\"Discord\",\"url\":\"https://discord.gg/example\"}]}", 0, 0, 1, 0)]
    [InlineData("{\"news\":[{\"title\":\"Siege weekend\"}]}", 0, 0, 0, 1)]
    public void EachKeyAlone_IsEnough(string json, int motd, int rules, int links, int news)
    {
        WriteInfoFile(json);

        var config = Create();

        Assert.Equal((motd, rules, links, news), (config.Motd.Count, config.Rules.Count, config.Links.Count, config.News.Count));
        Assert.Contains("Server info loaded", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void UnknownKeysAreIgnored()
    {
        WriteInfoFile("{\"serverName\":\"EU-1 Test\",\"region\":\"EU\",\"motd\":[\"Welcome to EU-1\"],\"extra\":{\"a\":1}," +
            "\"links\":[{\"label\":\"Discord\",\"url\":\"https://discord.gg/example\",\"icon\":\"chat\"}]," +
            "\"news\":[{\"title\":\"Siege weekend\",\"author\":\"admin\"}]}");

        var config = Create();

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
        Assert.Equal("https://discord.gg/example", Assert.Single(config.Links).Url);
        Assert.Equal("Siege weekend", Assert.Single(config.News).Title);
    }

    [Fact]
    public void FileWithNoKnownKey_GivesNothing()
    {
        WriteInfoFile("{\"serverName\":\"EU-1 Test\"}");

        var config = Create();

        AssertEmpty(config);
        Assert.Contains("motd 0 paragraph(s), rules 0, links 0, news 0", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void KeepsTheFirstTenNonEmptyParagraphs()
    {
        string[] entries = { "", "p1", "   ", "p2", "p3", "\\n", "p4", "p5", "p6", "p7", "p8", "p9", "p10", "p11", "p12" };
        WriteInfoFile("{\"motd\":[" + string.Join(",", entries.Select(entry => "\"" + entry + "\"")) + "]}");

        var config = Create();

        Assert.Equal(ServerInfoLimits.MaxMotdParagraphs, config.Motd.Count);
        Assert.Equal(Enumerable.Range(1, 10).Select(index => "p" + index), config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("\"motd\" in") && log.Contains("was cut to 10 entry(s) and 2000 characters"));
    }

    [Fact]
    public void ExactlyTheCaps_AreNotReportedAsCut()
    {
        string[] entries = Enumerable.Range(0, ServerInfoLimits.MaxMotdParagraphs)
            .Select(index => new string((char)('a' + index), ServerInfoLimits.MaxMotdLength / ServerInfoLimits.MaxMotdParagraphs)).ToArray();
        WriteInfoFile("{\"motd\":[" + string.Join(",", entries.Select(entry => "\"" + entry + "\"")) + "]}");

        var config = Create();

        Assert.Equal(entries, config.Motd);
        Assert.Equal(ServerInfoLimits.MaxMotdLength, config.Motd.Sum(paragraph => paragraph.Length));
        Assert.DoesNotContain(LogsForThisTest(), log => log.Contains("was cut"));
    }

    [Fact]
    public void TotalLengthIsCappedAndLaterParagraphsAreDropped()
    {
        string first = new string('a', 1500);
        string second = new string('b', 800);
        WriteInfoFile("{\"motd\":[\"" + first + "\",\"" + second + "\",\"tail\"]}");

        var config = Create();

        Assert.Equal(new[] { first, new string('b', 500) }, config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("was cut to"));
    }

    [Fact]
    public void NoLaterParagraphFillsTheRoomLeftByACut()
    {
        // The cut paragraph ends in a space that is trimmed, which leaves one character of room.
        string first = new string('a', 1500);
        string second = new string('b', 499) + " cccc";
        WriteInfoFile("{\"motd\":[\"" + first + "\",\"" + second + "\",\"x\"]}");

        var config = Create();

        Assert.Equal(new[] { first, new string('b', 499) }, config.Motd);
    }

    [Fact]
    public void ParagraphAfterAFullMotd_IsDroppedAndReported()
    {
        string full = new string('a', ServerInfoLimits.MaxMotdLength);
        WriteInfoFile("{\"motd\":[\"" + full + "\",\"x\"]}");

        var config = Create();

        Assert.Equal(new[] { full }, config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("was cut to"));
    }

    [Fact]
    public void CutNeverSplitsASurrogatePair()
    {
        // The pair starts at the last allowed index, so a plain cut would keep only its high half.
        string line = new string('a', ServerInfoLimits.MaxMotdLength - 1) + "\U0001F600" + "tail";
        WriteInfoFile("{\"motd\":[\"" + line + "\"]}");

        var config = Create();

        string cut = Assert.Single(config.Motd);
        Assert.Equal(new string('a', ServerInfoLimits.MaxMotdLength - 1), cut);
        Assert.False(char.IsHighSurrogate(cut[cut.Length - 1]));
    }

    [Fact]
    public void ControlCharactersBecomeSpacesAndTextIsTrimmed()
    {
        WriteInfoFile("{\"motd\":[\"  Rules:\\tbe nice\\nno griefing\\u0007  \"],\"rules\":[\"\\tNo\\rgriefing \"]," +
            "\"links\":[{\"label\":\" Disc\\nord \",\"url\":\"https://discord.gg/example\"}]," +
            "\"news\":[{\"date\":\"\\t28 Sep\",\"title\":\"Siege\\u0000weekend\",\"text\":\"Double\\u0085renown\"}]}");

        var config = Create();

        Assert.Equal("Rules: be nice no griefing", Assert.Single(config.Motd));
        Assert.Equal("No griefing", Assert.Single(config.Rules));
        Assert.Equal("Disc ord", Assert.Single(config.Links).Label);
        var news = Assert.Single(config.News);
        Assert.Equal(("28 Sep", "Siege weekend", "Double renown"), (news.Date, news.Title, news.Text));
    }

    // Operator text is shown as written, so braces and tags are not placeholders or markup anywhere.
    [Fact]
    public void BracesAndTagsAreKeptAsWritten()
    {
        const string literal = "{PLAYER} {=coop_server_info_title}x <b>bold</b>";
        WriteInfoFile(Json(new
        {
            motd = new[] { literal },
            rules = new[] { literal },
            links = new[] { new { label = "{LINK} <a href=\"event:1\">", url = "https://example.com/" } },
            news = new[] { new { date = "{DATE}", title = literal, text = literal } },
        }));

        var config = Create();

        Assert.Equal(literal, Assert.Single(config.Motd));
        Assert.Equal(literal, Assert.Single(config.Rules));
        Assert.Equal("{LINK} <a href=\"event:1\">", Assert.Single(config.Links).Label);
        var news = Assert.Single(config.News);
        Assert.Equal(("{DATE}", literal, literal), (news.Date, news.Title, news.Text));
    }

    [Fact]
    public void NonAsciiTextAndEscapedPairsAreKept()
    {
        WriteInfoFile("{\"motd\":[\"Willkommen überall, 欢迎\",\"smile \\ud83d\\ude00\"],\"news\":[{\"title\":\"Fête \\ud83d\\ude00\"}]}");

        var config = Create();

        Assert.Equal(new[] { "Willkommen überall, 欢迎", "smile \U0001F600" }, config.Motd);
        Assert.Equal("Fête \U0001F600", Assert.Single(config.News).Title);
    }

    [Fact]
    public void FileSavedWithAByteOrderMarkIsRead()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(InfoFilePath, "{\"motd\":[\"Welcome to EU-1\"]}", new UTF8Encoding(true));

        var config = Create();

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("{\"motd\":[\"a\",]}")]
    [InlineData("{\"rules\":[\"a\"],\"news\":[}")]
    public void MalformedJson_LogsAnErrorAndGivesNothing(string json)
    {
        WriteInfoFile(json);

        var config = Create();

        AssertEmpty(config);
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    // Looking up a key unescapes top-level keys that could match it, and a lone surrogate there throws.
    [Theory]
    [InlineData("{\"motd\":[\"Welcome to EU-1\"],\"rules\":[\"No griefing\"],\"\\ud800\":0}")]
    [InlineData("{\"\\udc00note\":1}")]
    [InlineData("{\"motd\":[\"Welcome to EU-1\"],\"\\ud800news\":0}")]
    public void LoneSurrogateEscapeInATopLevelKey_LogsAnErrorAndGivesNothing(string json)
    {
        WriteInfoFile(json);

        var config = Create();

        AssertEmpty(config);
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void LoneSurrogateEscapeOutsideTheKnownKeys_IsIgnored()
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\"],\"note\":\"\\ud800\",\"meta\":{\"\\ud800\":1}}");

        var config = Create();

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
        Assert.Contains("motd 1 paragraph(s)", Assert.Single(LogsForThisTest()));
    }

    // Inside one link or news entry a broken key costs only that entry.
    [Fact]
    public void LoneSurrogateEscapeInAnEntryKey_SkipsOnlyThatEntry()
    {
        WriteInfoFile("{\"links\":[{\"url\":\"https://example.com/a\",\"\\ud800\":1},{\"url\":\"https://example.com/b\"}]," +
            "\"news\":[{\"title\":\"First\",\"\\udc00x\":1},{\"title\":\"Second\"}]}");

        var config = Create();

        Assert.Equal("https://example.com/b", Assert.Single(config.Links).Url);
        Assert.Equal("Second", Assert.Single(config.News).Title);
        Assert.Contains(LogsForThisTest(), log => log.Contains("link at index 0"));
        Assert.Contains(LogsForThisTest(), log => log.Contains("news in") && log.Contains("1 entry(s) that are not objects with a title or text"));
    }

    [Theory]
    [InlineData("{\"motd\":\"Welcome to EU-1\"}", "motd")]
    [InlineData("{\"motd\":42}", "motd")]
    [InlineData("{\"motd\":{\"line\":\"Welcome\"}}", "motd")]
    [InlineData("{\"motd\":null}", "motd")]
    [InlineData("{\"rules\":\"No griefing\"}", "rules")]
    [InlineData("{\"links\":{\"url\":\"https://example.com/\"}}", "links")]
    [InlineData("{\"news\":true}", "news")]
    public void KeyThatIsNotAnArray_LogsAWarningAndIsSkipped(string json, string key)
    {
        WriteInfoFile(json);

        var config = Create();

        AssertEmpty(config);
        Assert.Contains(LogsForThisTest(), log => log.Contains("Server info \"" + key + "\" in") && log.Contains("is not an array"));
    }

    [Fact]
    public void KeyThatIsNotAnArray_DoesNotStopTheOtherKeys()
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\"],\"rules\":\"No griefing\",\"news\":[{\"title\":\"Siege weekend\"}]}");

        var config = Create();

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
        Assert.Empty(config.Rules);
        Assert.Equal("Siege weekend", Assert.Single(config.News).Title);
    }

    [Theory]
    [InlineData("[\"Welcome to EU-1\"]")]
    [InlineData("\"Welcome to EU-1\"")]
    [InlineData("42")]
    public void RootThatIsNotAnObject_LogsAWarningAndGivesNothing(string json)
    {
        WriteInfoFile(json);

        var config = Create();

        AssertEmpty(config);
        Assert.Contains(LogsForThisTest(), log => log.Contains("is not a JSON object"));
    }

    [Theory]
    [InlineData("motd")]
    [InlineData("rules")]
    public void NonStringEntriesAreSkipped(string key)
    {
        WriteInfoFile("{\"" + key + "\":[1,\"Welcome to EU-1\",null,{\"a\":1},[\"b\"],true,\"Restart 06:00 UTC\"]}");

        var config = Create();

        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, key == "motd" ? config.Motd : config.Rules);
        Assert.Contains(LogsForThisTest(), log => log.Contains("\"" + key + "\" in") && log.Contains("has 5 entry(s) that are not valid strings"));
    }

    // An operator tool that cuts an escaped emoji in half used to stop the server from starting.
    [Theory]
    [InlineData("\\ud83d")]
    [InlineData("\\ude00 x")]
    [InlineData("a \\ud83d b")]
    [InlineData("\\ude00\\ud83d")]
    public void LoneSurrogateEscape_IsSkippedWithAWarning(string entry)
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\",\"" + entry + "\",\"Restart 06:00 UTC\"],\"rules\":[\"" + entry + "\",\"No griefing\"]}");

        var config = Create();

        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, config.Motd);
        Assert.Equal(new[] { "No griefing" }, config.Rules);
        Assert.Contains(LogsForThisTest(), log => log.Contains("\"motd\" in") && log.Contains("has 1 entry(s) that are not valid strings"));
        Assert.Contains(LogsForThisTest(), log => log.Contains("\"rules\" in") && log.Contains("has 1 entry(s) that are not valid strings"));
        Assert.Contains(LogsForThisTest(), log => log.Contains("motd 2 paragraph(s), rules 1"));
    }

    [Fact]
    public void RulesKeepTheFirstTwentyAndCutAt2000Characters()
    {
        var many = Enumerable.Range(1, 25).Select(index => "Rule " + index).ToArray();
        WriteInfoFile(Json(new { rules = many }));

        var config = Create();

        Assert.Equal(many.Take(ServerInfoLimits.MaxRules), config.Rules);
        Assert.Contains(LogsForThisTest(), log => log.Contains("\"rules\" in") && log.Contains("was cut to 20 entry(s) and 2000 characters"));

        lock (logs) logs.Clear();
        string first = new string('a', 1900);
        WriteInfoFile(Json(new { rules = new[] { first, new string('b', 300), "tail" } }));

        config = Create();

        Assert.Equal(new[] { first, new string('b', 100) }, config.Rules);
        Assert.Contains(LogsForThisTest(), log => log.Contains("\"rules\" in") && log.Contains("was cut to"));
    }

    // Every link rule the client applies is applied when the file is loaded, so a bad link never goes out.
    [Fact]
    public void BadLinks_AreDroppedWithAWarningNamingTheirIndex()
    {
        string[] urls =
        {
            "https://discord.gg/example",
            "javascript:void(0)",
            "file:///C:/Windows/win.ini",
            "ftp://example.com/file",
            "/relative/path",
            "discord.gg/example",
            "https://user@example.com/",
            "https://user:password@example.com/",
            "https://exa mple.com/",
            "https://example.com/\u0007",
            "https://example.com/" + new string('a', ServerInfoLimits.MaxLinkUrlLength),
            "https://example.com/rules",
        };
        WriteInfoFile(Json(new { links = urls.Select(url => new { label = "Link", url }).ToArray() }));

        var config = Create();

        Assert.Equal(new[] { "https://discord.gg/example", "https://example.com/rules" }, config.Links.Select(link => link.Url));
        for (int index = 1; index <= 10; index++)
            Assert.Contains(LogsForThisTest(), log => log.Contains("link at index " + index + " in"));
        Assert.DoesNotContain(LogsForThisTest(), log => log.Contains("link at index 0 ") || log.Contains("link at index 11 "));
        Assert.DoesNotContain(LogsForThisTest(), log => log.Contains("javascript") || log.Contains("win.ini"));
    }

    [Theory]
    [InlineData("{\"links\":[\"https://example.com/\"]}")]
    [InlineData("{\"links\":[{\"label\":\"Discord\"}]}")]
    [InlineData("{\"links\":[{\"label\":\"Discord\",\"url\":42}]}")]
    [InlineData("{\"links\":[{\"label\":\"Discord\",\"url\":null}]}")]
    [InlineData("{\"links\":[{\"label\":\"Discord\",\"url\":\"\\ud83d\"}]}")]
    [InlineData("{\"links\":[{\"label\":\"Discord\",\"url\":\"\"}]}")]
    public void LinkWithoutAUsableUrl_IsDropped(string json)
    {
        WriteInfoFile(json);

        var config = Create();

        Assert.Empty(config.Links);
        Assert.Contains(LogsForThisTest(), log => log.Contains("link at index 0 in"));
    }

    [Fact]
    public void LinkAddressesAreStoredNormalized()
    {
        WriteInfoFile(Json(new
        {
            links = new[]
            {
                new { label = "Shop", url = "https://bücher.example/pfad" },
                new { label = "Site", url = "HTTPS://Example.COM:443/Rules" },
            },
        }));

        var config = Create();

        Assert.Equal(new[] { "https://xn--bcher-kva.example/pfad", "https://example.com/Rules" }, config.Links.Select(link => link.Url));
    }

    // The client shows the address when the label is empty, so these keep the link with an empty label.
    [Theory]
    [InlineData("{\"links\":[{\"url\":\"https://example.com/\"}]}")]
    [InlineData("{\"links\":[{\"label\":\"\",\"url\":\"https://example.com/\"}]}")]
    [InlineData("{\"links\":[{\"label\":\"   \",\"url\":\"https://example.com/\"}]}")]
    [InlineData("{\"links\":[{\"label\":7,\"url\":\"https://example.com/\"}]}")]
    [InlineData("{\"links\":[{\"label\":\"\\ud83d\",\"url\":\"https://example.com/\"}]}")]
    public void MissingOrUnusableLabel_KeepsTheLinkWithAnEmptyLabel(string json)
    {
        WriteInfoFile(json);

        var config = Create();

        var link = Assert.Single(config.Links);
        Assert.Equal(string.Empty, link.Label);
        Assert.Equal("https://example.com/", link.Url);
    }

    [Fact]
    public void LinksKeepTheFirstEightAndLabelsAreCut()
    {
        var links = Enumerable.Range(1, 10)
            .Select(index => new { label = index == 1 ? new string('L', 50) : "Link " + index, url = "https://example.com/" + index })
            .ToArray();
        WriteInfoFile(Json(new { links }));

        var config = Create();

        Assert.Equal(ServerInfoLimits.MaxLinks, config.Links.Count);
        Assert.Equal(new string('L', ServerInfoLimits.MaxLinkLabelLength), config.Links[0].Label);
        Assert.Equal("https://example.com/8", config.Links[7].Url);
        Assert.Contains(LogsForThisTest(), log => log.Contains("links in") && log.Contains("were cut to 8 link(s) and 40 label characters"));
    }

    [Fact]
    public void BadLinksAfterTheCap_DoNotCountAsACut()
    {
        var links = Enumerable.Range(1, 8).Select(index => new { url = "https://example.com/" + index }).ToList();
        links.Add(new { url = "javascript:void(0)" });
        WriteInfoFile(Json(new { links }));

        var config = Create();

        Assert.Equal(8, config.Links.Count);
        Assert.DoesNotContain(LogsForThisTest(), log => log.Contains("were cut"));
        Assert.Contains(LogsForThisTest(), log => log.Contains("link at index 8 in"));
    }

    [Fact]
    public void NewsFieldsAreOptionalButAnEntryNeedsATitleOrText()
    {
        WriteInfoFile("{\"news\":[{\"title\":\"Only a title\"},{\"text\":\"Only text\"},{\"date\":\"28 Sep\"},{}," +
            "\"not an object\",42,{\"date\":7,\"title\":[\"x\"],\"text\":\"Wrong typed fields read as empty\"}," +
            "{\"title\":\"   \",\"text\":\"\\u0007\"}]}");

        var config = Create();

        Assert.Equal(new[] { ("", "Only a title", ""), ("", "", "Only text"), ("", "", "Wrong typed fields read as empty") },
            config.News.Select(item => (item.Date, item.Title, item.Text)));
        Assert.Contains(LogsForThisTest(), log => log.Contains("news in") && log.Contains("has 5 entry(s) that are not objects with a title or text"));
    }

    [Fact]
    public void NewsKeepsTheFirstTenAndCutsItsFields()
    {
        var news = Enumerable.Range(1, 12).Select(index => new
        {
            date = index == 1 ? new string('d', 50) : "Day " + index,
            title = index == 1 ? new string('t', 100) : "Title " + index,
            text = index == 1 ? new string('x', 600) : "Text " + index,
        }).ToArray();
        WriteInfoFile(Json(new { news }));

        var config = Create();

        Assert.Equal(ServerInfoLimits.MaxNews, config.News.Count);
        var first = config.News[0];
        Assert.Equal(
            (ServerInfoLimits.MaxNewsDateLength, ServerInfoLimits.MaxNewsTitleLength, ServerInfoLimits.MaxNewsTextLength),
            (first.Date.Length, first.Title.Length, first.Text.Length));
        Assert.Equal("Title 10", config.News[9].Title);
        Assert.Contains(LogsForThisTest(), log => log.Contains("news in") && log.Contains("was cut to 10 entry(s) with 40 date, 80 title and 500 text characters"));
    }

    // The per-key caps keep motd, rules and links under the total, so only the last news entries can go over it.
    [Fact]
    public void PerKeyCapsLeaveRoomForNewsUnderTheTotal()
    {
        int others = ServerInfoLimits.MaxMotdLength + ServerInfoLimits.MaxRulesLength +
            (ServerInfoLimits.MaxLinks * (ServerInfoLimits.MaxLinkLabelLength + ServerInfoLimits.MaxLinkUrlLength));
        int news = ServerInfoLimits.MaxNews *
            (ServerInfoLimits.MaxNewsDateLength + ServerInfoLimits.MaxNewsTitleLength + ServerInfoLimits.MaxNewsTextLength);

        Assert.True(others < ServerInfoLimits.MaxTotalLength);
        Assert.True(others + news > ServerInfoLimits.MaxTotalLength);
    }

    [Fact]
    public void FullFile_IsCutToTheTotalByDroppingTheLastNews()
    {
        WriteInfoFile(Json(new
        {
            motd = Enumerable.Range(0, 10).Select(index => new string((char)('a' + index), 200)).ToArray(),
            rules = Enumerable.Range(0, 20).Select(index => new string((char)('a' + index), 100)).ToArray(),
            links = Enumerable.Range(0, 8).Select(index => new
            {
                label = new string('L', 40),
                url = "https://example.com/" + index + new string('p', ServerInfoLimits.MaxLinkUrlLength - 21),
            }).ToArray(),
            news = Enumerable.Range(0, 10).Select(index => new
            {
                date = new string('d', 40),
                title = "News " + index + new string('t', 74),
                text = new string('x', 500),
            }).ToArray(),
        }));

        var config = Create();

        int total = config.Motd.Sum(text => text.Length) + config.Rules.Sum(text => text.Length) +
            config.Links.Sum(link => link.Label.Length + link.Url.Length) +
            config.News.Sum(item => item.Date.Length + item.Title.Length + item.Text.Length);
        Assert.Equal((10, 20, 8), (config.Motd.Count, config.Rules.Count, config.Links.Count));
        Assert.InRange(config.News.Count, 1, ServerInfoLimits.MaxNews - 1);
        Assert.InRange(total, ServerInfoLimits.MaxTotalLength - 620, ServerInfoLimits.MaxTotalLength);
        Assert.Equal(Enumerable.Range(0, config.News.Count).Select(index => "News " + index), config.News.Select(item => item.Title.Substring(0, 6)));
        Assert.Contains(LogsForThisTest(), log => log.Contains("news in") && log.Contains("under 12000 characters"));
    }

    [Fact]
    public void FolderAtThePath_LogsAnErrorAndGivesNothing()
    {
        Directory.CreateDirectory(InfoFilePath);

        var config = Create();

        AssertEmpty(config);
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void InvalidPath_LogsAnErrorAndIsNotFatal()
    {
        var config = new ServerInfoConfig(directory + Path.DirectorySeparatorChar + "bad\0name.json", new ServerInfoLinkRules());

        AssertEmpty(config);
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    public void Dispose()
    {
        OutputSinkManager.RemoveLogCallback(CaptureLog);
        if (!Directory.Exists(directory)) return;

        Directory.Delete(directory, recursive: true);
    }

    private ServerInfoConfig Create() => new(InfoFilePath, new ServerInfoLinkRules());

    private static void AssertEmpty(IServerInfoConfig config)
    {
        Assert.Empty(config.Motd);
        Assert.Empty(config.Rules);
        Assert.Empty(config.Links);
        Assert.Empty(config.News);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value);

    private void WriteInfoFile(string json)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(InfoFilePath, json);
    }

    // Other tests log in parallel, so keep only the lines that name this test's folder.
    private string[] LogsForThisTest()
    {
        lock (logs)
        {
            return logs.Where(log => log.Contains(directory)).ToArray();
        }
    }

    private void CaptureLog(string message)
    {
        lock (logs)
        {
            logs.Add(message);
        }
    }
}
