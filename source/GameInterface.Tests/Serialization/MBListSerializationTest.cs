using Autofac;
using Common.Serialization;
using GameInterface.Serialization;
using GameInterface.Serialization.Generics;
using GameInterface.Tests.Bootstrap.Modules;
using System.Collections.Generic;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MBListSerializationTest
    {
        ILifetimeScope container;
        public MBListSerializationTest()
        {
            container = SerializationTestContainer.BeginScope();
        }

        [Fact]
        public void MBList_Full_Serialization()
        {
            List<int> ints = new List<int> { 1, 2, 3, 4, 5 };

            MBReadOnlyList<int> MBReadOnlyList = new MBReadOnlyList<int>(ints);

            var factory = container.Resolve<IBinaryPackageFactory>();
            MBReadOnlyListBinaryPackage package = new MBReadOnlyListBinaryPackage(MBReadOnlyList, factory);

            package.Pack();

            byte[] bytes = BinaryPackageSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryPackageSerializer.Deserialize(bytes);

            Assert.IsType<MBReadOnlyListBinaryPackage>(obj);

            MBReadOnlyListBinaryPackage returnedPackage = (MBReadOnlyListBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            MBReadOnlyList<int> newMBReadOnlyList = returnedPackage.Unpack<MBReadOnlyList<int>>(deserializeFactory);

            Assert.Equal(MBReadOnlyList, newMBReadOnlyList);
        }
    }
}
