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
        builder.RegisterType<ClientLogic>().As<ILogic>().As<IClientLogic>().InstancePerLifetimeScope();
        builder.RegisterType<CoopClient>().As<ICoopClient>().As<INetwork>().As<INetEventListener>().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<IHandler>(builder);
        RegisterAllTypesWithInterface<IPacketHandler>(builder);

        RegisterAllTypesWithInterface<IClientState>(builder);

        base.Load(builder);
    }

    private void RegisterAllTypesWithInterface<TInterface>(ContainerBuilder builder)
    {
        foreach (var handlerType in TypeCollector.Collect<ClientModule, TInterface>())
        {
            builder.RegisterType(handlerType).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }
    }
}
