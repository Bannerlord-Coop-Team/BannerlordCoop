using System.Diagnostics;
using System.Text;

namespace CoopMcpServer;

public sealed record FfmpegResult(int? ExitCode, bool TimedOut, bool Stalled, string Progress, string ErrorTail);

public interface IFfmpegRunner
{
    // stallTimeout stops ffmpeg when its -progress frame count has not advanced for that long, including before the first frame.
    Task<FfmpegResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, TimeSpan? stallTimeout, CancellationToken cancellationToken);
}

public sealed class FfmpegRunner : IFfmpegRunner
{
    private const int TailCharacters = 4096;

    public async Task<FfmpegResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, TimeSpan? stallTimeout, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start);
        if (process == null) throw new IOException("ffmpeg did not start.");
        long started = Environment.TickCount64;
        long lastAdvance = started, lastFrame = 0;
        var progress = new StringBuilder();
        var output = Task.Run(async () =>
        {
            string line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
            {
                lock (progress)
                {
                    progress.Append(line).Append('\n');
                    if (progress.Length > 2 * TailCharacters) progress.Remove(0, progress.Length - TailCharacters);
                }
                if (line.StartsWith("frame=", StringComparison.Ordinal) && long.TryParse(line.AsSpan(6), out long frame) && frame > Interlocked.Read(ref lastFrame))
                {
                    Interlocked.Exchange(ref lastFrame, frame);
                    Interlocked.Exchange(ref lastAdvance, Environment.TickCount64);
                }
            }
        });
        var error = process.StandardError.ReadToEndAsync();
        var exit = process.WaitForExitAsync();
        bool timedOut = false, stalled = false;
        while (!exit.IsCompleted)
        {
            await Task.WhenAny(exit, Task.Delay(250));
            if (exit.IsCompleted) break;
            if (cancellationToken.IsCancellationRequested)
            {
                await KillAsync(process, exit);
                cancellationToken.ThrowIfCancellationRequested();
            }
            long now = Environment.TickCount64;
            timedOut = now - started > (long)timeout.TotalMilliseconds;
            stalled = !timedOut && stallTimeout.HasValue && now - Interlocked.Read(ref lastAdvance) > (long)stallTimeout.Value.TotalMilliseconds;
            if (timedOut || stalled)
            {
                await KillAsync(process, exit);
                break;
            }
        }
        await output;
        string text;
        lock (progress) text = Tail(progress.ToString());
        return new FfmpegResult(timedOut || stalled ? null : process.ExitCode, timedOut, stalled, text, Tail(await error));
    }

    private static async Task KillAsync(Process process, Task exit)
    {
        try { process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        await exit;
    }

    private static string Tail(string text) => text.Length <= TailCharacters ? text : text.Substring(text.Length - TailCharacters);
}
