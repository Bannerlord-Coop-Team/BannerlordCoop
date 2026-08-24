#if DEBUG
using Common;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Serialization
{
    internal static class SerializationDebugCommands
    {
        [CommandLineArgumentFunction("package_cache_state", "coop.debug.serialization")]
        public static string PackageCacheState(List<string> args)
        {
            if (args.Count != 0)
            {
                return "Usage: coop.debug.serialization.package_cache_state";
            }

            if (ContainerProvider.TryResolve<IBinaryPackageFactory>(out var packageFactory) == false)
            {
                return "Unable to resolve IBinaryPackageFactory";
            }

            return $"LIVE_TEST_JSON={{\"packageCount\":{packageFactory.CachedPackageCount}}}";
        }
    }
}
#endif
