using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class SkillObjectSerializationTest
    {
        ILifetimeScope container;
        public SkillObjectSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void SkillObject_Full_Serialization()
        {
            SkillObject testSkillObject = new SkillObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            SkillObjectBinaryPackage package = new SkillObjectBinaryPackage(testSkillObject, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<SkillObjectBinaryPackage>(obj);

            SkillObjectBinaryPackage returnedPackage = (SkillObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
