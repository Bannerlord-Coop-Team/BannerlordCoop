using Autofac;
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
        IContainer container;
        public MBListSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void MBList_Serialize()
        {
            List<int> ints = new List<int> { 1, 2, 3, 4, 5 };

            MBList<int> MBReadOnlyList = new MBList<int>(ints);

            var factory = container.Resolve<IBinaryPackageFactory>();
            MBReadOnlyListBinaryPackage package = new MBReadOnlyListBinaryPackage(MBReadOnlyList, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void MBList_Full_Serialization()
        {
            List<int> ints = new List<int> { 1, 2, 3, 4, 5 };

            MBReadOnlyList<int> MBReadOnlyList = new MBReadOnlyList<int>(ints);

            var factory = container.Resolve<IBinaryPackageFactory>();
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
