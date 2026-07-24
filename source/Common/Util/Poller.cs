using Common.Logging;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Util;

/// <summary>
/// Poller used to run a periodic task in the background
/// </summary>
public class Poller
{
    private static readonly ILogger Logger = LogManager.GetLogger<Poller>();

    /// <summary>
    /// The function to be polled
    /// </summary>
    private Action<TimeSpan> pollingFunction;

    /// <summary>
    /// The interval at which to poll the function (in milliseconds)
    /// </summary>
    private TimeSpan pollingInterval;

    /// <summary>
    /// A cancellation token source to stop the polling
    /// </summary>
    private CancellationTokenSource cts;

    private Task pollingTask;

    [ThreadStatic]
    private static Poller activePoller;

    /// <summary>
    /// Returns true if task is running otherwise false
    /// </summary>
    public bool IsRunning => !cts?.IsCancellationRequested ?? false;
    public bool IsPollingThread => ReferenceEquals(activePoller, this);

    /// <summary>
    /// Creates a new poller object to run a periodic task in the background
    /// </summary>
    /// <param name="pollingFunction">
    /// Function to run in the background, 
    /// delta time as a <see cref="TimeSpan"/> type is passed to this function every time it is run
    /// </param>
    /// <param name="pollingInterval">
    /// Interval to run the polling task, the task will run this interval.
    /// Note that the task will run approximately this interval depending on current software load and some other factors.
    /// </param>
    public Poller(Action<TimeSpan> pollingFunction, TimeSpan pollingInterval)
    {
        this.pollingFunction = pollingFunction;
        this.pollingInterval = pollingInterval;
    }

    /// <summary>
    /// Starts the polling task in the background
    /// </summary>
    public void Start()
    {
        if (pollingTask != null)
        {
            Stop();
        }

        cts = new CancellationTokenSource();
        pollingTask = Task.Run(PollAsync);
    }

    private async Task PollAsync()
    {
        // Setup initial start time
        var startTime = DateTime.Now;

        // Track the last error so a fault that recurs every tick doesn't flood the log.
        string lastError = null;
        long repeatCount = 0;

        while (cts.IsCancellationRequested == false)
        {
            // Calculate the delta time span
            var delta = DateTime.Now - startTime;

            // Poll the function. Never let an exception escape: this loop runs on a
            // fire-and-forget task, so an unhandled exception would silently kill the
            // pump with no trace. Log it instead and keep polling.
            try
            {
                activePoller = this;
                pollingFunction(delta);
            }
            catch (Exception ex)
            {
                if (ex.Message != lastError)
                {
                    lastError = ex.Message;
                    repeatCount = 0;
                    Logger.Error(ex, "Polling function threw an exception; the poll loop will continue");
                }
                else if (++repeatCount % 1000 == 0)
                {
                    Logger.Error("Polling function still throwing the same exception ({RepeatCount}x): {Message}", repeatCount, ex.Message);
                }
            }
            finally
            {
                activePoller = null;
            }

            startTime = DateTime.Now;

            // Wait for the specified interval to elapse before continuing the loop
            try
            {
                await Task.Delay(pollingInterval, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Stop() was called while waiting; exit cleanly.
                break;
            }
        }
    }

    /// <summary>
    /// Stops the background polling task (cancellation only — does NOT wait for an in-flight tick to finish).
    /// Use <see cref="StopAndWait"/> when a running tick would race a teardown of the state it reads.
    /// </summary>
    public void Stop()
    {
        // Cancel the cancellation token
        cts?.Cancel();
    }

    /// <summary>
    /// Stops the poller and blocks (up to <paramref name="timeout"/>) until any in-flight tick has finished, so
    /// the caller can safely tear down state the polling function reads. Cancelling alone (<see cref="Stop"/>)
    /// only prevents FUTURE ticks — a tick already running continues concurrently and would race the teardown.
    /// </summary>
    /// <param name="timeout">Upper bound on the wait, so a stuck polling function can never hang the caller.</param>
    /// <returns>
    /// True if the loop stopped within the timeout; false when called by its active callback or when
    /// the wait elapsed before the loop stopped.
    /// </returns>
    public bool StopAndWait(TimeSpan timeout)
    {
        cts?.Cancel();

        if (IsPollingThread)
        {
            return false;
        }

        var task = pollingTask;
        if (task == null)
        {
            return true;
        }

        try
        {
            return task.Wait(timeout);
        }
        catch (AggregateException)
        {
            // The loop faulted; it is no longer running, which is all the caller needs.
            return true;
        }
    }
}
