using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Alleys.Patches;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using Serilog;
using System;

namespace GameInterface.Services.Alleys.Handlers;

/// <summary>
/// Applies <see cref="AlleyIncomePatch"/> after the game has loaded, instead of during the early,
/// pre-load <c>PatchAll</c>. The patch targets <c>DefaultClanFinanceModel</c>, whose beforefieldinit
/// static ctor eagerly calls <c>Game.Current.GameTextManager.FindText</c>; touching that type before a
/// campaign exists throws in the ctor and the CLR caches the failure, which kills clan finance for the
/// whole run (frozen clan screen, and the server's per-tick finance throw aborts the campaign tick so
/// AI parties stop moving). Deferring to <see cref="GameLoaded"/> guarantees the text manager exists
/// when the patch (and thus that static ctor) first runs. Applied once per process, on both sides.
/// </summary>
internal class AlleyIncomePatchHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<AlleyIncomePatchHandler>();

    private static bool applied;

    private readonly IMessageBroker messageBroker;
    private readonly Harmony harmony;

    public AlleyIncomePatchHandler(IMessageBroker messageBroker, Harmony harmony)
    {
        this.messageBroker = messageBroker;
        this.harmony = harmony;

        messageBroker.Subscribe<GameLoaded>(Handle_GameLoaded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<GameLoaded>(Handle_GameLoaded);
    }

    private void Handle_GameLoaded(MessagePayload<GameLoaded> payload)
    {
        // Patches persist for the process (UnpatchAll is disabled), so applying once is enough; a later
        // load must not re-run PatchCategory or the postfix would be added twice and double the income.
        if (applied) return;
        applied = true;

        try
        {
            harmony.PatchCategory(typeof(AlleyIncomePatch).Assembly, AlleyIncomePatch.DeferredCategory);
            Logger.Information("Applied deferred alley income patch ({Category}) after game load", AlleyIncomePatch.DeferredCategory);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply deferred alley income patch");
        }
    }
}
