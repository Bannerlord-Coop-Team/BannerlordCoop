using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Common;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Policies;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.States;
using GameInterface.Policies;
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

        builder.RegisterType<ServerContext>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<ServerLogic>().As<IServerLogic>().As<ILogic>().InstancePerLifetimeScope();
        builder.RegisterType<CoopServer>().As<ICoopServer>().As<INetwork>().As<INetEventListener>().InstancePerLifetimeScope();
        builder.RegisterType<CoopSaveManager>().As<ICoopSaveManager>().InstancePerLifetimeScope();

        // Withholds world broadcasts from a peer until it has the transfer save and has entered the
        // campaign. AutoActivate so it subscribes to connection lifecycle events before any peer joins.
        builder.RegisterType<ConnectionMessageQueue>().As<IConnectionMessageQueue>().InstancePerLifetimeScope().AutoActivate();

        // Pauses time while a peer's packet queue is overloaded (slow client catching up). Constructed
        // as a CoopServer dependency, so it registers its unpause policy when the server is built.
        builder.RegisterType<Coop.Core.Server.Services.Time.OverloadedPeerManager>().As<Coop.Core.Server.Services.Time.IOverloadedPeerManager>().InstancePerLifetimeScope();

        // Policies
        builder.RegisterType<ServerSyncPolicy>().As<ISyncPolicy>().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<ServerModule, IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<ServerModule, IPacketHandler>(builder, autoInstantiate: true);

        RegisterAllTypesWithInterface<ServerModule, IServerState>(builder);
    }
}