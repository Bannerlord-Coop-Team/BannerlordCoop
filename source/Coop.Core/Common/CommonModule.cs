using Autofac;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client.Services.Heroes.Data;
using Coop.Core.Common.Configuration;
using Coop.Core.Server;
using GameInterface;

namespace Coop.Core.Common;

/// <summary>
/// Module dependencies shared between the client and server
/// </summary>
public abstract class CommonModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<StateFactory>().As<IStateFactory>().InstancePerLifetimeScope();

        #region Network
        builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().InstancePerLifetimeScope();
        #endregion

        #region Communication
        builder.RegisterType<EventPacketHandler>().AsSelf().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterInstance(MessageBroker.Instance).As<IMessageBroker>().SingleInstance().ExternallyOwned();
        #endregion

        builder.RegisterType<DeferredHeroRepository>().As<IDeferredHeroRepository>().InstancePerLifetimeScope();

        #region GameInterface
        builder.RegisterModule<GameInterfaceModule>();
        #endregion

        builder.RegisterType<CoopFinalizer>().As<ICoopFinalizer>().InstancePerLifetimeScope();

        base.Load(builder);
    }

    /// <summary>
    /// Registers all types that inherit <typeparamref name="TInterface"/> in the same namespace as <typeparamref name="TModule"/>
    /// </summary>
    /// <typeparam name="TModule">Module or class to grab namespace from and use as a prefix for finding types</typeparam>
    /// <typeparam name="TInterface">Type to collect if inherited from</typeparam>
    /// <param name="builder"><see cref="ContainerBuilder"/> for registering types</param>
    /// <param name="autoInstantiate">Flag to auto instantiate type when container is built</param>
    protected void RegisterAllTypesWithInterface<TModule, TInterface>(ContainerBuilder builder, bool autoInstantiate = false)
    {
        foreach (var handlerType in TypeCollector.Collect<TModule, TInterface>())
        {
            var handlerBuilder = builder.RegisterType(handlerType).AsSelf().InstancePerLifetimeScope();

            if (autoInstantiate)
            {
                handlerBuilder.AutoActivate();
            }
        }
    }
}
