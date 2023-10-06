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
public class ClientModule : CommonModule
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ClientLogic>().As<ILogic>().As<IClientLogic>().SingleInstance();
        builder.RegisterType<CoopClient>().As<ICoopClient>().As<INetwork>().As<INetEventListener>().SingleInstance();

        RegisterAllTypesWithInterface<ClientModule, IHandler>(builder, autoInstantiate: true);
        RegisterAllTypesWithInterface<ClientModule, IPacketHandler>(builder, autoInstantiate: true);

        RegisterAllTypesWithInterface<ClientModule, IClientState>(builder);

        base.Load(builder);
    }
}
