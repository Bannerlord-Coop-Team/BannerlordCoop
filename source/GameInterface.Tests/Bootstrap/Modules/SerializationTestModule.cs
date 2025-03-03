using Autofac;
using Common.Messaging;
using GameInterface.Serialization;
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
        }
    }
}
