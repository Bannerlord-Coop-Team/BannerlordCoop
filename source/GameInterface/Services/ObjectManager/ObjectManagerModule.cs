using Autofac;
using Common;
using Common.Logging;
using GameInterface.Services.Registry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services.ObjectManager;
internal class ObjectManagerModule : Module
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServiceModule>();

    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ObjectManager>().As<IObjectManager>().InstancePerLifetimeScope();
        builder.RegisterType<RegistryCollection>().As<IRegistryCollection>().InstancePerLifetimeScope();


        foreach (var type in GetRegistries())
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        base.Load(builder);
    }

    private IEnumerable<Type> GetRegistries()
    {
        var assembly = GetType().Assembly;
        var types = assembly.GetTypes()
            .Where(t => t.GetInterface(nameof(IRegistry)) != null &&
                        t.IsClass && !t.IsAbstract && !t.IsGenericType);
        return types;
    }
}
