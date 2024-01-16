using Autofac;
using System;

namespace Coop.Core
{
    public interface IContainerProvider
    {
        /// <summary>
        /// Autofac Dependency Injection Container
        /// </summary>
        IContainer GetContainer();

        /// <summary>
        /// DotNet Dependency Injection Service Provider
        /// </summary>
        IServiceProvider GetServiceProvider();

        void SetProvider(IContainer container);
        void SetProvider(IServiceProvider serviceProvider);
    }

    internal class ContainerProvider : IContainerProvider
    {
        private IContainer container;
        private IServiceProvider serviceProvider;

        public IContainer GetContainer() => container;

        public IServiceProvider GetServiceProvider() => serviceProvider;

        public void SetProvider(IContainer container)
        {
            this.container = container;
        }

        public void SetProvider(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }
    }
}
