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
    private readonly bool navalServicesEnabled;

    // Registries for every game type, without GameInterface's naval ones unless NavalDLC co-op is active.
    public RegistryModule() : this(Common.ModInformation.IsNavalDlcActive)
    {
    }

    // Lets GameInterfaceModule pass its naval decision down.
    public RegistryModule(bool navalServicesEnabled)
    {
        this.navalServicesEnabled = navalServicesEnabled;
    }

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

    // Coop.Naval registers its own registries, otherwise they'd be registered without the naval services they need.
    // GameInterface's naval registries are skipped while NavalDLC co-op is off.
    private IEnumerable<Type> GetRegistries()
    {
        return AppDomain.CurrentDomain.GetDomainTypes()
            .Where(t => t.IsConcrete() &&
                        t.Assembly.GetName().Name != GameInterfaceModule.CoopNavalAssemblyName &&
                        GameInterfaceModule.ShouldRegister(t, navalServicesEnabled) &&
                        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAutoRegistry<>)));
    }
}