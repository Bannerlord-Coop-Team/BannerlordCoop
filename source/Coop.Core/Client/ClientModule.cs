using Autofac;
using Common.LogicStates;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Handler;
using Coop.Core.Client.Services.Save.Handler;
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

            // TODO create collector
            builder.RegisterType<PartyMovementHandler>().As<IPartyMovementHandler>().SingleInstance().AutoActivate();
            builder.RegisterType<SaveDataHandler>().AsSelf().InstancePerLifetimeScope().AutoActivate();
            builder.RegisterType<SwitchHeroHandler>().AsSelf().InstancePerLifetimeScope().AutoActivate();
            base.Load(builder);
        }
    }
}
