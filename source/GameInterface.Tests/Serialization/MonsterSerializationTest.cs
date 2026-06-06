using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using Xunit;
using Common.Serialization;
using GameInterface.Services.ObjectManager;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class MonsterSerializationTest
    {
        IContainer container;
        public MonsterSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Monster_Serialize()
        {
            Monster testMonster = (Monster)FormatterServices.GetUninitializedObject(typeof(Monster));

            var factory = container.Resolve<IBinaryPackageFactory>();
            MonsterBinaryPackage package = new MonsterBinaryPackage(testMonster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Monster_Full_Serialization()
        {
            Monster testMonster = (Monster)FormatterServices.GetUninitializedObject(typeof(Monster));
            testMonster.StringId = "testMonster";

            var factory = container.Resolve<IBinaryPackageFactory>();
            var objectManager = container.Resolve<IObjectManager>();

            objectManager.AddExisting(testMonster.StringId, testMonster);

            MonsterBinaryPackage package = new MonsterBinaryPackage(testMonster, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<MonsterBinaryPackage>(obj);

            MonsterBinaryPackage returnedPackage = (MonsterBinaryPackage)obj;

            Assert.Equal(returnedPackage.StringId, package.StringId);
        }
    }
}
