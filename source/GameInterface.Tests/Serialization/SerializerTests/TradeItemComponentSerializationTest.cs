using GameInterface.Serialization;
using GameInterface.Serialization.Impl;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace GameInterface.Tests.Serialization.SerializerTests
{
    public class TradeItemComponentSerializationTest
    {
        [Fact]
        public void TradeItemComponent_Serialize()
        {
            TradeItemComponent tradeItemComponent = new TradeItemComponent();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TradeItemComponentBinaryPackage package = new TradeItemComponentBinaryPackage(tradeItemComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);
            
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void TradeItemComponent_Full_Serialization()
        {
            TradeItemComponent tradeItemComponent = new TradeItemComponent();

            BinaryPackageFactory factory = new BinaryPackageFactory();
            TradeItemComponentBinaryPackage package = new TradeItemComponentBinaryPackage(tradeItemComponent, factory);

            package.Pack();

            byte[] bytes = BinaryFormatterSerializer.Serialize(package);

            Assert.NotEmpty(bytes);

            object obj = BinaryFormatterSerializer.Deserialize(bytes);

            Assert.IsType<TradeItemComponentBinaryPackage>(obj);

            TradeItemComponentBinaryPackage returnedPackage = (TradeItemComponentBinaryPackage)obj;

            TradeItemComponent newtradeItemComponent = returnedPackage.Unpack<TradeItemComponent>();

            Assert.Equal(tradeItemComponent.MoraleBonus, newtradeItemComponent.MoraleBonus);
        }
    }
}
