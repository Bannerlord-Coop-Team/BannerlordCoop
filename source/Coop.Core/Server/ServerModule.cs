using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Session;
using Common.PacketHandlers;
using Coop.Core.Common;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Session;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Policies;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.MobileParties;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.Services.Session;
using Coop.Core.Server.Services.Time;
using Coop.Core.Server.States;
using Coop.Steam;
using GameInterface.Policies;
using LiteNetLib;
using Missions;
using System.Runtime.CompilerServices;

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
        builder.RegisterType<SendCoalescer>().As<ISendCoalescer>().InstancePerLifetimeScope();
        builder.RegisterType<CoopSaveManager>().As<ICoopSaveManager>().InstancePerLifetimeScope();
        builder.RegisterType<JoinMobilePartyPositionSnapshotSender>()
            .As<IJoinMobilePartyPositionSnapshotSender>()
            .InstancePerDependency();

        // Withholds world broadcasts from a peer until it has the transfer save and has entered the
        // campaign. AutoActivate so it subscribes to connection lifecycle events before any peer joins.
        builder.RegisterType<ConnectionMessageQueue>().As<IConnectionMessageQueue>().InstancePerLifetimeScope().AutoActivate();

        builder.RegisterType<MissionManager>().As<IMissionManager>().InstancePerLifetimeScope();
        // Pauses time while a peer's packet queue is overloaded (slow client catching up). Constructed
        // as a CoopServer dependency, so it registers its unpause policy when the server is built.
        builder.RegisterType<OverloadedPeerManager>().As<IOverloadedPeerManager>().InstancePerLifetimeScope().AutoActivate();

        // Policies
        builder.RegisterType<ServerSyncPolicy>().As<ISyncPolicy>().InstancePerLifetimeScope();

        // The standalone server logs into Steam itself and advertises with the host-selected lobby
        // visibility, so eligible players can join without port forwarding while the owner never
        // plays. Same guard as ClientModule: only touch Steam types when the boot probe found Steam.
        if (SessionDiscovery.SteamAvailable)
        {
            RegisterSteamSessionServices(builder);
        }
        else
        {
            builder.RegisterType<NoopSessionAdvertiser>().As<ISessionAdvertiser>().InstancePerLifetimeScope();
            builder.RegisterType<NoopSessionTunnelHost>()
                .As<ISessionTunnelHost>()
                .As<ISessionTunnelIdentityResolver>()
                .InstancePerLifetimeScope();
        }

        builder.RegisterType<SessionAdvertisementConfig>().AsSelf().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<ServerModule, IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<ServerModule, IPacketHandler>(builder, autoInstantiate: true);
    }

    // Non-inlined so referencing the Steam tunnel transport (its layout embeds Steamworks value
    // types) never pulls Steamworks.NET into Load's JIT on a non-Steam install.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterSteamSessionServices(ContainerBuilder builder)
    {
        builder.RegisterType<SteamLobbyApi>()
            .As<ISteamLobbyApi>()
            .As<ISteamPublicLobbyApi>()
            .InstancePerLifetimeScope();
        builder.Register(context => new SteamPublicLobbyAdvertiser(
                context.Resolve<ISteamPublicLobbyApi>(),
                context.Resolve<SessionAdvertisementConfig>().Visibility))
            .As<ISessionAdvertiser>()
            .InstancePerLifetimeScope();
        builder.RegisterType<SteamGameServerNetworkingTunnelTransport>().As<ISteamTunnelTransport>().InstancePerLifetimeScope();
        builder.RegisterType<SteamTunnelHost>()
            .As<ISessionTunnelHost>()
            .As<ISessionTunnelIdentityResolver>()
            .InstancePerLifetimeScope();
        builder.RegisterType<ServerSessionJoinInfoSource>().As<ISessionJoinInfoSource>().InstancePerLifetimeScope();
        builder.RegisterType<ServerSessionAdvertisementHandler>().AsSelf().InstancePerLifetimeScope().AutoActivate();
    }
}
