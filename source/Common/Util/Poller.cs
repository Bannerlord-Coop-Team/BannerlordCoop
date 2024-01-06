using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Util;

/// <summary>
/// Poller used to run a periodic task in the background
/// </summary>
public class Poller
{
    /// <summary>
    /// The function to be polled
    /// </summary>
    // 
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

    /// <summary>
    /// Returns true if task is running otherwise false
    /// </summary>
    public bool IsRunning => !cts?.IsCancellationRequested ?? false;

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
        pollingTask = Task.Factory.StartNew(PollAsync);
    }

    private async Task PollAsync()
    {
        // Setup initial start time
        var startTime = DateTime.Now;

        while (cts.IsCancellationRequested == false)
        {
            // Calculate the delta time span
            var delta = DateTime.Now - startTime;

            // Poll the function
            pollingFunction(delta);

            startTime = DateTime.Now;

            // Wait for the specified interval to elapse before continuing the loop
            await Task.Delay(pollingInterval, cts.Token);
        }
    }

    /// <summary>
    /// Stops the background polling task
    /// </summary>
    public void Stop()
    {
        // Cancel the cancellation token
        cts?.Cancel();
    }
}
