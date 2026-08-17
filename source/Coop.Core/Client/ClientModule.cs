using Autofac;
using Common.LogicStates;
using Common.Messaging;
using Common.Network;
using Common.Network.Session;
using Common.PacketHandlers;
using Coop.Core.Client.Policies;
using Coop.Core.Client.Services.Kingdoms;
using Coop.Core.Client.Services.MobileParties;
using Coop.Core.Client.Services.Session;
using Coop.Core.Client.States;
using Coop.Core.Common;
using Coop.Core.Common.Configuration;
using Coop.Core.Common.Session;
using GameInterface.Policies;
using LiteNetLib;
using Missions;
using System;

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
        builder.RegisterType<CoopClient>()
            .As<ICoopClient>()
            .As<INetwork>()
            .As<IRelayNetwork>()
            .As<INetEventListener>()
            .As<ILocalPeerEndpointSource>()
            .InstancePerLifetimeScope();
        builder.RegisterType<PlayerPartyTroopXpBaselineApplier>()
            .As<IPlayerPartyTroopXpBaselineApplier>()
            .InstancePerDependency();
        builder.RegisterType<AllianceOfferPendingApplier>()
            .As<IAllianceOfferPendingApplier>()
            .InstancePerDependency();
        builder.RegisterType<PeaceOfferPendingApplier>()
            .As<IPeaceOfferPendingApplier>()
            .InstancePerDependency();

        // Policies
        builder.RegisterType<ClientSyncPolicy>().As<ISyncPolicy>().InstancePerLifetimeScope();

        RegisterSessionProviderRuntime(builder, isServer: false);

        builder.RegisterType<ConfiguredSessionJoinInfoSource>().As<ISessionJoinInfoSource>().InstancePerLifetimeScope();
        builder.RegisterType<SessionAdvertisementConfig>().AsSelf().InstancePerLifetimeScope();

        // Keeps the module resolvable on its own; a session container registers the real intent.
        builder.Register(c =>
        {
            var config = c.Resolve<INetworkConfig>();
            return JoinAttemptPresentation.For(JoinIntent.PlayerDirect, config.Address, config.Port);
        }).AsSelf().InstancePerLifetimeScope();

        RegisterAllTypesWithInterface<ClientModule, IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<ClientModule, IPacketHandler>(builder, autoInstantiate: true);
    }

    internal static void RegisterSessionProviderRuntime(ContainerBuilder builder, bool isServer)
    {
        builder.Register(context =>
            {
                var provider = isServer
                    ? SessionDiscovery.ServerProvider
                    : SessionDiscovery.ClientProvider;
                var networkConfig = context.Resolve<INetworkConfig>();
                var options = new SessionProviderRuntimeOptions
                {
                    PeerIdentityBridgeName = networkConfig.PeerIdentityBridgeName,
                };

                if (provider == null)
                {
                    var advertiser = new NoopSessionAdvertiser();
                    var tunnelHost = new NoopSessionTunnelHost();
                    var peerIdentityResolver = (IAuthenticatedPeerIdentityResolver)tunnelHost;
                    var providerResources = Array.Empty<IDisposable>();
                    if (isServer && PeerIdentityBridgeName.IsValid(options.PeerIdentityBridgeName))
                    {
                        var bridgeResolver = new NamedPipePeerIdentityResolver(options.PeerIdentityBridgeName);
                        peerIdentityResolver = bridgeResolver;
                        providerResources = new IDisposable[] { bridgeResolver };
                    }

                    var unavailable = UnavailableSessionServices.Instance;
                    return new SessionProviderRuntime(
                        advertiser,
                        tunnelHost,
                        unavailable,
                        unavailable,
                        unavailable,
                        unavailable,
                        new NoopMissionPeerTransport(),
                        peerIdentityResolver,
                        NoopPeerIdentityPublisher.Instance,
                        providerResources);
                }

                if (isServer)
                {
                    options.Visibility = context.Resolve<SessionAdvertisementConfig>().Visibility;
#if DEBUG
                    options.Visibility = ServerVisibility.None;
#endif
                    return provider.CreateServerRuntime(options);
                }

                options.Visibility = context.Resolve<SessionAdvertisementConfig>().Visibility;
                return provider.CreateClientRuntime(options);
            })
            .As<ISessionProviderRuntime>()
            .InstancePerLifetimeScope();

        builder.Register(context => context.Resolve<ISessionProviderRuntime>().Advertiser)
            .As<ISessionAdvertiser>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
        builder.Register(context => context.Resolve<ISessionProviderRuntime>().TunnelHost)
            .As<ISessionTunnelHost>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
        builder.Register(context => context.Resolve<ISessionProviderRuntime>().PeerIdentityResolver)
            .As<IAuthenticatedPeerIdentityResolver>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
        builder.Register(context => context.Resolve<ISessionProviderRuntime>().Membership)
            .As<ISessionMembership>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
        builder.Register(context => context.Resolve<ISessionProviderRuntime>().AdvertisementOwner)
            .As<ISessionAdvertisementOwner>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
        builder.Register(context => context.Resolve<ISessionProviderRuntime>().ServerReadiness)
            .As<ISessionServerReadiness>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
        builder.Register(context => context.Resolve<ISessionProviderRuntime>().TransportTargetSource)
            .As<ISessionTransportTargetSource>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
        builder.Register(context => context.Resolve<ISessionProviderRuntime>().MissionTransport)
            .As<IMissionPeerTransport>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
        builder.Register(context => context.Resolve<ISessionProviderRuntime>().PeerIdentityPublisher)
            .As<IPeerIdentityPublisher>()
            .InstancePerLifetimeScope()
            .ExternallyOwned();
    }
}
