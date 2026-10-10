using Autofac;
using Common.Messaging;
using Common.Util;
using GameInterface;
using GameInterface.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Naval;

public class CoopNavalModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        base.Load(builder);

        builder.RegisterInstance(HarmonyPatchCategoryRegistration.Uncategorized(typeof(CoopNavalModule).Assembly));

        foreach (var type in GetConcreteTypes<IHandler>())
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        foreach (var type in GetConcreteTypes<IGameAbstraction>())
        {
            var interfaceToRegister = type.GetInterfaces().SingleOrDefault(
                i => typeof(IGameAbstraction).IsAssignableFrom(i) &&
                i != typeof(IGameAbstraction));

            if (interfaceToRegister == null)
            {
                throw new InvalidOperationException($"{type} must have inherit " +
                    $"from an interface that inherits from {nameof(IGameAbstraction)}");
            }

            builder.RegisterType(type).As(interfaceToRegister).InstancePerLifetimeScope();
        }
    }

    private static IEnumerable<Type> GetConcreteTypes<T>() =>
        typeof(CoopNavalModule).Assembly.GetTypes().Where(t => typeof(T).IsAssignableFrom(t) && t.IsConcrete());
}
