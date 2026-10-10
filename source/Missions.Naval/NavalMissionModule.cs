using Autofac;
using Common.Commands;
using GameInterface;
using GameInterface.Services.MapEvents;
using Missions.Battles;

namespace Missions.Naval;

/// <summary>
/// Registers the coop naval battle services. Loaded by <see cref="MissionModule"/> only while NavalDLC is active,
/// so nothing here is resolved by a process that cannot load the NavalDLC assemblies.
/// </summary>
public class NavalMissionModule : Module
{
    internal const string PatchCategory = "CoopNavalShipPatches";

    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterInstance(new HarmonyPatchCategoryRegistration(typeof(NavalMissionModule).Assembly, PatchCategory));

        builder.RegisterType<CoopNavalBattleLauncher>()
            .As<ICoopNavalBattleLauncher>()
            .InstancePerDependency();
        builder.RegisterType<CoopShipSnapshotBuilder>()
            .As<ICoopShipSnapshotBuilder>()
            .InstancePerDependency();
        builder.RegisterType<NavalShipEngine>()
            .As<INavalShipEngine>()
            .InstancePerDependency();
        builder.RegisterType<NavalInspectCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<NavalRopeStatusCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#if DEBUG
        builder.RegisterType<NavalDebugHulls>()
            .As<INavalDebugHulls>()
            .InstancePerDependency();
        builder.RegisterType<NavalHelmCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<NavalReswapCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<NavalDamageCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<NavalSinkCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<NavalWinCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<NavalLoseCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<NavalRopeDebugCommands.RopeListCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
        builder.RegisterType<NavalRopeDebugCommands.RopeThrowCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
#endif
    }
}
