using GameInterface.Serialization;
using GameInterface.Serialization.Generics;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MBReadOnlyListSerializationTest
    {
        [Fact]
        public void MBReadOnlyList_Serialize()
        {
            List<int> ints = new List<int> { 1, 2, 3, 4, 5 };

            MBReadOnlyList<int> MBReadOnlyList = new MBReadOnlyList<int>(ints);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MBReadOnlyListBinaryPackage package = new MBReadOnlyListBinaryPackage(MBReadOnlyList, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MBReadOnlyList_Full_Serialization()
        {
            List<int> ints = new List<int> { 1, 2, 3, 4, 5 };

            MBReadOnlyList<int> MBReadOnlyList = new MBReadOnlyList<int>(ints);

            BinaryPackageFactory factory = new BinaryPackageFactory();
            MBReadOnlyListBinaryPackage package = new MBReadOnlyListBinaryPackage(MBReadOnlyList, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MBReadOnlyListBinaryPackage>(obj);

            MBReadOnlyListBinaryPackage returnedPackage = (MBReadOnlyListBinaryPackage)obj;

            MBReadOnlyList<int> newMBReadOnlyList = returnedPackage.Unpack<MBReadOnlyList<int>>();

            Assert.Equal(MBReadOnlyList, newMBReadOnlyList);
        }
    }
}
