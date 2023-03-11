using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public class Poller
    {
        // The function to be polled
        private Action<TimeSpan> _pollingFunction;

        // The interval at which to poll the function (in milliseconds)
        private TimeSpan _pollingInterval;

        // A cancellation token source to stop the polling
        private CancellationTokenSource _cts;

        // A flag to indicate whether the poller is currently running
        private bool _isRunning;

        private Task _pollingTask;

        public Poller(Action<TimeSpan> pollingFunction, TimeSpan pollingInterval)
        {
            _pollingFunction = pollingFunction;
            _pollingInterval = pollingInterval;
        }

        public void Start()
        {
            if (_pollingTask != null)
            {
                Stop();
            }

            _pollingTask = Task.Factory.StartNew(StartAsync);
        }

        private async Task StartAsync()
        {
            // Set the running flag to true
            _isRunning = true;

            // Create a new cancellation token source
            _cts = new CancellationTokenSource();

            // Store the current time
            var startTime = DateTime.Now;

            // Run the polling loop asynchronously
            await Task.Factory.StartNew(async () =>
            {
                while (_isRunning)
                {
                    // Calculate the delta time span
                    var delta = DateTime.Now - startTime;

                    // Poll the function
                    _pollingFunction(delta);

                    // Wait for the specified interval before continuing the loop
                    await Task.Delay(_pollingInterval, _cts.Token);

                    startTime = DateTime.Now;
                }
            }, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Stop()
        {
            // Set the running flag to false
            _isRunning = false;

            // Cancel the cancellation token
            _cts.Cancel();
        }
    }
}
