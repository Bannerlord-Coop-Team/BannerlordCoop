using Autofac;
using Common.Extensions;
using Common.Util;
using GameInterface.Registry.Auto;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Registry;
internal class RegistryModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RegistryManager>().As<IRegistryManager>().InstancePerLifetimeScope().AutoActivate();
        builder.RegisterType<AutoRegistryFactory>().As<IAutoRegistryFactory>().InstancePerLifetimeScope().AutoActivate();

        foreach (var type in GetRegistries())
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        base.Load(builder);
    }

    private IEnumerable<Type> GetRegistries()
    {
        return AppDomain.CurrentDomain.GetDomainTypes()
            .Where(t => t.IsConcrete() &&
                        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAutoRegistry<>)));
    }
}