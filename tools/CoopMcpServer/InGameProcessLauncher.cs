using System.Diagnostics;

namespace CoopMcpServer;

public interface IOwnedProcess : IDisposable
{
    int Pid { get; }
    DateTime StartedUtc { get; }
    bool IsAlive { get; }
    bool IsTreeAlive { get; }
    Task StopAsync(TimeSpan grace);
}

public interface IGameProcessLauncher
{
    IOwnedProcess Launch(LaunchProfile profile, string role, string platformId, string runToken, string saveName = null);
}

public sealed class InGameProcessLauncher : IGameProcessLauncher
{
    private readonly IOwnedProcessFactory processes;
    public InGameProcessLauncher(IOwnedProcessFactory processes) { this.processes = processes; }

    public IOwnedProcess Launch(LaunchProfile profile, string role, string platformId, string runToken, string saveName = null) =>
        processes.Start(CreateStartInfo(profile, role, platformId, runToken, saveName));

    public ProcessStartInfo CreateStartInfo(LaunchProfile profile, string role, string platformId, string runToken, string saveName = null)
    {
        var info = new ProcessStartInfo(profile.Executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(profile.Executable),
        };
        foreach (string argument in new[] { "/singleplayer", "/" + role, "/autoconnect",
            "/platformId", platformId, "/cooptestrun", runToken })
            info.ArgumentList.Add(argument);
        if (role == "client") info.ArgumentList.Add("/cooptestmanualjoin");
        if (role == "server" && saveName != null)
        {
            info.ArgumentList.Add("/coopsave");
            info.ArgumentList.Add(saveName);
        }
        info.ArgumentList.Add("_MODULES_*" + string.Join("*", profile.Modules) + "*_MODULES_");
        return info;
    }
}
