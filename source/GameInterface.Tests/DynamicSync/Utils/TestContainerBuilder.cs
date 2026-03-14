using Autofac;
using GameInterface.DynamicSync;
using GameInterface.DynamicSync.Builders;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameInterface.Tests.DynamicSync.Utils;
internal class DynamicSyncTestContainerBuilder
{
    public static IContainer Build(Mock<IObjectManager> objectManagerMock)
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

        return builder.Build();
    }
}
