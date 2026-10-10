using System.Diagnostics;

namespace CoopMcpServer.TestHost;

// A harmless root/child/grandchild tree with deliberately inherited output handles.
public sealed class BuildTreeFixture
{
    public async Task RunAsync(string directory, int depth, int exitCode)
    {
        if (depth < 2)
        {
            var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "CoopMcpServer.TestHost.exe")) { UseShellExecute = false };
            foreach (string arg in new[] { "build-tree", directory, (depth + 1).ToString(), exitCode.ToString() }) info.ArgumentList.Add(arg);
            using var child = Process.Start(info);
        }
        Console.Out.WriteLine("stdout from depth " + depth);
        Console.Error.WriteLine("stderr from depth " + depth);
        File.WriteAllText(Path.Combine(directory, depth + ".pid"), Environment.ProcessId.ToString());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        if (depth == 0)
        {
            while (!File.Exists(Path.Combine(directory, "release-root"))) await Task.Delay(20, deadline.Token);
            Environment.ExitCode = exitCode;
            return;
        }
        await Task.Delay(TimeSpan.FromSeconds(60), deadline.Token);
    }
}
