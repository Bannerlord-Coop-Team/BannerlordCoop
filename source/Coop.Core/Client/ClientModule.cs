using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client.Policies;
using Coop.Core.Client.States;
using Coop.Core.Common;
using GameInterface.Policies;
using LiteNetLib;

namespace Coop.Core.Client;

/// <summary>
/// Client state DI container
/// </summary>
public class ClientModule : CommonModule
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterType<ClientLogic>().As<ILogic>().As<IClientLogic>().InstancePerLifetimeScope();
        builder.RegisterType<CoopClient>().As<ICoopClient>().As<INetwork>().As<INetEventListener>().InstancePerLifetimeScope();

        // Policies
        builder.RegisterType<ClientSyncPolicy>().As<ISyncPolicy>().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<ClientModule, IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<ClientModule, IPacketHandler>(builder, autoInstantiate: true);

        RegisterAllTypesWithInterface<ClientModule, IClientState>(builder);
    }
}
