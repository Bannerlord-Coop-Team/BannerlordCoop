using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEnginesConstructionProgress.Messages;
using GameInterface.Services.SiegeEnginesConstructionProgress.Patches;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesConstructionProgress.Handlers;

/// <summary>
/// Applies replicated siege engine construction progress values on the client.
/// </summary>
internal class SiegeEngineProgressHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SiegeEngineProgressHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<ChangeSiegeEngineProgress>(HandleProgress);
        messageBroker.Subscribe<ChangeSiegeEngineHitpoints>(HandleHitpoints);
    }

    private void HandleProgress(MessagePayload<ChangeSiegeEngineProgress> payload)
    {
        var obj = payload.What;
        // Resolve on the game thread so the lookup stays ordered behind deferred registrations.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineConstructionProgress>(obj.SiegeEngineId, out var siegeEngine)) return;

            // RunSetProgress dirties the settlement visual itself, but only on the completion tick that flips
            // IsActive. Dirtying on every 1% tick here forces SettlementVisualManager to destroy and re-create
            // every siege engine entity at its rest orientation each message, which snaps aiming engines back
            // and reads as rotation jitter on the client.
            SiegeEngineProgressPatches.RunSetProgress(siegeEngine, obj.IsRedeployment, obj.Value);
        });
    }

    private void HandleHitpoints(MessagePayload<ChangeSiegeEngineHitpoints> payload)
    {
        var obj = payload.What;
        // Resolve on the game thread so the lookup stays ordered behind deferred registrations.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineConstructionProgress>(obj.SiegeEngineId, out var siegeEngine)) return;

            SiegeEngineProgressPatches.RunSetHitpoints(siegeEngine, obj.Hitpoints, obj.MaxHitPoints);
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeSiegeEngineProgress>(HandleProgress);
        messageBroker.Unsubscribe<ChangeSiegeEngineHitpoints>(HandleHitpoints);
    }
}
