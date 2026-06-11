using Autofac;
using Common.Messaging;
using Common.Network;
using Common.Serialization;
using GameInterface.DynamicSync;
using GameInterface.Registry;
using GameInterface.Serialization;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using Moq;
using Serilog;

namespace GameInterface.Tests.Bootstrap.Modules
{
    internal class SerializationTestModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var logger = new LoggerConfiguration().CreateLogger();

            base.Load(builder);
            builder.RegisterType<BinaryPackageFactory>().As<IBinaryPackageFactory>();

            builder.RegisterInstance(logger).As<ILogger>();
            builder.RegisterInstance(new Mock<IMessageBroker>().Object).As<IMessageBroker>();
            builder.RegisterModule<ObjectManagerModule>();

            builder.RegisterInstance(new Mock<INetwork>().Object).As<INetwork>();
            builder.RegisterInstance(new Mock<IDynamicSyncPatchCollector>().Object).As<IDynamicSyncPatchCollector>();
            builder.RegisterInstance(new Mock<ISerializableTypeMapper>().Object).As<ISerializableTypeMapper>();
            builder.RegisterInstance(new Mock<ISerializableTypeMapper>().Object).As<ISerializableTypeMapper>();
            builder.RegisterInstance(new Mock<IControllerIdProvider>().Object).As<IControllerIdProvider>();
            builder.RegisterModule<RegistryModule>();
        }
    }
}
