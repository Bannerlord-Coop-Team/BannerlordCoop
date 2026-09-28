using Common.Logging;
using Coop.Core.Common.Configuration;
using GameInterface.Services.UI.Motd;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Coop.Tests.Configuration;

/// <summary>Tests the operator's server-info.json and its MOTD rules.</summary>
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
    public void MissingFile_GivesNoMotdAndNoLog()
    {
        Directory.CreateDirectory(directory);

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Empty(config.Motd);
        Assert.Empty(LogsForThisTest());
    }

    [Fact]
    public void MissingFolder_GivesNoMotdAndNoLog()
    {
        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Empty(config.Motd);
        Assert.Empty(LogsForThisTest());
    }

    [Fact]
    public void AsksTheDataPathForTheInfoFile()
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\"]}");
        var dataPath = new Mock<IServerDataPath>();
        dataPath.Setup(path => path.Resolve("COOP_SERVER_INFO_FILE", "server-info.json")).Returns(InfoFilePath);

        var config = new ServerInfoConfig(dataPath.Object);

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
    }

    [Fact]
    public void DataPathThatThrows_LogsAnErrorAndIsNotFatal()
    {
        var dataPath = new Mock<IServerDataPath>();
        dataPath.Setup(path => path.Resolve(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new ArgumentException("Illegal characters in path " + directory));

        var config = new ServerInfoConfig(dataPath.Object);

        Assert.Empty(config.Motd);
        // The log carries the exception, which names this test's folder.
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void ReadsParagraphsInOrderAndLogsOnlyTheCount()
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\",\"Restart 06:00 UTC\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, config.Motd);
        string loaded = Assert.Single(LogsForThisTest());
        Assert.Contains("motd 2 paragraph(s)", loaded);
        Assert.DoesNotContain("Welcome", loaded);
    }

    [Fact]
    public void UnknownKeysAreIgnored()
    {
        WriteInfoFile("{\"serverName\":\"EU-1 Test\",\"region\":\"EU\",\"motd\":[\"Welcome to EU-1\"],\"extra\":{\"a\":1}}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
    }

    [Fact]
    public void FileWithoutMotd_GivesNoMotd()
    {
        WriteInfoFile("{\"serverName\":\"EU-1 Test\"}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Empty(config.Motd);
        Assert.Contains("motd 0 paragraph(s)", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void KeepsTheFirstTenNonEmptyParagraphs()
    {
        string[] entries = { "", "p1", "   ", "p2", "p3", "\\n", "p4", "p5", "p6", "p7", "p8", "p9", "p10", "p11", "p12" };
        WriteInfoFile("{\"motd\":[" + string.Join(",", entries.Select(entry => "\"" + entry + "\"")) + "]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(MotdLimits.MaxParagraphs, config.Motd.Count);
        Assert.Equal(Enumerable.Range(1, 10).Select(index => "p" + index), config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("was cut to 10 paragraph(s) and 2000 characters"));
    }

    [Fact]
    public void ExactlyTheCaps_AreNotReportedAsCut()
    {
        string[] entries = Enumerable.Range(0, MotdLimits.MaxParagraphs)
            .Select(index => new string((char)('a' + index), MotdLimits.MaxLength / MotdLimits.MaxParagraphs)).ToArray();
        WriteInfoFile("{\"motd\":[" + string.Join(",", entries.Select(entry => "\"" + entry + "\"")) + "]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(entries, config.Motd);
        Assert.Equal(MotdLimits.MaxLength, config.Motd.Sum(paragraph => paragraph.Length));
        Assert.DoesNotContain(LogsForThisTest(), log => log.Contains("was cut"));
    }

    [Fact]
    public void TotalLengthIsCappedAndLaterParagraphsAreDropped()
    {
        string first = new string('a', 1500);
        string second = new string('b', 800);
        WriteInfoFile("{\"motd\":[\"" + first + "\",\"" + second + "\",\"tail\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

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

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { first, new string('b', 499) }, config.Motd);
    }

    [Fact]
    public void ParagraphAfterAFullMotd_IsDroppedAndReported()
    {
        string full = new string('a', MotdLimits.MaxLength);
        WriteInfoFile("{\"motd\":[\"" + full + "\",\"x\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { full }, config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("was cut to"));
    }

    [Fact]
    public void CutNeverSplitsASurrogatePair()
    {
        // The pair starts at the last allowed index, so a plain cut would keep only its high half.
        string line = new string('a', MotdLimits.MaxLength - 1) + "\U0001F600" + "tail";
        WriteInfoFile("{\"motd\":[\"" + line + "\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        string cut = Assert.Single(config.Motd);
        Assert.Equal(new string('a', MotdLimits.MaxLength - 1), cut);
        Assert.False(char.IsHighSurrogate(cut[cut.Length - 1]));
    }

    [Fact]
    public void ControlCharactersBecomeSpacesAndParagraphsAreTrimmed()
    {
        WriteInfoFile("{\"motd\":[\"  Rules:\\tbe nice\\nno griefing\\u0007  \"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal("Rules: be nice no griefing", Assert.Single(config.Motd));
    }

    [Fact]
    public void BracesAndTagsAreKeptAsWritten()
    {
        WriteInfoFile("{\"motd\":[\"{PLAYER} {=coop_motd_title}x <b>bold</b>\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal("{PLAYER} {=coop_motd_title}x <b>bold</b>", Assert.Single(config.Motd));
    }

    [Fact]
    public void NonAsciiTextAndEscapedPairsAreKept()
    {
        WriteInfoFile("{\"motd\":[\"Willkommen überall, 欢迎\",\"smile \\ud83d\\ude00\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { "Willkommen überall, 欢迎", "smile \U0001F600" }, config.Motd);
    }

    [Fact]
    public void FileSavedWithAByteOrderMarkIsRead()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(InfoFilePath, "{\"motd\":[\"Welcome to EU-1\"]}", new UTF8Encoding(true));

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("{\"motd\":[\"a\",]}")]
    public void MalformedJson_LogsAnErrorAndGivesNoMotd(string json)
    {
        WriteInfoFile(json);

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Empty(config.Motd);
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    // Looking up motd unescapes top-level keys that could match it, and a lone surrogate there throws.
    [Theory]
    [InlineData("{\"motd\":[\"Welcome to EU-1\"],\"\\ud800\":0}")]
    [InlineData("{\"\\udc00note\":1}")]
    public void LoneSurrogateEscapeInAKey_LogsAnErrorAndIsNotFatal(string json)
    {
        WriteInfoFile(json);

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Empty(config.Motd);
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void LoneSurrogateEscapeOutsideTheMotd_IsIgnored()
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\"],\"note\":\"\\ud800\",\"meta\":{\"\\ud800\":1}}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { "Welcome to EU-1" }, config.Motd);
        Assert.Contains("motd 1 paragraph(s)", Assert.Single(LogsForThisTest()));
    }

    [Theory]
    [InlineData("{\"motd\":\"Welcome to EU-1\"}")]
    [InlineData("{\"motd\":42}")]
    [InlineData("{\"motd\":{\"line\":\"Welcome\"}}")]
    [InlineData("[\"Welcome to EU-1\"]")]
    [InlineData("\"Welcome to EU-1\"")]
    public void WrongShape_LogsAWarningAndGivesNoMotd(string json)
    {
        WriteInfoFile(json);

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Empty(config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("has no motd array of strings"));
    }

    [Fact]
    public void NonStringEntriesAreSkipped()
    {
        WriteInfoFile("{\"motd\":[1,\"Welcome to EU-1\",null,{\"a\":1},[\"b\"],true,\"Restart 06:00 UTC\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("has 5 entry(s) that are not valid strings"));
    }

    // An operator tool that cuts an escaped emoji in half used to stop the server from starting.
    [Theory]
    [InlineData("\\ud83d")]
    [InlineData("\\ude00 x")]
    [InlineData("a \\ud83d b")]
    [InlineData("\\ude00\\ud83d")]
    public void LoneSurrogateEscape_IsSkippedWithAWarning(string entry)
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\",\"" + entry + "\",\"Restart 06:00 UTC\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("has 1 entry(s) that are not valid strings"));
        Assert.Contains(LogsForThisTest(), log => log.Contains("motd 2 paragraph(s)"));
    }

    [Fact]
    public void FolderAtThePath_LogsAnErrorAndGivesNoMotd()
    {
        Directory.CreateDirectory(InfoFilePath);

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Empty(config.Motd);
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void InvalidPath_LogsAnErrorAndIsNotFatal()
    {
        var config = new ServerInfoConfig(directory + Path.DirectorySeparatorChar + "bad\0name.json");

        Assert.Empty(config.Motd);
        Assert.Contains("could not be read", Assert.Single(LogsForThisTest()));
    }

    public void Dispose()
    {
        OutputSinkManager.RemoveLogCallback(CaptureLog);
        if (!Directory.Exists(directory)) return;

        Directory.Delete(directory, recursive: true);
    }

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
