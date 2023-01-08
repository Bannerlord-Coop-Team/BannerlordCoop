using GameInterface.Serialization;
using GameInterface.Serialization.External;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class SaddleComponentSerializationTest
    {
        [Fact]
        public void SaddleComponent_Serialize()
        {
            SaddleComponent saddleComponent = (SaddleComponent)FormatterServices.GetUninitializedObject(typeof(SaddleComponent));
            BinaryPackageFactory factory = new BinaryPackageFactory();
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

            BinaryPackageFactory factory = new BinaryPackageFactory();
            SaddleComponentBinaryPackage package = new SaddleComponentBinaryPackage(saddleComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<SaddleComponentBinaryPackage>(obj);

            SaddleComponentBinaryPackage returnedPackage = (SaddleComponentBinaryPackage)obj;

            SaddleComponent newSaddleComponent = returnedPackage.Unpack<SaddleComponent>();

            Assert.Equal(saddleComponent.Item.StringId, newSaddleComponent.Item.StringId);
            Assert.Equal(saddleComponent.ItemModifierGroup, newSaddleComponent.ItemModifierGroup);
        }
    }
}