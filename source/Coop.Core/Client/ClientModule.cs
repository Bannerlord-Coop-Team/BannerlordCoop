using Autofac;
using Common.LogicStates;
using Common.Network;
using Coop.Core.Common.Services.PartyMovement;
using LiteNetLib;

namespace Coop.Core.Client
{
    /// <summary>
    /// Client state DI container
    /// </summary>
    internal class ClientModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ClientLogic>().As<ILogic>().As<IClientLogic>().SingleInstance();
            builder.RegisterType<CoopClient>().As<ICoopClient>().As<INetwork>().As<INetEventListener>().SingleInstance();

            builder.RegisterType<PartyMovementHandler>().As<IPartyMovementHandler>().SingleInstance().AutoActivate();

            base.Load(builder);
        }
    }
}
