using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Common;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Policies;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.Services.Time;
using Coop.Core.Server.States;
using GameInterface.Policies;
using LiteNetLib;
using Missions;

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

        // The mission/P2P stack is composed into the server container too (it is also in ClientModule) so the
        // server-authoritative battle classes — notably BattleHostHandler, which elects the battle host — run
        // here. The client-only pieces (mesh client, location/battle controllers) stay lazy: nothing on the
        // server resolves IBattleNetwork, so no P2P socket is opened. See MissionModule for what activates.
        builder.RegisterModule<MissionModule>();

        builder.RegisterType<ServerContext>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<ServerLogic>().As<IServerLogic>().As<ILogic>().InstancePerLifetimeScope();
        builder.RegisterType<CoopServer>().As<ICoopServer>().As<INetwork>().As<INetEventListener>().InstancePerLifetimeScope();
        builder.RegisterType<CoopSaveManager>().As<ICoopSaveManager>().InstancePerLifetimeScope();

        // Withholds world broadcasts from a peer until it has the transfer save and has entered the
        // campaign. AutoActivate so it subscribes to connection lifecycle events before any peer joins.
        builder.RegisterType<ConnectionMessageQueue>().As<IConnectionMessageQueue>().InstancePerLifetimeScope().AutoActivate();

        builder.RegisterType<MissionManager>().As<IMissionManager>().InstancePerLifetimeScope();
        // Pauses time while a peer's packet queue is overloaded (slow client catching up). Constructed
        // as a CoopServer dependency, so it registers its unpause policy when the server is built.
        builder.RegisterType<OverloadedPeerManager>().As<IOverloadedPeerManager>().InstancePerLifetimeScope().AutoActivate();

        // Policies
        builder.RegisterType<ServerSyncPolicy>().As<ISyncPolicy>().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<ServerModule, IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<ServerModule, IPacketHandler>(builder, autoInstantiate: true);
    }
}