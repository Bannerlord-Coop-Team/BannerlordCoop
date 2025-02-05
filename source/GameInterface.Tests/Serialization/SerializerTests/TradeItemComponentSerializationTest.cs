using Autofac;
using GameInterface.Serialization;
using GameInterface.Serialization.External;
using GameInterface.Tests.Bootstrap.Modules;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.Core;
using Xunit;
using Common.Serialization;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TradeItemComponentSerializationTest
    {
        IContainer container;
        public TradeItemComponentSerializationTest()
        {
            ContainerBuilder builder = new ContainerBuilder();

            builder.RegisterModule<SerializationTestModule>();

            container = builder.Build();
        }

        [Fact]
        public void TradeItemComponent_Serialize()
        {
            TradeItemComponent tradeItemComponent = new TradeItemComponent();

            var factory = container.Resolve<IBinaryPackageFactory>();
            TradeItemComponentBinaryPackage package = new TradeItemComponentBinaryPackage(tradeItemComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
            
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void TradeItemComponent_Full_Serialization()
        {
            TradeItemComponent tradeItemComponent = new TradeItemComponent();
            tradeItemComponent.MoraleBonus = 5;

            var factory = container.Resolve<IBinaryPackageFactory>();
            TradeItemComponentBinaryPackage package = new TradeItemComponentBinaryPackage(tradeItemComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
                
            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TradeItemComponentBinaryPackage>(obj);

            TradeItemComponentBinaryPackage returnedPackage = (TradeItemComponentBinaryPackage)obj;

            var deserializeFactory = container.Resolve<IBinaryPackageFactory>();
            TradeItemComponent newtradeItemComponent = returnedPackage.Unpack<TradeItemComponent>(deserializeFactory);

            Assert.Equal(tradeItemComponent.MoraleBonus, newtradeItemComponent.MoraleBonus);
        }
    }
}
