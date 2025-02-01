using System.Collections.Generic;
using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using Common.Tests.Utils;
using Coop.Core;
using Coop.Tests.Mocks;
using GameInterface.Services.Modules;
using GameInterface.Services.Modules.Validators;
using Moq;
using Xunit.Abstractions;

namespace Coop.Tests;

internal abstract class TestComponentBase
{
    public ITestOutputHelper Output { get; }

    public TestMessageBroker TestMessageBroker { get; protected set; }
    public TestNetwork TestNetwork { get; protected set; }
    public IContainer Container { get; protected set; }

    protected TestComponentBase(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>
    /// Builds container and assigns class properties for ease of use in testing
    /// </summary>
    /// <param name="builder">Container builder</param>
    /// <returns>Container with all common types registered</returns>
    protected IContainer BuildContainer(ContainerBuilder builder)
    {
        RegisterCommonTypes(builder);

        var container = SetupContainerProvider(builder);

        TestMessageBroker = container.Resolve<TestMessageBroker>();
        TestNetwork = container.Resolve<TestNetwork>();
        Container = container;

        TestMessageBroker.Messages.Clear();
        TestNetwork.Clear();

        return container;
    }

    private ContainerBuilder RegisterCommonTypes(ContainerBuilder builder)
    {
        var moduleInfoProviderMock = new Mock<IModuleInfoProvider>();
        moduleInfoProviderMock.Setup(x => x.GetModuleInfos()).Returns(new List<ModuleInfo>());
        
        builder.RegisterType<SerializableTypeMapper>().As<ISerializableTypeMapper>().InstancePerLifetimeScope();
        builder.RegisterType<ProtoBufSerializer>().As<ICommonSerializer>().InstancePerLifetimeScope();
        builder.RegisterType<TestMessageBroker>().AsSelf().As<IMessageBroker>().InstancePerLifetimeScope();
        builder.RegisterType<ContainerProvider>().As<IContainerProvider>().InstancePerLifetimeScope();
        builder.RegisterType<TestNetwork>().AsSelf().As<INetwork>().InstancePerLifetimeScope();
        builder.RegisterInstance(moduleInfoProviderMock.Object).As<IModuleInfoProvider>().SingleInstance();
        builder.RegisterType<ModuleValidator>().As<IModuleValidator>().SingleInstance();
        return builder;
    }

    private IContainer SetupContainerProvider(ContainerBuilder builder)
    {
        var container = builder.Build();

        container.Resolve<IContainerProvider>().SetProvider(container);

        return container;
    }
}
