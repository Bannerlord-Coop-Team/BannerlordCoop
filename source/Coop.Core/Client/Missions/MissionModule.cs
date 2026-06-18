using Autofac;
using GameInterface.Missions;
using GameInterface.Missions.Agents.Handlers;
using GameInterface.Missions.Missiles;
using GameInterface.Missions.Missiles.Handlers;

namespace Coop.Core.Client.Missions
{
    internal class MissionModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            // Everything a mission composes is transient (InstancePerDependency): each mission resolves a
            // fresh CoopMissionController, which pulls a fresh ICoopMissionComponent and a fresh set of sync
            // handlers — so no agent/registry state from a previous mission leaks into the next.
            builder.RegisterType<CoopMissionComponent>().As<ICoopMissionComponent>().InstancePerDependency();

            builder.RegisterType<NetworkAgentRegistry>().As<INetworkAgentRegistry>().InstancePerDependency();
            builder.RegisterType<NetworkMissileRegistry>().As<INetworkMissileRegistry>().InstancePerDependency();
            builder.RegisterType<MissileHandler>().As<IMissileHandler>().InstancePerDependency();
            builder.RegisterType<WeaponDropHandler>().As<IWeaponDropHandler>().InstancePerDependency();
            builder.RegisterType<WeaponPickupHandler>().As<IWeaponPickupHandler>().InstancePerDependency();
            builder.RegisterType<ShieldDamageHandler>().As<IShieldDamageHandler>().InstancePerDependency();
            builder.RegisterType<AgentDamageHandler>().As<IAgentDamageHandler>().InstancePerDependency();
            builder.RegisterType<AgentDeathHandler>().As<IAgentDeathHandler>().InstancePerDependency();
        }
    }
}
