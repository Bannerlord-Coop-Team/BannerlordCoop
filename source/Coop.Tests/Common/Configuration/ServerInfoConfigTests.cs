using Common.Logging;
using Coop.Core.Common.Configuration;
using GameInterface.Services.Chat.Messages;
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
    public void ReadsMotdLinesInOrderAndLogsOnlyTheCount()
    {
        WriteInfoFile("{\"motd\":[\"Welcome to EU-1\",\"Restart 06:00 UTC\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, config.Motd);
        string loaded = Assert.Single(LogsForThisTest());
        Assert.Contains("motd 2 line(s)", loaded);
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
        Assert.Contains("motd 0 line(s)", Assert.Single(LogsForThisTest()));
    }

    [Fact]
    public void KeepsTheFirstFiveNonEmptyLines()
    {
        WriteInfoFile("{\"motd\":[\"\",\"one\",\"   \",\"two\",\"three\",\"\\n\",\"four\",\"five\",\"six\",\"seven\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(ServerInfoConfig.MaxMotdLines, config.Motd.Count);
        Assert.Equal(new[] { "one", "two", "three", "four", "five" }, config.Motd);
        Assert.Contains(LogsForThisTest(), log => log.Contains("was cut to 5 line(s)"));
    }

    [Fact]
    public void ExactlyFiveLines_AreNotReportedAsCut()
    {
        WriteInfoFile("{\"motd\":[\"one\",\"two\",\"three\",\"four\",\"five\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(5, config.Motd.Count);
        Assert.DoesNotContain(LogsForThisTest(), log => log.Contains("was cut"));
    }

    [Fact]
    public void ControlCharactersBecomeSpacesAndLinesAreTrimmed()
    {
        WriteInfoFile("{\"motd\":[\"  Rules:\\tbe nice\\nno griefing\\u0007  \"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal("Rules: be nice no griefing", Assert.Single(config.Motd));
    }

    [Fact]
    public void LongLinesAreCutToTheChatLimit()
    {
        string longLine = new string('a', ChatMessageLimits.MaxMessageLength + 50);
        string exactLine = new string('b', ChatMessageLimits.MaxMessageLength);
        WriteInfoFile("{\"motd\":[\"" + longLine + "\",\"" + exactLine + "\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal(new string('a', ChatMessageLimits.MaxMessageLength), config.Motd[0]);
        Assert.Equal(exactLine, config.Motd[1]);
        Assert.Contains(LogsForThisTest(), log => log.Contains("was cut to"));
    }

    [Fact]
    public void CutNeverSplitsASurrogatePair()
    {
        // The pair starts at the last allowed index, so a plain cut would keep only its high half.
        string line = new string('a', ChatMessageLimits.MaxMessageLength - 1) + "\U0001F600" + "tail";
        WriteInfoFile("{\"motd\":[\"" + line + "\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        string cut = Assert.Single(config.Motd);
        Assert.Equal(new string('a', ChatMessageLimits.MaxMessageLength - 1), cut);
        Assert.False(char.IsHighSurrogate(cut[cut.Length - 1]));
    }

    [Fact]
    public void NonAsciiTextIsKept()
    {
        WriteInfoFile("{\"motd\":[\"Willkommen \u00fcberall, \u6b22\u8fce\"]}");

        var config = new ServerInfoConfig(InfoFilePath);

        Assert.Equal("Willkommen \u00fcberall, \u6b22\u8fce", Assert.Single(config.Motd));
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
        Assert.Contains(LogsForThisTest(), log => log.Contains("has 5 entry(s) that are not strings"));
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

    [Fact]
    public void ResolvePath_InfoFileVariableWinsOverDataDirectory()
    {
        string infoFile = Path.Combine(directory, "info", "eu-1.json");

        string path = ServerInfoConfig.ResolvePath(
            infoFile,
            Path.Combine(directory, "configured-data"),
            Path.Combine(directory, "engine", "bin", "server"));

        Assert.Equal(infoFile, path);
    }

    [Fact]
    public void ResolvePath_DataDirectoryWinsOverDeploymentFallback()
    {
        string dataDirectory = Path.Combine(directory, "configured-data");

        string path = ServerInfoConfig.ResolvePath(
            null,
            dataDirectory,
            Path.Combine(directory, "engine", "bin", "server"));

        Assert.Equal(Path.Combine(dataDirectory, "server-info.json"), path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ResolvePath_BlankValuesFallBackToServerDataBesideTheEngine(string? blank)
    {
        string path = ServerInfoConfig.ResolvePath(
            blank,
            blank,
            Path.Combine(directory, "engine", "bin", "server"));

        Assert.Equal(Path.GetFullPath(Path.Combine(directory, "server-data", "server-info.json")), path);
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
