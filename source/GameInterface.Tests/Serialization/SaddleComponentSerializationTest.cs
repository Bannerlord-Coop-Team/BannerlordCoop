using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class SaddleComponentSerializationTest
    {
        ILifetimeScope container;
        public SaddleComponentSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void SaddleComponent_Full_Serialization()
        {
            SaddleComponent saddleComponent = (SaddleComponent)FormatterServices.GetUninitializedObject(typeof(SaddleComponent));
            saddleComponent.Item = new ItemObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            SaddleComponentBinaryPackage package = new SaddleComponentBinaryPackage(saddleComponent, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<SaddleComponentBinaryPackage>(obj);

            SaddleComponentBinaryPackage returnedPackage = (SaddleComponentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            SaddleComponent newSaddleComponent = returnedPackage.Unpack<SaddleComponent>(deserializeFactory);

            Assert.Equal(saddleComponent.Item.StringId, newSaddleComponent.Item.StringId);
            Assert.Equal(saddleComponent.ItemModifierGroup, newSaddleComponent.ItemModifierGroup);
        }
    }
}