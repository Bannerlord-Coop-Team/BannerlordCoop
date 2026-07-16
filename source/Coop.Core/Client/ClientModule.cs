using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.PacketHandlers;
using Coop.Core.Client.Policies;
using Coop.Core.Client.Services.Session;
using Coop.Core.Client.States;
using Coop.Core.Common;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Session;
using Coop.Steam;
using GameInterface.Policies;
using LiteNetLib;
using Missions;
using System.Runtime.CompilerServices;

namespace Coop.Core.Client;

/// <summary>
/// Client state DI container
/// </summary>
public class ClientModule : CommonModule
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterModule<MissionModule>();

        builder.RegisterType<ClientContext>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<ClientLogic>().As<ILogic>().As<IClientLogic>().InstancePerLifetimeScope();
        builder.RegisterType<CoopClient>().As<ICoopClient>().As<INetwork>().As<IRelayNetwork>().As<INetEventListener>().InstancePerLifetimeScope();

        // Policies
        builder.RegisterType<ClientSyncPolicy>().As<ISyncPolicy>().InstancePerLifetimeScope();

        // Steam registrations only when the boot probe found Steam, so tests and non-Steam installs never load Steamworks types.
        if (SessionDiscovery.SteamAvailable)
        {
            RegisterSteamSessionServices(builder);
        }
        else
        {
            builder.RegisterType<NoopSessionAdvertiser>().As<ISessionAdvertiser>().InstancePerLifetimeScope();
            builder.RegisterType<NoopSessionTunnelHost>().As<ISessionTunnelHost>().InstancePerLifetimeScope();
        }

        builder.RegisterType<ConfiguredSessionJoinInfoSource>().As<ISessionJoinInfoSource>().InstancePerLifetimeScope();
        builder.RegisterType<SessionAdvertisementConfig>().AsSelf().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<ClientModule, IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<ClientModule, IPacketHandler>(builder, autoInstantiate: true);
    }

    // The tunnel transport's layout embeds Steamworks value types, so mentioning it in Load
    // would pull in Steamworks.NET while JIT-compiling Load even when the Steam branch is
    // never taken; this non-inlined helper is only compiled once Steam is known to be present.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RegisterSteamSessionServices(ContainerBuilder builder)
    {
        builder.RegisterInstance(SteamBoot.JoinListener)
            .As<ISteamLobbyMembership>()
            .ExternallyOwned();
        builder.RegisterType<SteamLobbyApi>()
            .As<ISteamLobbyApi>()
            .As<ISteamPublicLobbyApi>()
            .InstancePerLifetimeScope();
        builder.RegisterType<SteamLobbyAdvertiser>().As<ISessionAdvertiser>().InstancePerLifetimeScope();
        builder.RegisterType<SessionLobbyMembershipHandler>().AsSelf().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<SteamNetworkingTunnelTransport>().As<ISteamTunnelTransport>().InstancePerLifetimeScope();
        builder.RegisterType<SteamTunnelHost>().As<ISessionTunnelHost>().InstancePerLifetimeScope();
        builder.RegisterType<SteamMissionBridge>().As<ISteamMissionBridge>().InstancePerLifetimeScope();
    }
}
