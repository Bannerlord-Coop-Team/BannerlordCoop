using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Util;

public class Poller
{
    // The function to be polled
    private Action<TimeSpan> pollingFunction;

    // The interval at which to poll the function (in milliseconds)
    private TimeSpan pollingInterval;

    // A cancellation token source to stop the polling
    private CancellationTokenSource cts;

    private Task pollingTask;

    public Poller(Action<TimeSpan> pollingFunction, TimeSpan pollingInterval)
    {
        this.pollingFunction = pollingFunction;
        this.pollingInterval = pollingInterval;
    }

    public void Start()
    {
        if (pollingTask != null)
        {
            Stop();
        }

        cts = new CancellationTokenSource();
        pollingTask = Task.Factory.StartNew(StartAsync);
    }

    private async Task StartAsync()
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

    public void Stop()
    {
        // Cancel the cancellation token
        cts?.Cancel();
    }
}
