using GameInterface.Serialization;
using System.Collections.Generic;
using Xunit;
using GameInterface.Serialization.Native;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ListSerializationTest
    {
        [Fact]
        public void List_Serialize()
        {
            List<int> List = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(List, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void List_Full_Serialization()
        {
            List<int> List = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(List, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EnumerableBinaryPackage>(obj);

            EnumerableBinaryPackage returnedPackage = (EnumerableBinaryPackage)obj;

            List<int> newList = returnedPackage.Unpack<List<int>>();

            Assert.Equal(List, newList);
        }
    }
}
