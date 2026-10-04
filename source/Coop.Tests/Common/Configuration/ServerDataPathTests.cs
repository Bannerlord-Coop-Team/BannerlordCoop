using Coop.Core.Common.Configuration;
using Coop.Core.Server.Connections;
using Moq;
using System.IO;
using Xunit;

namespace Coop.Tests.Configuration;

/// <summary>Tests the lookup order shared by the server's operator files.</summary>
public sealed class ServerDataPathTests
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "BannerlordCoop-ServerDataPathTests");

    [Fact]
    public void FileVariableWinsOverDataDirectory()
    {
        string infoFile = Path.Combine(directory, "info", "eu-1.json");

        string path = ServerDataPath.Resolve(
            infoFile,
            Path.Combine(directory, "configured-data"),
            Path.Combine(directory, "engine", "bin", "server"),
            "server-info.json");

        Assert.Equal(infoFile, path);
    }

    [Fact]
    public void DataDirectoryWinsOverDeploymentFallback()
    {
        string dataDirectory = Path.Combine(directory, "configured-data");

        string path = ServerDataPath.Resolve(
            null,
            dataDirectory,
            Path.Combine(directory, "engine", "bin", "server"),
            "server-info.json");

        Assert.Equal(Path.Combine(dataDirectory, "server-info.json"), path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankValuesFallBackToServerDataBesideTheEngine(string? blank)
    {
        string path = ServerDataPath.Resolve(
            blank,
            blank,
            Path.Combine(directory, "engine", "bin", "server"),
            "server-info.json");

        Assert.Equal(Path.GetFullPath(Path.Combine(directory, "server-data", "server-info.json")), path);
    }

    [Fact]
    public void BanListAsksTheDataPathForItsOwnFile()
    {
        var dataPath = new Mock<IServerDataPath>();
        dataPath.Setup(path => path.Resolve("COOP_STEAM_BAN_FILE", "steam-bans.json"))
            .Returns(Path.Combine(directory, "missing", "steam-bans.json"));

        var banList = new SteamBanList(dataPath.Object);

        Assert.False(banList.IsBanned("76561198000000042"));
        dataPath.Verify(path => path.Resolve("COOP_STEAM_BAN_FILE", "steam-bans.json"), Times.Once);
    }
}
