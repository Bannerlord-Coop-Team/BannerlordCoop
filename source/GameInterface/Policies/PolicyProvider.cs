using Autofac;
using Common.Logging;
using Serilog;

namespace GameInterface.Policies;

// TODO make cleaner
internal class PolicyProvider
{
    private static readonly ILogger Logger = LogManager.GetLogger<PolicyProvider>();

    public static bool AllowOriginalCalls
    {
        get
        {
            // If container provider is not set, allow method
            if (ContainerProvider.TryGetContainer(out ILifetimeScope scope) == false)
            {
                Logger.Error("{providerName} was not set before patch calls", nameof(ContainerProvider));
                return true;
            }

            if (scope.Resolve<ISyncPolicy>().AllowOriginalCalls) return true;

            return false;
        }
    }
}
