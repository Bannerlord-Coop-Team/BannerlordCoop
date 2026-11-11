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
        builder.RegisterType<RegistryManager>().As<IRegistryManager>().SingleInstance().AutoActivate();
        builder.RegisterType<AutoRegistryFactory>().As<IAutoRegistryFactory>().SingleInstance().AutoActivate();

        foreach (var type in GetRegistries())
        {
            builder.RegisterType(type).AsSelf().SingleInstance().AutoActivate();
        }

        base.Load(builder);
    }

    // Whether a type is an auto registry, which GameInterfaceModule also asks of its naval-only types.
    internal static bool IsAutoRegistry(Type type) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAutoRegistry<>));

    // Coop.Naval registers its own registries, otherwise they'd be registered without the naval services they need.
    // GameInterfaceModule registers GameInterface's naval registries itself, behind its NavalDLC decision.
    private IEnumerable<Type> GetRegistries()
    {
        return AppDomain.CurrentDomain.GetDomainTypes()
            .Where(t => t.IsConcrete() &&
                        t.Assembly.GetName().Name != GameInterfaceModule.CoopNavalAssemblyName &&
                        !GameInterfaceModule.NavalOnlyTypes.Contains(t) &&
                        IsAutoRegistry(t));
    }
}