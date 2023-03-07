using GameInterface.Serialization;
using System.Collections.Generic;
using Xunit;
using GameInterface.Serialization.Native;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class HashSetSerializationTest
    {
        [Fact]
        public void HashSet_Serialize()
        {
            HashSet<int> HashSet = new HashSet<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(HashSet, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void HashSet_Full_Serialization()
        {
            HashSet<int> HashSet = new HashSet<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(HashSet, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EnumerableBinaryPackage>(obj);

            EnumerableBinaryPackage returnedPackage = (EnumerableBinaryPackage)obj;

            HashSet<int> newHashSet = returnedPackage.Unpack<HashSet<int>>();

            Assert.Equal(HashSet, newHashSet);
        }
    }
}
