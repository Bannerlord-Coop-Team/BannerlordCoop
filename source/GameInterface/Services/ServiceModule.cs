using Autofac;
using Common.Audit;
using Common.Extensions;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameInterface.Services;

internal class ServiceModule : Module
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServiceModule>();

    private const string NAMESPACE = "GameInterface";

    protected override void Load(ContainerBuilder builder)
    {
        foreach (var type in GetHandlers())
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        foreach (var type in GetAuditors())
        {
            builder.RegisterType(type).AsSelf().InstancePerLifetimeScope().AutoActivate();
        }

        foreach (var type in GetGameAbstractions())
        {
            var interfaceToRegister = type.GetInterfaces().SingleOrDefault(
                i => typeof(IGameAbstraction).IsAssignableFrom(i) &&
                i != typeof(IGameAbstraction));

            if(interfaceToRegister != null)
            {
                Logger.Verbose("Registering {type} GameInterface Service", type.Name);

                builder.RegisterType(type).As(interfaceToRegister).InstancePerLifetimeScope();
            }
            else
            {
                throw new InvalidOperationException($"{type} must have inherit " +
                    $"from an interface that inherits from {nameof(IGameAbstraction)}");
            }
        }

        base.Load(builder);
    }

    // Namespace is needed to separate client and server handlers being registered with DI
    private IEnumerable<Type> GetHandlers()
    {
        var handlers = InterfaceCollector.GetInterfaces<IHandler>(NAMESPACE);

        // When dynamic sync generates its code at runtime, the generated handlers are created by
        // DynamicSyncPatcher.BindHandlers and must NOT also be activated by the container: the
        // "DynamicSync" namespace scan covers every loaded assembly, so any container built after
        // the runtime assembly exists (a second test environment, or rejoining coop in the same
        // process) would construct a second handler per type and every synced message would be
        // applied twice. Only when running from the compiled export (generation disabled) is the
        // container the sole owner of the generated handlers.
        if (DynamicSync.DynamicSyncConfiguration.Enabled)
            return handlers;

        return handlers.Concat(InterfaceCollector.GetInterfaces<IHandler>("DynamicSync"));
    }

    // Namespace is needed to separate client and server handlers being registered with DI
    private IEnumerable<Type> GetGameAbstractions() => InterfaceCollector.GetInterfaces<IGameAbstraction>(NAMESPACE);

    // Namespace is needed to separate client and server handlers being registered with DI
    private IEnumerable<Type> GetAuditors() => InterfaceCollector.GetInterfaces<IAuditor>(NAMESPACE);

    
}
