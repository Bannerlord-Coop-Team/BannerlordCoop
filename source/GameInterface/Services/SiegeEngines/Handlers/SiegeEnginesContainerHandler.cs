using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines.Messages;
using GameInterface.Services.SiegeEngines.Patches;
using GameInterface.Utils;
using Serilog;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines.Handlers;

/// <summary>
/// Applies replicated <see cref="SiegeEnginesContainer"/> mutations on the client.
/// </summary>
internal class SiegeEnginesContainerHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEnginesContainerHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;

    public SiegeEnginesContainerHandler(IMessageBroker messageBroker, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        messageBroker.Subscribe<ChangeSiegeEngineDeployed>(HandleDeployed);
        messageBroker.Subscribe<ChangeSiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Subscribe<ChangeSiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Subscribe<ChangeSiegeEngineReserveRemoved>(HandleReserveRemoved);
    }

    private void HandleDeployed(MessagePayload<ChangeSiegeEngineDeployed> payload)
    {
        var obj = payload.What;
        // Resolve on the game thread so the lookups stay ordered behind deferred registrations.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEnginesContainer>(obj.ContainerId, out var container)) return;
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineConstructionProgress>(obj.SiegeEngineId, out var siegeEngine)) return;

            FillEngineType(siegeEngine, obj.EngineTypeId);
            FillRangedSiegeEngine(container, siegeEngine);
            SiegeEnginesContainerPatches.RunDeploySiegeEngineAtIndex(container, siegeEngine, obj.Index);
            DirtyOwnerVisual(container);
        });
    }

    // The shell's readonly SiegeEngine field cannot reference-sync (catalog types are XML objects the
    // co-op registry never holds), so the messages carry the type id and the apply fills it here —
    // before the container call, whose count refresh keys on the type.
    private static void FillEngineType(SiegeEngineConstructionProgress siegeEngine, string engineTypeId)
    {
        if (siegeEngine.SiegeEngine != null || string.IsNullOrEmpty(engineTypeId)) return;

        var engineType = MBObjectManager.Instance.GetObject<SiegeEngineType>(engineTypeId);
        if (engineType == null)
        {
            Logger.Error("Unknown siege engine type {EngineTypeId} in a container apply", engineTypeId);
            return;
        }

        ReflectionUtils.SetPrivateField(typeof(SiegeEngineConstructionProgress), nameof(SiegeEngineConstructionProgress.SiegeEngine), siegeEngine, engineType);
    }

    // Vanilla allocates the bombardment state in SiegeEvent.CreateSiegeObject when a ranged engine finishes
    // building, which only the server's siege logic runs. The settlement's map visual derefs it every frame
    // for each deployed ranged engine, so fill the client's engine here to match vanilla's fresh-deploy state.
    private static void FillRangedSiegeEngine(SiegeEnginesContainer container, SiegeEngineConstructionProgress siegeEngine)
    {
        if (siegeEngine.SiegeEngine?.IsRanged != true || siegeEngine.RangedSiegeEngine != null) return;

        var side = SiegeContainerLookup.FindOwnerSide(container);
        if (side == null) return;

        siegeEngine.SetRangedSiegeEngine(new RangedSiegeEngine(siegeEngine.SiegeEngine, side));
    }

    // The settlement's map visual only refreshes when dirtied, and vanilla dirties inside the
    // server-only paths, so each apply re-renders the engine meshes here.
    private static void DirtyOwnerVisual(SiegeEnginesContainer container)
    {
        SiegeContainerLookup.FindOwnerSettlement(container)?.Party?.SetVisualAsDirty();
    }

    private void HandleUndeployed(MessagePayload<ChangeSiegeEngineUndeployed> payload)
    {
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEnginesContainer>(obj.ContainerId, out var container)) return;

            SiegeEnginesContainerPatches.RunRemoveDeployedSiegeEngine(container, obj.Index, obj.IsRanged, obj.MoveToReserve);
            DirtyOwnerVisual(container);
        });
    }

    private void HandleReserveAdded(MessagePayload<ChangeSiegeEngineReserveAdded> payload)
    {
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEnginesContainer>(obj.ContainerId, out var container)) return;
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineConstructionProgress>(obj.SiegeEngineId, out var siegeEngine)) return;

            FillEngineType(siegeEngine, obj.EngineTypeId);
            SiegeEnginesContainerPatches.RunAddPrebuiltEngineToReserve(container, siegeEngine);
            DirtyOwnerVisual(container);
        });
    }

    private void HandleReserveRemoved(MessagePayload<ChangeSiegeEngineReserveRemoved> payload)
    {
        var obj = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEnginesContainer>(obj.ContainerId, out var container)) return;
            if (!objectManager.TryGetObjectWithLogging<SiegeEngineConstructionProgress>(obj.SiegeEngineId, out var siegeEngine)) return;

            SiegeEnginesContainerPatches.RunRemovedSiegeEngineFromReserve(container, siegeEngine);
            DirtyOwnerVisual(container);
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<ChangeSiegeEngineDeployed>(HandleDeployed);
        messageBroker.Unsubscribe<ChangeSiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Unsubscribe<ChangeSiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Unsubscribe<ChangeSiegeEngineReserveRemoved>(HandleReserveRemoved);
    }
}
