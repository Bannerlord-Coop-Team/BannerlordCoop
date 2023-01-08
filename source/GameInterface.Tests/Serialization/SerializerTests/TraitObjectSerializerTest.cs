using GameInterface.Serialization.External;
using GameInterface.Serialization;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TraitObjectSerializationTest
    {
        [Fact]
        public void TraitObject_Serialize()
        {
            TraitObject testTraitObject = (TraitObject)FormatterServices.GetUninitializedObject(typeof(TraitObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TraitObjectBinaryPackage package = new TraitObjectBinaryPackage(testTraitObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void TraitObject_Full_Serialization()
        {
            TraitObject testTraitObject = (TraitObject)FormatterServices.GetUninitializedObject(typeof(TraitObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TraitObjectBinaryPackage package = new TraitObjectBinaryPackage(testTraitObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TraitObjectBinaryPackage>(obj);

            TraitObjectBinaryPackage returnedPackage = (TraitObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
