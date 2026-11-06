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

        // The per-client naval mission state the patches read (the battle flag, copy set, rope records, ship snapshots, damage gate and deployment plans). One per client container rather than per lifetime scope, so a
        // mission child scope resolves the same instance the patches get from ContainerProvider; the behaviour resets it per battle.
        builder.RegisterType<NavalMissionTracker>()
            .As<INavalMissionTracker>()
            .SingleInstance();
        builder.RegisterType<NavalForeignHullRegistry>()
            .As<INavalForeignHullRegistry>()
            .SingleInstance();
        builder.RegisterType<NavalRopeRegistry>()
            .As<INavalRopeRegistry>()
            .SingleInstance();
        builder.RegisterType<CoopShipSnapshotRegistry>()
            .As<ICoopShipSnapshotRegistry>()
            .SingleInstance();
        builder.RegisterType<NavalShipDamageGate>()
            .As<INavalShipDamageGate>()
            .SingleInstance();
        builder.RegisterType<NavalPlayerDeploymentSlotTracker>()
            .As<INavalPlayerDeploymentSlotTracker>()
            .SingleInstance();
        builder.RegisterType<NavalLateDeploymentPlanTracker>()
            .As<INavalLateDeploymentPlanTracker>()
            .SingleInstance();

        builder.RegisterType<CoopNavalBattleLauncher>()
            .As<ICoopNavalBattleLauncher>()
            .InstancePerDependency();
        builder.RegisterType<NavalBattleServicesFactory>()
            .As<INavalBattleServicesFactory>()
            .InstancePerDependency();
        builder.RegisterType<CoopShipSnapshotBuilder>()
            .As<ICoopShipSnapshotBuilder>()
            .InstancePerDependency();
        builder.RegisterType<NavalShipEngine>()
            .As<INavalShipEngine>()
            .InstancePerDependency();
        builder.RegisterType<NavalShipCaptureEngine>()
            .As<INavalShipCaptureEngine>()
            .InstancePerDependency();
        builder.RegisterType<NavalCopyWeaponStates>()
            .As<INavalCopyWeaponStates>()
            .InstancePerDependency();
        builder.RegisterType<NavalTickFailureLog>()
            .As<INavalTickFailureLog>()
            .InstancePerDependency();
        builder.RegisterType<NavalAsyncAgentTick>()
            .As<IAsyncAgentTick>()
            .InstancePerDependency();
        builder.RegisterType<NavalLateHullDeployment>()
            .As<INavalLateHullDeployment>()
            .InstancePerDependency();
        builder.RegisterType<NavalBallistaTargets>()
            .As<INavalBallistaTargets>()
            .InstancePerDependency();
        builder.RegisterType<NavalSteeredCopies>()
            .As<INavalSteeredCopies>()
            .InstancePerDependency();
        // One set of knobs per client, so a copy_steer tuning holds across battles.
        builder.RegisterType<CopySteeringKnobs>()
            .AsSelf()
            .SingleInstance();
        // Registered after MissionModule's AgentFormationAssigner, so it is the one resolved.
        builder.RegisterType<NavalAgentFormationAssigner>()
            .As<IAgentFormationAssigner>()
            .InstancePerDependency();
        builder.RegisterAssemblyTypes(typeof(NavalMissionModule).Assembly)
            .Where(type => type.IsClass &&
                           !type.IsAbstract &&
                           typeof(ICoopCommand).IsAssignableFrom(type))
            .As<ICoopCommand>()
            .InstancePerDependency();
#if DEBUG
        builder.RegisterType<NavalRamInspect>()
            .As<INavalRamInspect>()
            .InstancePerDependency();
        builder.RegisterType<NavalEdgeRetreatStaging>()
            .As<INavalEdgeRetreatStaging>()
            .InstancePerDependency();
        builder.RegisterType<NavalRamStaging>()
            .As<INavalRamStaging>()
            .InstancePerDependency();
        builder.RegisterType<NavalLateHullProbe>()
            .As<INavalLateHullProbe>()
            .InstancePerDependency();
        builder.RegisterType<NavalWeaponProbe>()
            .As<INavalWeaponProbe>()
            .InstancePerDependency();
        builder.RegisterType<NavalRollProbe>()
            .As<INavalRollProbe>()
            .InstancePerDependency();
        builder.RegisterType<NavalDebugHulls>()
            .As<INavalDebugHulls>()
            .InstancePerDependency();
#endif
    }
}
