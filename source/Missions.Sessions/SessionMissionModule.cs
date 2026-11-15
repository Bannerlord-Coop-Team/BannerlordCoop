using Autofac;
using GameInterface.Services.Sessions;

namespace Missions.Sessions;

/// <summary>
/// Composition root for campaign-free mission sessions. Registered next to <c>MissionModule</c> in the
/// server and client containers.
/// </summary>
public class SessionMissionModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // One controller per session mission, built by the mission lifetime factory like the battle controller.
        builder.RegisterType<CoopSessionController>()
            .Keyed<CoopSessionController>(MissionLifetimeFactory.MissionTag)
            .InstancePerMatchingLifetimeScope(MissionLifetimeFactory.MissionTag)
            .ExternallyOwned();
        builder.Register((context, parameters) => context.Resolve<IMissionLifetimeFactory>().Create<CoopSessionController>(parameters))
            .AsSelf().InstancePerDependency().ExternallyOwned();

        builder.RegisterType<SessionMissionHost>()
            .As<ISessionMissionHost>()
            .SingleInstance();
        builder.RegisterType<SessionModChannel>()
            .As<ISessionModChannel>()
            .SingleInstance();
    }
}
