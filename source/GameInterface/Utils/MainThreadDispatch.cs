using Common;
using Common.Logging;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Utils;

/// <summary>
/// Marshals network-handler work onto the game-loop (main) thread.
/// </summary>
/// <remarks>
/// Network messages are dispatched synchronously on the LiteNetLib poller background thread, but most
/// TaleWorlds game and UI state (screens, menus, encounters, map events) may only be touched from the
/// main thread. This helper standardises the deferral so handlers do not each hand-roll it:
/// <list type="bullet">
/// <item>it defers to the game-loop thread via <see cref="GameLoopRunner.RunOnMainThread(Action, bool)"/>;</item>
/// <item>it runs the action only if a campaign is loaded, re-checked on the main thread because the world
/// can change between a message arriving and the deferred action running;</item>
/// <item>it logs and swallows exceptions so a deferred action cannot crash the game tick
/// (<see cref="GameLoopRunner.Update(TimeSpan)"/> invokes queued actions unguarded).</item>
/// </list>
/// It deliberately does not wrap the action in <c>AllowedThread</c>: the receive-path client handlers add
/// that themselves where they need it, while server-authoritative work must run with patches live.
/// </remarks>
internal static class MainThreadDispatch
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(MainThreadDispatch));

    /// <summary>
    /// Runs <paramref name="action"/> on the game-loop thread once a campaign is loaded.
    /// </summary>
    /// <param name="context">Short human-readable description of the work, used in log messages.</param>
    /// <param name="action">The game/UI work to run on the main thread.</param>
    public static void RunWhenCampaignReady(string context, Action action)
    {
        GameLoopRunner.RunOnMainThread(() =>
        {
            try
            {
                if (Campaign.Current == null)
                {
                    Logger.Warning("Skipping deferred {Context}: no campaign is loaded", context);
                    return;
                }

                action();
            }
            catch (Exception e)
            {
                Logger.Error(e, "Deferred {Context} threw on the main thread", context);
            }
        });
    }
}
