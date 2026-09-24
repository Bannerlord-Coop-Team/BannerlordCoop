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
        [ProtoMember(2)]
        public uint Handle { get; }

        public NetworkCreateVillageMarketData(string marketDataId, uint handle)
        {
            MarketDataId = marketDataId;
            Handle = handle;
        }
    }
}
