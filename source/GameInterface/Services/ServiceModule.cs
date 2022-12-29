using Autofac;
using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services
{
    internal class ServiceModule : Module, IServiceModule
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ServiceModule>().As<IServiceModule>().SingleInstance();

            foreach (var type in GetHandlers())
            {
                builder.RegisterType(type).AsSelf().SingleInstance();
            }

            foreach (var type in GetInterfaces())
            {
                var interfaceToRegister = type.GetInterfaces().SingleOrDefault(
                    i => typeof(IGameAbstraction).IsAssignableFrom(i) &&
                    i != typeof(IGameAbstraction));

                if(interfaceToRegister != null)
                {
                    builder.RegisterType(type).As(interfaceToRegister).SingleInstance();
                }
                else
                {
                    throw new InvalidOperationException($"{type} must have inherit " +
                        $"from an interface that inherits from {nameof(IGameAbstraction)}");
                }
            }

            base.Load(builder);
        }

        static IHandler[] Handlers;
        public void InstantiateServices(IContainer container)
        {
            List<IHandler> handlers = new List<IHandler>();
            foreach (var type in GetHandlers())
            {
                handlers.Add((IHandler)container.Resolve(type));
            }
            Handlers = handlers.ToArray();
        }

        private IEnumerable<Type> GetHandlers()
        {
            var assembly = GetType().Assembly;
            var @namespace = GetType().Namespace;
            var types = assembly.GetTypes()
                .Where(t => t.GetInterface(nameof(IHandler)) != null &&
                            t.Namespace.StartsWith(@namespace) &&
                            t.IsClass);
            return types;
        }

        private IEnumerable<Type> GetInterfaces()
        {
            var assembly = GetType().Assembly;
            var @namespace = GetType().Namespace;
            var types = assembly.GetTypes()
                .Where(t => t.GetInterface(nameof(IGameAbstraction)) != null &&
                            t.Namespace.StartsWith(@namespace) &&
                            t.IsClass);
            return types;
        }
    }
}
