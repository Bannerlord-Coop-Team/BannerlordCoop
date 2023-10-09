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
public class ServerModule : CommonModule
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterModule<ConnectionModule>();

        builder.RegisterType<ServerLogic>().As<IServerLogic>().As<ILogic>().InstancePerLifetimeScope();
        builder.RegisterType<CoopServer>().As<ICoopServer>().As<INetwork>().As<INetEventListener>().InstancePerLifetimeScope();
        builder.RegisterType<CoopSaveManager>().As<ICoopSaveManager>().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<ServerModule, IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<ServerModule, IPacketHandler>(builder, autoInstantiate: true);

        RegisterAllTypesWithInterface<ServerModule, IServerState>(builder);
    }
}