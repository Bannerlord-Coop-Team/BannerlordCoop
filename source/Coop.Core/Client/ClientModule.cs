using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client.States;
using Coop.Core.Common;
using LiteNetLib;
using System.Linq;

namespace Coop.Core.Client;

/// <summary>
/// Client state DI container
/// </summary>
public class ClientModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ClientLogic>().As<ILogic>().As<IClientLogic>().SingleInstance();
        builder.RegisterType<CoopClient>().As<ICoopClient>().As<INetwork>().As<INetEventListener>().SingleInstance();

        RegisterAllTypesWithInterface<IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<IPacketHandler>(builder, autoInstantiate: true);

        RegisterAllTypesWithInterface<IClientState>(builder);

        base.Load(builder);
    }

    private void RegisterAllTypesWithInterface<TInterface>(ContainerBuilder builder, bool autoInstantiate = false)
    {
        foreach (var handlerType in TypeCollector.Collect<ClientModule, TInterface>())
        {
            var handlerBuilder = builder.RegisterType(handlerType).AsSelf().InstancePerLifetimeScope();

            if (autoInstantiate)
            {
                handlerBuilder.AutoActivate();
            }
        }
    }
}
