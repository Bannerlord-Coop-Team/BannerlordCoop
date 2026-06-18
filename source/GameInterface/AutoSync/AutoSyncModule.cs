using Autofac;
using GameInterface.AutoSync;
using GameInterface.AutoSync.Builders;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.AutoSync;

/// <summary>
/// Module for collecting and setting up autosync classes
/// </summary>
internal class AutoSyncModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<AutoSyncPatchCollector>().As<IAutoSyncPatchCollector>().InstancePerLifetimeScope();

        builder.RegisterType<AutoSyncRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncPatcher>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncAssemblyInfoBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncPatchBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncFieldBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncFieldArrayBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncFieldMBListBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncFieldListBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncFieldQueueBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncPropertyBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncPropertyArrayBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncPropertyMBListBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncPropertyListBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncPropertyQueueBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncConstantsBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncFieldPropertyOwnerBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncHandler>().InstancePerLifetimeScope();

        foreach (var type in GetAutoSyncClasses())
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        base.Load(builder);
    }

    private IEnumerable<Type> GetAutoSyncClasses()
    {
        var assembly = GetType().Assembly;
        var @namespace = GetType().Namespace;
        var types = assembly.GetTypes()
            .Where(t => t.GetInterface(nameof(IAutoSync)) != null &&
                        t.IsClass &&
                        t.IsGenericType == false &&
                        t.IsAbstract == false);
        return types;
    }
}
