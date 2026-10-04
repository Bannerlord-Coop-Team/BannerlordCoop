using System.Diagnostics;

namespace CoopMcpServer;

public sealed record FfmpegResult(int? ExitCode, bool TimedOut, string Progress, string ErrorTail);

public interface IFfmpegRunner
{
    Task<FfmpegResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken);
}

public sealed class FfmpegRunner : IFfmpegRunner
{
    private const int TailCharacters = 4096;

    public async Task<FfmpegResult> RunAsync(string executable, IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start);
        if (process == null) throw new IOException("ffmpeg did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        bool timedOut = false;
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            await process.WaitForExitAsync();
            cancellationToken.ThrowIfCancellationRequested();
            timedOut = true;
        }
        return new FfmpegResult(timedOut ? null : process.ExitCode, timedOut, Tail(await output), Tail(await error));
    }

    private static string Tail(string text) => text.Length <= TailCharacters ? text : text.Substring(text.Length - TailCharacters);
}
