﻿using Autofac;
using GameInterface.AutoSync.Internal;
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

        builder.RegisterType<AutoSyncPatcher>().As<IAutoSyncPatcher>().InstancePerLifetimeScope();
        builder.RegisterType<AutoSyncPropertyMapper>().As<IAutoSyncPropertyMapper>().InstancePerLifetimeScope();

        foreach(var type in GetAutoSyncClasses())
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
