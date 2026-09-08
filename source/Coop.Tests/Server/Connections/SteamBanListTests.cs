using Coop.Core.Server.Connections;
using System;
using System.IO;
using Xunit;

namespace Coop.Tests.Server.Connections;

/// <summary>Tests the dedicated-server Steam ban list.</summary>
public sealed class SteamBanListTests : IDisposable
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "BannerlordCoop-SteamBanListTests-" + Guid.NewGuid().ToString("N"));

    private string BanFilePath => Path.Combine(directory, "steam-bans.json");

    [Fact]
    public void IsBanned_ReadsIdsAndNamedEntries()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(BanFilePath,
            "{\"steamIds\":[\"76561198000000042\"]," +
            "\"entries\":[{\"steamId\":\"76561198000000043\",\"heroName\":\"Player\"}]}");
        var banList = new SteamBanList(BanFilePath);

        Assert.True(banList.IsBanned("76561198000000042"));
        Assert.True(banList.IsBanned("76561198000000043"));
        Assert.False(banList.IsBanned("76561198000000044"));
    }

    [Fact]
    public void IsBanned_ReloadsChangedFileAndClearsDeletedFile()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(BanFilePath, "{\"steamIds\":[\"76561198000000042\"]}");
        var banList = new SteamBanList(BanFilePath);
        Assert.True(banList.IsBanned("76561198000000042"));

        File.WriteAllText(BanFilePath, "{\"steamIds\":[\"76561198000000043\"]}");
        File.SetLastWriteTimeUtc(BanFilePath, DateTime.UtcNow.AddSeconds(2));
        Assert.False(banList.IsBanned("76561198000000042"));
        Assert.True(banList.IsBanned("76561198000000043"));

        File.Delete(BanFilePath);
        Assert.False(banList.IsBanned("76561198000000043"));
    }

    [Fact]
    public void IsBanned_MalformedReloadKeepsLastValidList()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(BanFilePath, "{\"steamIds\":[\"76561198000000042\"]}");
        var banList = new SteamBanList(BanFilePath);
        Assert.True(banList.IsBanned("76561198000000042"));

        File.WriteAllText(BanFilePath, "{");
        File.SetLastWriteTimeUtc(BanFilePath, DateTime.UtcNow.AddSeconds(2));

        Assert.True(banList.IsBanned("76561198000000042"));
    }

    [Fact]
    public void IsBanned_UnreadableReplacementKeepsLastValidList()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(BanFilePath, "{\"steamIds\":[\"76561198000000042\"]}");
        var banList = new SteamBanList(BanFilePath);
        Assert.True(banList.IsBanned("76561198000000042"));

        File.Delete(BanFilePath);
        Directory.CreateDirectory(BanFilePath);

        Assert.True(banList.IsBanned("76561198000000042"));
    }

    [Fact]
    public void ResolvePath_ConfiguredDataDirectoryWinsOverDeploymentFallback()
    {
        string applicationDirectory = Path.Combine(directory, "engine", "bin", "server");
        string deploymentDataDirectory = Path.Combine(directory, "server-data");
        string configuredDataDirectory = Path.Combine(directory, "configured-data");
        Directory.CreateDirectory(applicationDirectory);
        Directory.CreateDirectory(deploymentDataDirectory);
        Directory.CreateDirectory(configuredDataDirectory);
        File.WriteAllText(Path.Combine(deploymentDataDirectory, "steam-bans.json"), "{}");
        File.WriteAllText(Path.Combine(configuredDataDirectory, "steam-bans.json"), "{}");

        string path = SteamBanList.ResolvePath(
            null,
            configuredDataDirectory,
            applicationDirectory);

        Assert.Equal(Path.Combine(configuredDataDirectory, "steam-bans.json"), path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server")]
    [InlineData("76561198000000abc")]
    public void Normalize_RejectsNonSteamIdentities(string? value)
    {
        Assert.Null(SteamBanList.Normalize(value));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
