using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Registry.Auto;
using GameInterface.Services.ObjectManager;
using Serilog;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEngines;

internal static class SiegeEngineRegistration
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(SiegeEngineRegistration));

    public static bool EnsureRegistered(SiegeEngineConstructionProgress siegeEngine, string context)
    {
        if (siegeEngine == null || ModInformation.IsClient) return false;

        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            Logger.Error("Unable to resolve object manager while registering siege engine for {Context}", context);
            return false;
        }

        if (objectManager.TryGetId(siegeEngine, out _)) return true;

        Logger.Warning(
            "Registering untracked siege engine before {Context}. EngineType={EngineType}, Progress={Progress}, RedeploymentProgress={RedeploymentProgress}",
            context,
            siegeEngine.SiegeEngine?.StringId,
            siegeEngine.Progress,
            siegeEngine.RedeploymentProgress);

        MessageBroker.Instance.Publish(siegeEngine, new InstanceCreated<SiegeEngineConstructionProgress>(siegeEngine));

        return objectManager.TryGetIdWithLogging(siegeEngine, out _);
    }
}
