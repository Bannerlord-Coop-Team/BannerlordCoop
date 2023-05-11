using GameInterface.Serialization.External;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class SkillObjectSerializationTest
    {
        IContainer container;
        public SkillObjectSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void SkillObject_Serialize()
        {
            SkillObject testSkillObject = new SkillObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            SkillObjectBinaryPackage package = new SkillObjectBinaryPackage(testSkillObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void SkillObject_Full_Serialization()
        {
            SkillObject testSkillObject = new SkillObject("Test");

            var factory = container.Resolve<IBinaryPackageFactory>();
            SkillObjectBinaryPackage package = new SkillObjectBinaryPackage(testSkillObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<SkillObjectBinaryPackage>(obj);

            SkillObjectBinaryPackage returnedPackage = (SkillObjectBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
