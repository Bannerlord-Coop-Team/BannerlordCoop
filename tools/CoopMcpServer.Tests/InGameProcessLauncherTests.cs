namespace CoopMcpServer.Tests;

public sealed class InGameProcessLauncherTests
{
    [Theory]
    [InlineData("server", false)]
    [InlineData("client", false)]
    [InlineData("client", true)]
    [InlineData("server", true)]
    public void LaunchArgumentsUseAutoConnectUnlessClientJoinIsDeferred(string role, bool deferClientJoin)
    {
        var profile = new LaunchProfile { Executable = @"C:\Game Folder\Bannerlord.exe" };
        var info = new InGameProcessLauncher(new OwnedProcessFactory()).CreateStartInfo(profile, role, "testclient1", "run-token", deferClientJoin: deferClientJoin);
        Assert.False(info.UseShellExecute);
        Assert.Equal(profile.Executable, info.FileName);
        Assert.Equal(@"C:\Game Folder", info.WorkingDirectory);
        var expected = new List<string> { "/singleplayer", "/" + role, "/autoconnect",
            "/platformId", "testclient1", "/cooptestrun", "run-token" };
        if (role == "client" && deferClientJoin) expected.Add("/cooptestmanualjoin");
        expected.Add("_MODULES_*Native*SandBoxCore*SandBox*StoryMode*Coop*_MODULES_");
        Assert.Equal(expected, info.ArgumentList);
    }

    [Theory]
    [InlineData("server", true)]
    [InlineData("client", false)]
    public void SaveSelectionIsOneArgumentAndOnlyControlsServerStartup(string role, bool selected)
    {
        var info = new InGameProcessLauncher(new OwnedProcessFactory()).CreateStartInfo(
            new LaunchProfile { Executable = @"C:\Game Folder\Bannerlord.exe" }, role, "identity", "token", "Danustica campaign");
        Assert.Equal(selected, info.ArgumentList.Contains("/coopsave"));
        Assert.Equal(selected, info.ArgumentList.Contains("Danustica campaign"));
        Assert.Contains("/autoconnect", info.ArgumentList);
        Assert.DoesNotContain("/coopowner", info.ArgumentList);
    }

    [Fact]
    public void DuplicatePlatformIdsAreRejectedBeforeLaunch()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "Bannerlord.exe");
            File.WriteAllText(path, "fake, never launched");
            var profile = new LaunchProfile { Executable = path, ClientPlatformIds = new[] { "testclient", "TESTCLIENT" } };
            Assert.Throws<ArgumentException>(() => profile.Validate(2));
        }
        finally { Directory.Delete(directory, true); }
    }
}
