using Autofac;
using Common.Logging;
using Common.Messaging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services;

internal class ServiceModule : Module
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServiceModule>();

    protected override void Load(ContainerBuilder builder)
    {
        foreach (var type in GetHandlers())
        {
            builder.RegisterType(type).AsSelf().SingleInstance().AutoActivate();
        }

        foreach (var type in GetInterfaces())
        {
            var interfaceToRegister = type.GetInterfaces().SingleOrDefault(
                i => typeof(IGameAbstraction).IsAssignableFrom(i) &&
                i != typeof(IGameAbstraction));

            if(interfaceToRegister != null)
            {
                Logger.Verbose("Registering {type} GameInterface Service", type.Name);

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
