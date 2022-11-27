using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;
using Common.Extensions;
using System.Reflection;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class SkillObjectSerializationTest
    {
        [Fact]
        public void SkillObject_Serialize()
        {
            SkillObject testSkillObject = (SkillObject)FormatterServices.GetUninitializedObject(typeof(SkillObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            SkillObjectBinaryPackage package = new SkillObjectBinaryPackage(testSkillObject, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void SkillObject_Full_Serialization()
        {
            SkillObject testSkillObject = (SkillObject)FormatterServices.GetUninitializedObject(typeof(SkillObject));

            BinaryPackageFactory factory = new BinaryPackageFactory();
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
