using GameInterface.Serialization;
using System.Collections.Generic;
using Xunit;
using GameInterface.Serialization.Native;
using Autofac;
using Common.Serialization;
using GameInterface.Tests.Bootstrap.Modules;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ListSerializationTest
    {
        IContainer container;
        public ListSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void List_Serialize()
        {
            List<int> list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

            var factory = container.Resolve<IBinaryPackageFactory>();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(list, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void List_Full_Serialization()
        {
            List<int> list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

            var factory = container.Resolve<IBinaryPackageFactory>();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(list, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<EnumerableBinaryPackage>(obj);

            EnumerableBinaryPackage returnedPackage = (EnumerableBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            List<int> newList = returnedPackage.Unpack<List<int>>(deserializeFactory);

            Assert.Equal(list, newList);
        }
    }
}
