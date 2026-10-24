using Common.Logging;
using Serilog.Events;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// Collects the errors a naval scenario logs on its test thread, such as a throw that <c>GameThread.RunSafe</c>, the arrivals
/// queue or the hull authority caught, so the harness fails the scenario on them instead of passing over a log line.
/// </summary>
public sealed class NavalLoggedFailures : IDisposable
{
    private readonly int threadId = System.Environment.CurrentManagedThreadId;
    private readonly List<string> failures = new();
    private readonly List<string> expected = new();

    // Registers the collector for errors logged on the creating thread from now on.
    public NavalLoggedFailures()
    {
        OutputSinkManager.AddLogCallback(Record, LogEventLevel.Error);
    }

    /// <summary>Whether <see cref="AssertNone"/> already failed the scenario.</summary>
    public bool HasFailed { get; private set; }

    /// <summary>Lets the logged errors containing <paramref name="fragment"/> pass, for a scenario that provokes them.</summary>
    public void Expect(string fragment) => expected.Add(fragment);

    /// <summary>Fails with every unexpected error logged since the last call.</summary>
    public void AssertNone()
    {
        if (failures.Count == 0) return;

        var logged = failures.ToList();
        failures.Clear();
        HasFailed = true;
        Assert.Fail("The naval code logged a failure it caught:\n" + string.Join("\n", logged));
    }

    // Background threads (another environment's timers) log here too, and only this scenario's thread is its own.
    private void Record(string message)
    {
        if (System.Environment.CurrentManagedThreadId != threadId) return;
        if (expected.Any(fragment => message.Contains(fragment))) return;

        failures.Add(message);
    }

    // Stops collecting, as the scenario ends.
    public void Dispose() => OutputSinkManager.RemoveLogCallback(Record);
}
