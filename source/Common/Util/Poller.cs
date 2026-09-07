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
    /// Guards the loop ownership below, so Start, Stop and StopAndWait never interleave
    /// </summary>
    private readonly object lifecycleLock = new object();

    /// <summary>
    /// The loop this poller owns right now, or null while nothing is polling
    /// </summary>
    private PollLoop currentLoop;

    [ThreadStatic]
    private static Poller activePoller;

    /// <summary>
    /// Returns true if task is running otherwise false
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (lifecycleLock)
            {
                return currentLoop != null && !currentLoop.Token.IsCancellationRequested;
            }
        }
    }

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
    /// Starts the polling task in the background. On an already started poller this cancels the running loop and
    /// queues the replacement behind it, because both loops would otherwise call the polling function at the same time.
    /// </summary>
    public void Start()
    {
        lock (lifecycleLock)
        {
            // Read the task before cancelling: cancellation can finish the loop right here, which clears currentLoop.
            var previous = currentLoop?.Task;
            currentLoop?.Cancellation.Cancel();

            var loop = new PollLoop(new CancellationTokenSource());
            currentLoop = loop;
            loop.Task = Task.Run(() => RunLoopAsync(loop, previous));
        }
    }

    private async Task RunLoopAsync(PollLoop loop, Task previous)
    {
        if (previous != null)
        {
            try
            {
                // Let the loop this one replaces finish first, otherwise two loops poll the same state side by side.
                await previous;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Replaced polling loop ended with an exception");
            }
        }

        try
        {
            await PollAsync(loop);
        }
        finally
        {
            lock (lifecycleLock)
            {
                if (ReferenceEquals(currentLoop, loop))
                {
                    currentLoop = null;
                }
            }

            // Nothing can reach this source once the loop above has given up ownership, so its handles can go.
            loop.Cancellation.Dispose();
        }
    }

    private async Task PollAsync(PollLoop loop)
    {
        // Setup initial start time
        var startTime = DateTime.Now;

        // Track the last error so a fault that recurs every tick doesn't flood the log.
        string lastError = null;
        long repeatCount = 0;

        while (loop.Token.IsCancellationRequested == false)
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
                await Task.Delay(pollingInterval, loop.Token);
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
        CancelCurrentLoop();
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
        var task = CancelCurrentLoop();

        if (IsPollingThread)
        {
            return false;
        }

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

    /// <summary>
    /// Cancels the loop this poller owns and hands its task back, so the caller can wait outside the lifecycle lock.
    /// </summary>
    private Task CancelCurrentLoop()
    {
        lock (lifecycleLock)
        {
            var loop = currentLoop;
            if (loop == null)
            {
                return null;
            }

            // Read the task before cancelling: cancellation can finish the loop right here, which clears currentLoop.
            var task = loop.Task;
            loop.Cancellation.Cancel();
            return task;
        }
    }

    /// <summary>
    /// One polling loop and the cancellation it owns. A loop only ever reads its own token, so a restart cannot
    /// hand the previous loop the new loop's cancellation and leave it running.
    /// </summary>
    private sealed class PollLoop
    {
        public PollLoop(CancellationTokenSource cancellation)
        {
            Cancellation = cancellation;
            Token = cancellation.Token;
        }

        public CancellationTokenSource Cancellation { get; }

        public CancellationToken Token { get; }

        public Task Task { get; set; }
    }
}
