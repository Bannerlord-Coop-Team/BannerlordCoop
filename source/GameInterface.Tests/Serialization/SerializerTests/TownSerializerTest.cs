using GameInterface.Serialization.Impl;
using GameInterface.Serialization;
using TaleWorlds.Core;
using Xunit;
using System.Runtime.Serialization;
using Common.Extensions;
using System.Reflection;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TownSerializationTest
    {
        [Fact]
        public void Town_Serialize()
        {
            Town testTown = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TownBinaryPackage package = new TownBinaryPackage(testTown, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Town_Full_Serialization()
        {
            Town testTown = (Town)FormatterServices.GetUninitializedObject(typeof(Town));

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TownBinaryPackage package = new TownBinaryPackage(testTown, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TownBinaryPackage>(obj);

            TownBinaryPackage returnedPackage = (TownBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
