namespace CoopMcpServer.Tests;

public sealed class InGameProcessLauncherTests
{
    [Theory]
    [InlineData("server", false)]
    [InlineData("client", true)]
    public void LaunchArgumentsAreStructuredAndClientsDeferJoining(string role, bool deferred)
    {
        var profile = new LaunchProfile { Executable = @"C:\Game Folder\Bannerlord.exe" };
        var info = new InGameProcessLauncher().CreateStartInfo(profile, role, "testclient1", "run-token");
        Assert.False(info.UseShellExecute);
        Assert.Equal(profile.Executable, info.FileName);
        Assert.Equal(@"C:\Game Folder", info.WorkingDirectory);
        Assert.Contains("/" + role, info.ArgumentList);
        Assert.Contains("/autoconnect", info.ArgumentList);
        Assert.Contains("run-token", info.ArgumentList);
        Assert.Equal(deferred, info.ArgumentList.Contains("/cooptestmanualjoin"));
        Assert.Contains("_MODULES_*Native*SandBoxCore*SandBox*StoryMode*Coop*_MODULES_", info.ArgumentList);
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
