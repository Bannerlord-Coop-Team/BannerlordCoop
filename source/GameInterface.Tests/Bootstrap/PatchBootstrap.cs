using Autofac;
using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Configuration;
using Coop.Tests.Mocks;
using System;
using System.Threading;

namespace GameInterface.Tests.Bootstrap;

/// <summary>
/// Easy setup for patch testing
/// </summary>
/// <remarks>
/// Remember to dispose otherwise any other tests that using <see cref="ContainerProvider"/> will run into a race condition
/// </remarks>
internal class PatchBootstrap : IDisposable
{
    private static readonly SemaphoreSlim _sem = new SemaphoreSlim(1);
    public IContainer Container { get; }
    private readonly IDisposable containerLock;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public PatchBootstrap()
    {
        if (_sem.Wait(Timeout) == false)
        {
            throw new InvalidOperationException($"Unable to get semaphore after {Timeout.Seconds} seconds");
        }

        GameBootStrap.Initialize();

        ContainerBuilder builder = new ContainerBuilder();
        builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
        builder.RegisterType<TestNetwork>().As<INetwork>().SingleInstance();
        builder.RegisterType<NetworkConfiguration>().As<INetworkConfiguration>().SingleInstance();
        builder.RegisterModule<GameInterfaceModule>();
        Container = builder.Build();

        // This will not allow changing of the container in ContainerProvider until containerLock is disposed
        // Remember to dispose
        containerLock = ContainerProvider.UseContainerThreadSafe(Container);
    }

    ~PatchBootstrap()
    {
        Dispose();
    }

    public void Dispose()
    {
        containerLock.Dispose();

        _sem.Release();
    }
}
