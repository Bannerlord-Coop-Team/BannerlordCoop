using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using Common.Serialization;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class SaddleComponentSerializationTest
    {
        IContainer container;
        public SaddleComponentSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void SaddleComponent_Serialize()
        {
            SaddleComponent saddleComponent = (SaddleComponent)FormatterServices.GetUninitializedObject(typeof(SaddleComponent));
            var factory = container.Resolve<IBinaryPackageFactory>();
            SaddleComponentBinaryPackage package = new SaddleComponentBinaryPackage(saddleComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void SaddleComponent_Full_Serialization()
        {
            SaddleComponent saddleComponent = (SaddleComponent)FormatterServices.GetUninitializedObject(typeof(SaddleComponent));
            saddleComponent.Item = new ItemObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            SaddleComponentBinaryPackage package = new SaddleComponentBinaryPackage(saddleComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<SaddleComponentBinaryPackage>(obj);

            SaddleComponentBinaryPackage returnedPackage = (SaddleComponentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            SaddleComponent newSaddleComponent = returnedPackage.Unpack<SaddleComponent>(deserializeFactory);

            Assert.Equal(saddleComponent.Item.StringId, newSaddleComponent.Item.StringId);
            Assert.Equal(saddleComponent.ItemModifierGroup, newSaddleComponent.ItemModifierGroup);
        }
    }
}