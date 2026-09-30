using Autofac;
using Autofac.Builder;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Coop.Core.Server;
using Coop.Core.Server.Services.Telemetry;
using GameInterface;
using Xunit;
#if DEBUG
using Common.Logging;
using System;
using System.Collections.Concurrent;
#endif

namespace Coop.Tests.Server.Services.Telemetry;

/// <summary>
/// Serializes tests that read the process-wide log output.
/// </summary>
[CollectionDefinition(nameof(LogOutputCollection), DisableParallelization = true)]
public sealed class LogOutputCollection
{
}

/// <summary>
/// Tests which server statistics uploader the server container registers.
/// </summary>
[Collection(nameof(LogOutputCollection))]
public class ServerTelemetryRegistrationTests
{
    [Fact]
    public void ServerModule_RegistersOneSharedUploaderForBothInterfaces()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<ServerModule>();
        builder.RegisterModule<GameInterfaceModule>();

        // Skips AutoActivate, so the reporters never build the Release uploader here.
        using var container = builder.Build(ContainerBuildOptions.IgnoreStartableComponents);

        var heartbeat = GetRegistration<IServerTelemetryUploader>(container);
        var battles = GetRegistration<IBattlesFoughtUploader>(container);

        Assert.Equal(heartbeat.Id, battles.Id);
        Assert.Equal(InstanceSharing.Shared, heartbeat.Sharing);
        Assert.IsType<CurrentScopeLifetime>(heartbeat.Lifetime);
#if DEBUG
        Assert.Equal(typeof(DisabledServerTelemetryUploader), heartbeat.Activator.LimitType);
#else
        Assert.Equal(typeof(ServerTelemetryUploader), heartbeat.Activator.LimitType);
#endif
    }

#if DEBUG
    [Fact]
    public void DebugServerContainer_BuildsOneDisabledUploaderAndLogsOnce()
    {
        var messages = new ConcurrentQueue<string>();
        Action<string> callback = messages.Enqueue;
        OutputSinkManager.AddLogCallback(callback);
        try
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<ServerModule>();
            builder.RegisterModule<GameInterfaceModule>();
            using var container = builder.Build();

            var uploader = Assert.IsType<DisabledServerTelemetryUploader>(
                container.Resolve<IServerTelemetryUploader>());
            Assert.Same(uploader, container.Resolve<IBattlesFoughtUploader>());
            Assert.Single(messages, message =>
                message == "Server statistics are off in this DEBUG build; no heartbeat or battle count is sent");
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(callback);
        }
    }
#endif

    private static IComponentRegistration GetRegistration<TService>(IContainer container)
    {
        Assert.True(container.ComponentRegistry.TryGetRegistration(
            new TypedService(typeof(TService)),
            out var registration));
        return registration;
    }
}
