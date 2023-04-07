using GameInterface.Serialization;
using Xunit;
using GameInterface.Serialization.Native;
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ArraySerializationTest
    {
        IContainer container;
        public ArraySerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Array_Serialize()
        {
            int[] arr = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var factory = container.Resolve<IBinaryPackageFactory>();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(arr, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Array_Full_Serialization()
        {
            int[] arr = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            var factory = container.Resolve<IBinaryPackageFactory>();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(arr, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EnumerableBinaryPackage>(obj);

            EnumerableBinaryPackage returnedPackage = (EnumerableBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            int[] newArr = returnedPackage.Unpack<int[]>(deserializeFactory);

            Assert.Equal(arr, newArr);
        }

        [Fact]
        public void Staggered_Array_Serialize()
        {
            int[][] arr = new int[2][];
            
            arr[0] = new int[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            arr[1] = new int[] { 5, 6, 7 };

            var factory = container.Resolve<IBinaryPackageFactory>();
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

            var factory = container.Resolve<IBinaryPackageFactory>();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(arr, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EnumerableBinaryPackage>(obj);

            EnumerableBinaryPackage returnedPackage = (EnumerableBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            int[][] newArr = returnedPackage.Unpack<int[][]>(deserializeFactory);

            Assert.Equal(arr, newArr);
        }
    }
}
