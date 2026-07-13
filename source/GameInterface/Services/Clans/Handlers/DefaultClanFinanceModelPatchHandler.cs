using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Clans.Patches;
using GameInterface.Services.GameState.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.Core;

namespace GameInterface.Services.Clans.Handlers;

/// <summary>
/// Applies the clan-finance patches after the campaign creates its text manager. Patching
/// <see cref="DefaultClanFinanceModel"/> earlier runs its static constructor, which accesses the
/// text manager and permanently poisons the type when no campaign is active yet.
/// </summary>
internal class DefaultClanFinanceModelPatchHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<DefaultClanFinanceModelPatchHandler>();

    private static bool applied;

    private readonly IMessageBroker messageBroker;
    private readonly Harmony harmony;

    public DefaultClanFinanceModelPatchHandler(IMessageBroker messageBroker, Harmony harmony)
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
        if (applied) return;

        if (Game.Current?.GameTextManager == null)
        {
            Logger.Error("CampaignReady fired but GameTextManager is null; skipping deferred clan-finance patches");
            return;
        }

        try
        {
            harmony.PatchCategory(
                typeof(DefaultClanFinanceModelPatches).Assembly,
                DefaultClanFinanceModelPatches.DeferredCategory);
            applied = true;
            Logger.Information("Applied deferred clan-finance patches ({Category}) on campaign ready", DefaultClanFinanceModelPatches.DeferredCategory);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to apply deferred clan-finance patches");
        }
    }
}
