using Autofac;

namespace Coop.Core.Client.Services.Discord;

public sealed class DiscordPresenceModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // The presence adapter owns connection disposal on its serialized worker queue.
        builder.RegisterType<DiscordRpcConnection>().As<IDiscordRpcConnection>().InstancePerDependency().ExternallyOwned();
        builder.RegisterType<DiscordPresenceClient>().As<IDiscordPresenceClient>().InstancePerLifetimeScope();
    }
}
