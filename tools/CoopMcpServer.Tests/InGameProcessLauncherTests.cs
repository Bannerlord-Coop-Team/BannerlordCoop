namespace CoopMcpServer.Tests;

public sealed class InGameProcessLauncherTests
{
    [Theory]
    [InlineData("server", false)]
    [InlineData("client", true)]
    public void LaunchArgumentsAreStructuredAndClientsDeferJoining(string role, bool deferred)
    {
        var profile = new LaunchProfile { Executable = @"C:\Game Folder\Bannerlord.exe" };
        var info = new InGameProcessLauncher(new OwnedProcessFactory()).CreateStartInfo(profile, role, "testclient1", "run-token");
        Assert.False(info.UseShellExecute);
        Assert.Equal(profile.Executable, info.FileName);
        Assert.Equal(@"C:\Game Folder", info.WorkingDirectory);
        Assert.Contains("/" + role, info.ArgumentList);
        Assert.Contains("/autoconnect", info.ArgumentList);
        Assert.Contains("run-token", info.ArgumentList);
        Assert.Equal(deferred, info.ArgumentList.Contains("/cooptestmanualjoin"));
        Assert.Contains("_MODULES_*Native*SandBoxCore*SandBox*StoryMode*Coop*_MODULES_", info.ArgumentList);
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

    [Theory]
    [InlineData("Bannerlord.Harmony", true)]
    [InlineData("Sandbox_Core-2", true)]
    [InlineData("..", false)]
    [InlineData(".Hidden", false)]
    [InlineData("Trailing.", false)]
    [InlineData("A..B", false)]
    [InlineData("A/B", false)]
    public void ModuleIdsMayBeDottedButNeverNameAFolderOutsideModules(string module, bool accepted)
    {
        Assert.Equal(accepted, LaunchProfile.IsModuleId(module));
    }

    // A native message box (the shader cache's "Mod change detected", a shader compile error) waits for a click and
    // stalls an unattended run. The engine's own launch flag logs those boxes instead of showing them.
    [Theory]
    [InlineData("server")]
    [InlineData("client")]
    public void EveryRoleLogsNativeMessageBoxesInsteadOfShowingThem(string role)
    {
        var info = new InGameProcessLauncher(new OwnedProcessFactory()).CreateStartInfo(
            new LaunchProfile { Executable = @"C:\Game Folder\Bannerlord.exe" }, role, "identity", "token");
        Assert.Contains("/suppress_message_boxes", info.ArgumentList);
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
