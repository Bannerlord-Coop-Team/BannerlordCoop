using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.VillageMarketDatas.Messages
{
    /// <summary>
    /// An event published to clients, commanding them to create VillageMarketData.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateVillageMarketData : ICommand
    {
        [ProtoMember(1)]
        public string MarketDataId { get; }

        public NetworkCreateVillageMarketData(string marketDataId)
        {
            MarketDataId = marketDataId;
        }
    }
}
