using Autofac;
using Common;
using Common.Logging;
using GameInterface.Registry;
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
        builder.RegisterType<ObjectManager>().As<IObjectManager>().SingleInstance();
        builder.RegisterType<RegistryCollection>().As<IRegistryCollection>().SingleInstance();


        foreach (var type in GetRegistries())
        {
            builder.RegisterType(type).AsSelf().SingleInstance().AutoActivate();
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
