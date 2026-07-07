using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines;
using GameInterface.Services.SiegeEnginesConstructionProgress.Messages;
using GameInterface.Services.SiegeEnginesConstructionProgress.Patches;
using Serilog;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesConstructionProgress.Handlers;

/// <summary>
/// Applies replicated siege engine construction progress values on the client.
/// </summary>
internal class SiegeEngineProgressHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEngineProgressHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SiegeEngineProgressHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<ChangeSiegeEngineProgress>(HandleProgress);
    }

    private void HandleProgress(MessagePayload<ChangeSiegeEngineProgress> payload)
    {
        var obj = payload.What;
        // Resolve on the game thread so the lookup stays ordered behind deferred registrations.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineConstructionProgress>(obj.SiegeEngineId, out var siegeEngine)) return;

            SiegeEngineProgressPatches.RunSetProgress(siegeEngine, obj.IsRedeployment, obj.Value);
            // Engine icons on the campaign map key on IsActive (progress-derived); refresh the owner.
            SiegeContainerLookup.FindOwnerSettlement(siegeEngine)?.Party?.SetVisualAsDirty();
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeSiegeEngineProgress>(HandleProgress);
    }
}
