﻿using Autofac;
using Common.Messaging;
using GameInterface.Services;

namespace GameInterface
{
    public class GameInterfaceModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<MessageBrokerImpl>().As<IMessageBroker>().SingleInstance();
            builder.RegisterType<GameInterface>().As<IGameInterface>().SingleInstance();
            builder.RegisterModule<ServiceModule>();
        }
    }
}
