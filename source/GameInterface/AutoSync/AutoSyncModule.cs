using Autofac;
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
        builder.RegisterType<AutoSyncPatchCollector>().As<IAutoSyncPatchCollector>().SingleInstance();

        builder.RegisterType<AutoSyncRegistry>().SingleInstance();
        builder.RegisterType<AutoSyncPatcher>().SingleInstance();
        builder.RegisterType<AutoSyncBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncAssemblyInfoBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncPatchBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncFieldBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncFieldArrayBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncFieldMBListBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncFieldListBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncFieldQueueBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncFieldDictionaryBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncPropertyBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncPropertyArrayBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncPropertyMBListBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncPropertyListBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncPropertyQueueBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncPropertyDictionaryBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncConstantsBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncFieldPropertyOwnerBuilder>().SingleInstance();
        builder.RegisterType<AutoSyncHandler>().SingleInstance();

        foreach (var type in GetAutoSyncClasses())
        {
            builder.RegisterType(type).AsSelf().SingleInstance().AutoActivate();
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
