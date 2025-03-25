using Autofac;
using GameInterface.DynamicSync;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.AutoSync;

/// <summary>
/// Module for collecting and setting up autosync classes
/// </summary>
internal class DynamicSyncModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<DynamicSyncRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncPatcher>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncPatchProcessor>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncBuilder>().As<IDynamicSyncBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicHandler>().InstancePerLifetimeScope();

        foreach (var type in GetDynamicSyncClasses())
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        base.Load(builder);
    }

    private IEnumerable<Type> GetDynamicSyncClasses()
    {
        var assembly = GetType().Assembly;
        var @namespace = GetType().Namespace;
        var types = assembly.GetTypes()
            .Where(t => t.GetInterface(nameof(IDynamicSync)) != null &&
                        t.IsClass &&
                        t.IsGenericType == false &&
                        t.IsAbstract == false);
        return types;
    }
}
