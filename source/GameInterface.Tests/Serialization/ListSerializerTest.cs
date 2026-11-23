using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.Native;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class ListSerializationTest
    {
        ILifetimeScope container;
        public ListSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void List_Full_Serialization()
        {
            List<int> list = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };

            var factory = container.Resolve<IBinaryPackageFactory>();
            EnumerableBinaryPackage package = new EnumerableBinaryPackage(list, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<EnumerableBinaryPackage>(obj);

            EnumerableBinaryPackage returnedPackage = (EnumerableBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            List<int> newList = returnedPackage.Unpack<List<int>>(deserializeFactory);

            Assert.Equal(list, newList);
        }
    }
}
