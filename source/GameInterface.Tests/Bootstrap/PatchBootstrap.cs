using Autofac;
using Common.Messaging;
using System;

namespace GameInterface.Tests.Bootstrap;

/// <summary>
/// Easy setup for patch testing
/// </summary>
/// <remarks>
/// Remember to dispose otherwise any other tests that using <see cref="ContainerProvider"/> will run into a race condition
/// </remarks>
internal class PatchBootstrap : IDisposable
{
    public IContainer Container { get; }
    private readonly IDisposable containerLock;

    public PatchBootstrap()
    {
        ContainerBuilder builder = new ContainerBuilder();
        builder.RegisterType<MessageBroker>().As<IMessageBroker>().SingleInstance();
        builder.RegisterModule<GameInterfaceModule>();
        Container = builder.Build();

        // This will not allow changing of the container in ContainerProvider until containerLock is disposed
        // Remember to dispose
        containerLock = ContainerProvider.UseContainerThreadSafe(Container);
    }

    public void Dispose()
    {
        containerLock.Dispose();
    }
}
