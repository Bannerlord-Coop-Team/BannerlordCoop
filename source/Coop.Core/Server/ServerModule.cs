﻿using Autofac;
using Common.LogicStates;
using Common.Network;
using Coop.Core.Client;
using Coop.Core.Server.Connections;
using Coop.Core.Server.Services.Save;
using Coop.Core.Server.States;
using LiteNetLib;

namespace Coop.Core.Server
{
    /// <summary>
    /// Server dependencies
    /// </summary>
    internal class ServerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ServerLogic>().As<IServerLogic>().As<ILogic>().InstancePerLifetimeScope();
            builder.RegisterType<CoopServer>().As<ICoopServer>().As<INetwork>().As<INetEventListener>().InstancePerLifetimeScope();
            builder.RegisterType<InitialServerState>().As<IServerState>();
            builder.RegisterType<ClientRegistry>().As<IClientRegistry>().InstancePerLifetimeScope();
            builder.RegisterType<CoopSaveManager>().As<ICoopSaveManager>().InstancePerLifetimeScope();

            foreach (var handlerType in HandlerCollector.Collect<ServerModule>())
            {
                builder.RegisterType(handlerType).AsSelf().InstancePerLifetimeScope().AutoActivate();
            }

            base.Load(builder);
        }
    }
}