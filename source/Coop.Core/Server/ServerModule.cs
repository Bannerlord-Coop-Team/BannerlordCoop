using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client;
using Coop.Core.Client.Services.Heroes.Data;
using Coop.Core.Common;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.States;
using LiteNetLib;

namespace Coop.Core.Server;

/// <summary>
/// Server dependencies
/// </summary>
public class ServerModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterModule<ConnectionModule>();

        builder.RegisterType<ServerLogic>().As<IServerLogic>().As<ILogic>().InstancePerLifetimeScope();
        builder.RegisterType<CoopServer>().As<ICoopServer>().As<INetwork>().As<INetEventListener>().InstancePerLifetimeScope();
        builder.RegisterType<InitialServerState>().As<IServerState>();
        builder.RegisterType<CoopSaveManager>().As<ICoopSaveManager>().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<IPacketHandler>(builder, autoInstantiate: true);

        RegisterAllTypesWithInterface<IServerState>(builder);

        

        base.Load(builder);
    }

    private void RegisterAllTypesWithInterface<TInterface>(ContainerBuilder builder, bool autoInstantiate = false)
    {
        foreach (var handlerType in TypeCollector.Collect<ServerModule, TInterface>())
        {
            var handlerBuilder = builder.RegisterType(handlerType).AsSelf().InstancePerLifetimeScope();

            if (autoInstantiate)
            {
                handlerBuilder.AutoActivate();
            }
        }
    }
}