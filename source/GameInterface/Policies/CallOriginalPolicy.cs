using Autofac;
using Common;
using Common.Logging;
using Common.Util;
using Serilog;
using System.Linq;

namespace GameInterface.Policies;

public class CallOriginalPolicy
{
    private static readonly ILogger Logger = LogManager.GetLogger<CallOriginalPolicy>();

    public static bool IsOriginalAllowed()
    {
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

    /// <summary>
    /// True when the current call runs on the server inside another replicated action's
    /// <see cref="AllowedThread"/> scope. Such calls skip patch interception, but on the server
    /// they are still authoritative: lifetime patches use this to replicate vanilla side effects
    /// nested inside the outer action (e.g. a settlement ownership change culling its patrol)
    /// that would otherwise never reach clients and leave zombie objects behind.
    /// </summary>
    public static bool IsServerNestedCall() => ModInformation.IsServer && AllowedThread.IsThisThreadAllowed();
}
