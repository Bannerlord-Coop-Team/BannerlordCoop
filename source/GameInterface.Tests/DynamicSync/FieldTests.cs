using Autofac;
using GameInterface.DynamicSync;
using GameInterface.DynamicSync.Builders;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.DynamicSync;

public class FieldTests
{
    readonly Mock<IObjectManager> objectManagerMock = new Mock<IObjectManager>();

    readonly IContainer container;
    public FieldTests()
    {
        ContainerBuilder builder = new ContainerBuilder();

        builder.RegisterType<DynamicSyncRegistry>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncAssemblyInfoBuilder>().InstancePerLifetimeScope();

        builder.RegisterInstance(objectManagerMock.Object).SingleInstance();

        builder.RegisterType<DynamicSyncPropertyBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncFieldBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncPropertyArrayBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncFieldArrayBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncPropertyMBListBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncFieldMBListBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncPropertyListBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncFieldListBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncPropertyQueueBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncFieldQueueBuilder>().InstancePerLifetimeScope();

        builder.RegisterType<DynamicSyncPatchBuilder>().InstancePerLifetimeScope();
        builder.RegisterType<DynamicSyncBuilder>().InstancePerLifetimeScope();

        container = builder.Build();
    }

    [Fact]
    public void BuildFromValueField()
    {
        var dynamicSyncRegistry = container.Resolve<DynamicSyncRegistry>();

        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(FieldTestClass), "MyField"));

        var builder = container.Resolve<DynamicSyncBuilder>();

        builder.Build();
    }

    [Fact]
    public void BuildFromRefField()
    {
        var dynamicSyncRegistry = container.Resolve<DynamicSyncRegistry>();

        objectManagerMock.Setup(x => x.IsTypeManaged(typeof(RefClass))).Returns(true);

        dynamicSyncRegistry.AddField(AccessTools.Field(typeof(FieldRefTestClass), "MyRefField"));

        var builder = container.Resolve<DynamicSyncBuilder>();

        builder.Build();
    }

    [Fact]
    public void BuildFromProperty()
    {
        var dynamicSyncRegistry = container.Resolve<DynamicSyncRegistry>();

        dynamicSyncRegistry.AddProperty(AccessTools.Property(typeof(PropertyTestClass), "MyProperty"));

        var builder = container.Resolve<DynamicSyncBuilder>();

        builder.Build();
    }
}

public class FieldTestClass
{
    private int MyField = 5;
    public void SetField(int newValue)
    {
        MyField = newValue;
    }
    public int GetField()
    {
        return MyField;
    }
}

public class PropertyTestClass
{
    public int MyProperty { get; private set; } = 213;
}


public class FieldRefTestClass
{
    private RefClass MyRefField = new RefClass();

    public void SetField(RefClass newRef)
    {
        MyRefField = newRef;
    }
    public RefClass GetField()
    {
        return MyRefField;
    }
}

public class RefClass { }