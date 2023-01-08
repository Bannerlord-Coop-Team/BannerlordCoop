using GameInterface.Serialization;
using GameInterface.Serialization.External;
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
            tradeItemComponent.GetType().GetProperty("MoraleBonus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).SetValue(tradeItemComponent, 5);

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
