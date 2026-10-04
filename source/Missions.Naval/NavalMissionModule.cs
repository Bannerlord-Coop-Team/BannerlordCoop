using Autofac;
using Common.Commands;
using GameInterface.Services.MapEvents;

namespace Missions.Naval;

/// <summary>
/// Registers the coop naval battle services. Loaded by <see cref="MissionModule"/> only while NavalDLC is active,
/// so nothing here is resolved by a process that cannot load the NavalDLC assemblies.
/// </summary>
public class NavalMissionModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<CoopNavalBattleLauncher>()
            .As<ICoopNavalBattleLauncher>()
            .InstancePerDependency();
        builder.RegisterType<CoopShipSnapshotBuilder>()
            .As<ICoopShipSnapshotBuilder>()
            .InstancePerDependency();
        builder.RegisterType<NavalInspectCoopCommand>()
            .As<ICoopCommand>()
            .InstancePerDependency();
    }
}
