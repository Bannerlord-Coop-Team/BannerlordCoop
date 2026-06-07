using Autofac;
using Common.Logging;
using Common.Util;
using Serilog;
using System.Linq;

namespace GameInterface.Policies;

public class CallOriginalPolicy
{
    public static bool DisablePatches = true;

    private static readonly ILogger Logger = LogManager.GetLogger<CallOriginalPolicy>();

    public static bool IsOriginalAllowed()
    {
        // Allow original when patches are disabled
        if (DisablePatches) return true;

        // While using allowed thread, allow original call
        if (AllowedThread.IsThisThreadAllowed()) return true;

        if (ContainerProvider.TryResolve<ISyncPolicy>(out var syncPolicy) == false)
        {
            Logger.Error("Unable to resolve {name}", nameof(ISyncPolicy));
            return true;
        }

        if (syncPolicy.AllowOriginal()) return true;

        return false;
    }
}
