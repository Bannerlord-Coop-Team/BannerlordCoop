using GameInterface.Serialization;
using System.Collections.Generic;
using Xunit;
using TaleWorlds.CampaignSystem;
using GameInterface.Serialization.Native;
using Autofac;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class DictionarySerializerTest
    {
        IContainer container;
        public DictionarySerializerTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void Dictionary_Serialize()
        {
            Dictionary<string, CampaignTime> Dict = new Dictionary<string, CampaignTime>
            {
                { "1", new CampaignTime() },
                { "2", new CampaignTime() },
                { "3", new CampaignTime() },
            };

            var factory = container.Resolve<IBinaryPackageFactory>();
            DictionaryBinaryPackage package = new DictionaryBinaryPackage(Dict, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Dictionary_Full_Serialization()
        {
            Dictionary<string, CampaignTime> Dict = new Dictionary<string, CampaignTime>
            {
                { "1", new CampaignTime() },
                { "2", new CampaignTime() },
                { "3", new CampaignTime() },
            };

            Dictionary<string, CampaignTime> Dict2 = new Dictionary<string, CampaignTime>(Dict);

            var factory = container.Resolve<IBinaryPackageFactory>();
            DictionaryBinaryPackage package = new DictionaryBinaryPackage(Dict, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<DictionaryBinaryPackage>(obj);

            DictionaryBinaryPackage returnedPackage = (DictionaryBinaryPackage)obj;

            Dictionary<string, CampaignTime> newDict = returnedPackage.Unpack<Dictionary<string, CampaignTime>>();

            Assert.Equal(Dict, newDict);
        }
    }
}
