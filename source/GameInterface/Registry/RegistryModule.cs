using Autofac;
using GameInterface.Registry.Auto;
using GameInterface.Services.Alleys;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Registry;
internal class RegistryModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AutoRegistryFactory>().As<IAutoRegistryFactory>().InstancePerLifetimeScope().AutoActivate();

        foreach (var type in GetRegistries())
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        base.Load(builder);
    }

    private IEnumerable<Type> GetRegistries()
    {
        var assembly = GetType().Assembly;
        var @namespace = GetType().Namespace;

        var types = assembly.GetTypes()
            .Where(t => 
                t.IsClass &&
                !t.IsAbstract &&
                t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAutoRegistry<>)));
        return types;
    }
}
