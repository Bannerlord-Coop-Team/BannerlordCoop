﻿using Autofac;
using Common.LogicStates;
using Common.Network;
using Coop.Core;
using Coop.Core.Client;
using Coop.Core.Common;
using Coop.Core.Server;
using Xunit;

namespace Coop.Tests.Autofac
{
    public class ContainerBuildTests
    {
        [Fact]
        public void Client_Container_Build()
        {
            var containerProvider = new ContainerProvider();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CommonModule>();
            builder.RegisterModule<ClientModule>();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>().SingleInstance();
            var container = builder.Build();

            containerProvider.SetProvider(container);

            Assert.NotNull(container);

            var client = container.Resolve<INetwork>();
            Assert.NotNull(client);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);
        }

        [Fact]
        public void Server_Container_Build()
        {
            var containerProvider = new ContainerProvider();

            ContainerBuilder builder = new ContainerBuilder();
            builder.RegisterModule<CommonModule>();
            builder.RegisterModule<ServerModule>();
            builder.RegisterInstance(containerProvider).As<IContainerProvider>();
            var container = builder.Build();

            containerProvider.SetProvider(container);

            Assert.NotNull(container);

            var server = container.Resolve<INetwork>();
            Assert.NotNull(server);

            var logic = container.Resolve<ILogic>();
            Assert.NotNull(logic);
        }
    }
}
