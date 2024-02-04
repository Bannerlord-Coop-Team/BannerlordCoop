using Autofac;
using Common.Logging;
using Common.Util;
using Serilog;

namespace GameInterface.Policies;

// TODO make cleaner
internal class CallOriginalPolicy
{
    private static readonly ILogger Logger = LogManager.GetLogger<CallOriginalPolicy>();

    public static bool IsOriginalAllowed()
    {
        // While using allowed thread, allow original call
        if (AllowedThread.IsThisThreadAllowed()) return true;

        // If container provider is not set, allow method
        if (ContainerProvider.TryResolve<ISyncPolicy>(out var syncPolicy) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(ISyncPolicy));
            return true;
        }

        if (syncPolicy.AllowOriginal()) return true;

        return false;
    }
}
