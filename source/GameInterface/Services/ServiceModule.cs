using Autofac;
using Common.Messaging;
using GameInterface.Services.GameDebug.Interfaces;
using GameInterface.Services.GameState.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace GameInterface.Services
{
    internal class ServiceModule : Module, IServiceModule
    {
        private IEnumerable<Type> GetHandlers()
        {
            var assembly = GetType().Assembly;
            var @namespace = GetType().Namespace;
            var types = assembly.GetTypes()
                .Where(t => t.GetInterface(nameof(IHandler)) != null &&
                            t.Namespace.StartsWith(@namespace));
            return types;
        }

        protected override void Load(ContainerBuilder builder)
        {
            foreach (var type in GetHandlers())
            {
                builder.RegisterType(type).AsSelf().SingleInstance();
            }

            base.Load(builder);
        }

        static IHandler[] Handlers;
        public void InstantiateServices(IContainer container)
        {
            List<IHandler> handlers = new List<IHandler>();
            foreach(var type in GetHandlers())
            {
                handlers.Add((IHandler)container.Resolve(type));
            }
            Handlers = handlers.ToArray();
        }
    }
}
