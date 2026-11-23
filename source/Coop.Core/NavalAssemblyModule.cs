using Autofac;
using Common;
#if COOP_NAVAL
using Coop.Naval;
using Missions.Naval;
#endif
using System.Runtime.CompilerServices;

namespace Coop.Core;

/// <summary>
/// Registers the modules of the optional naval assemblies while War Sails co-op runs, because they bind to NavalDLC types.
/// </summary>
public class NavalAssemblyModule : Module
{
    // Registers nothing unless startup turned War Sails co-op on.
    protected override void Load(ContainerBuilder builder)
    {
        if (!ModInformation.IsNavalEnabled) return;

        RegisterNavalModules(builder);
    }

    // Not inlined, so the runtime only loads the naval assemblies once the flag above is on.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterNavalModules(ContainerBuilder builder)
    {
#if COOP_NAVAL
        builder.RegisterModule<CoopNavalModule>();
        builder.RegisterModule<NavalMissionModule>();
#endif
    }
}
