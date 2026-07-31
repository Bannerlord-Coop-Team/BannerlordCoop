using System;
using System.Diagnostics;
using System.Threading;

namespace Coop.CrashReporter
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            CrashReporterOptions options;
            if (!CrashReporterOptions.TryParse(args, out options))
                return 2;

            try
            {
                using (Process process = Process.GetProcessById(options.ProcessId))
                {
                    if (process.StartTime.ToUniversalTime().Ticks != options.ProcessStartUtcTicks)
                        return 3;

                    process.EnableRaisingEvents = true;
                    if (process.Handle == IntPtr.Zero)
                        return 1;

                    SignalReady();
                    return new CrashReportCollector(options).Run(process);
                }
            }
            catch
            {
                return 1;
            }
        }

        private static void SignalReady()
        {
            string eventName = Environment.GetEnvironmentVariable("COOP_CRASH_READY");
            if (string.IsNullOrEmpty(eventName))
                return;

            try
            {
                using (EventWaitHandle ready = EventWaitHandle.OpenExisting(eventName))
                    ready.Set();
            }
            catch
            {
            }
        }
    }
}
