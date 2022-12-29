using GameInterface.Serialization;
using Xunit;
using GameInterface.Serialization.Native;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArraySerializationTest
    {
        [Fact]
        public void Array_Serialize()
        {
            int[] arr = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(arr, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Array_Full_Serialization()
        {
            int[] arr = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(arr, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EnumerableBinaryPackage>(obj);

            EnumerableBinaryPackage returnedPackage = (EnumerableBinaryPackage)obj;

            int[] newArr = returnedPackage.Unpack<int[]>();

            Assert.Equal(arr, newArr);
        }

        [Fact]
        public void Staggered_Array_Serialize()
        {
            int[][] arr = new int[2][];
            
            arr[0] = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            arr[1] = new int[] { 5, 6, 7 };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(arr, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Staggered_Array_Full_Serialization()
        {
            int[][] arr = new int[2][];

            arr[0] = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            arr[1] = new int[] { 5, 6, 7 };

            BinaryPackageFactory factory = new BinaryPackageFactory();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(arr, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EnumerableBinaryPackage>(obj);

            EnumerableBinaryPackage returnedPackage = (EnumerableBinaryPackage)obj;

            int[][] newArr = returnedPackage.Unpack<int[][]>();

            Assert.Equal(arr, newArr);
        }
    }
}
