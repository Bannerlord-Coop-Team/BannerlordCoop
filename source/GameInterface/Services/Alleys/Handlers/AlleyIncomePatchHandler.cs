using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Alleys.Patches;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.Alleys.Handlers;

/// <summary>
/// Applies <see cref="AlleyIncomePatch"/> once the campaign is actually loaded, instead of during the
/// early, pre-load <c>PatchAll</c>. The patch targets <c>DefaultClanFinanceModel</c>, whose
/// beforefieldinit static ctor eagerly calls <c>Game.Current.GameTextManager.FindText</c>; touching that
/// type before a campaign exists throws in the ctor and the CLR caches the failure, which kills clan
/// finance for the whole run (frozen clan screen, and the server's per-tick finance throw aborts the
/// campaign tick so AI parties stop moving).
///
/// We trigger on <see cref="CampaignReady"/>, not <c>GameLoaded</c>: on the host <c>GameLoaded</c> fires
/// before the campaign loads (its <c>GameTextManager</c> is still null), so applying the patch there
/// re-poisons the ctor. <c>CampaignReady</c> is the campaign-is-up signal on both server and client. The
/// <c>GameTextManager</c> null-check is a belt-and-suspenders guard so we never trigger the ctor early.
/// Applied once per process.
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

        messageBroker.Subscribe<CampaignReady>(Handle_CampaignReady);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CampaignReady>(Handle_CampaignReady);
    }

    private void Handle_CampaignReady(MessagePayload<CampaignReady> payload)
    {
        // Patches persist for the process (UnpatchAll is disabled), so applying once is enough; a later
        // load must not re-run PatchCategory or the postfix would be added twice and double the income.
        if (applied) return;

        // Only patch once the text manager exists; otherwise applying the patch would run
        // DefaultClanFinanceModel's static ctor while Game.Current.GameTextManager is null and poison it.
        if (Game.Current?.GameTextManager == null)
        {
            Logger.Error("CampaignReady fired but GameTextManager is null; skipping the alley income patch to avoid poisoning DefaultClanFinanceModel");
            return;
        }

        applied = true;

        try
        {
            harmony.PatchCategory(typeof(AlleyIncomePatch).Assembly, AlleyIncomePatch.DeferredCategory);
            Logger.Information("Applied deferred alley income patch ({Category}) on campaign ready", AlleyIncomePatch.DeferredCategory);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply deferred alley income patch");
        }
    }
}
