using Autofac;
using System;

namespace GameInterface.Tests.Bootstrap.Modules;

/// <summary>
/// Shares one <see cref="SerializationTestModule"/> container across serialization tests, one scope per test.
/// </summary>
internal static class SerializationTestContainer
{
    private static readonly Lazy<IContainer> Container = new(BuildContainer);

    /// <summary>
    /// Starts a scope with its own object manager, so registrations made by one test never reach another.
    /// </summary>
    public static ILifetimeScope BeginScope() => Container.Value.BeginLifetimeScope(scopeBuilder =>
    {
        // ObjectManager is mutable test state, so each test needs its own registration store.
        scopeBuilder.RegisterType<global::GameInterface.Services.ObjectManager.ObjectManager>()
            .As<global::GameInterface.Services.ObjectManager.IObjectManager>()
            .SingleInstance();
    });

    // The registry modules scan every loaded type, so this runs once per test run instead of once per test.
    private static IContainer BuildContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<SerializationTestModule>();
        return builder.Build();
    }
}
