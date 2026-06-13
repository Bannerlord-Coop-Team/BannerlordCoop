using Common;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Coop.Tests;

/// <summary>
/// Dedicates a background thread as the game-loop thread for the whole test run and pumps it
/// continuously.
/// </summary>
/// <remarks>
/// Some production code paths (e.g. <c>TransferSaveState</c> packaging a save) call
/// <c>GameLoopRunner.RunOnMainThread(..., blocking: true)</c>, which blocks the caller until the
/// game-loop thread processes the queued action. No engine runs in unit tests, so without a pump
/// those calls time out after <c>GameLoopRunner.BlockingTimeout</c> (30s). Running the pump once at
/// assembly load lets those blocking calls complete from any (parallel) test thread.
/// </remarks>
internal static class TestGameLoopPump
{
    [ModuleInitializer]
    public static void Initialize()
    {
        var ready = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            GameLoopRunner.Instance.SetGameLoopThread();
            ready.Set();

            while (true)
            {
                GameLoopRunner.Instance.Update(TimeSpan.FromMilliseconds(16));
                Thread.Sleep(1);
            }
        })
        {
            IsBackground = true,
            Name = "TestGameLoopPump",
        };

        thread.Start();
        ready.Wait();
    }
}
