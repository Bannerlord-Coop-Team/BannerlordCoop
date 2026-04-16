using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TownMarketDatas.Messages
{
    /// <summary>
    /// An event published to clients, commanding them to create TownMarketData.
    /// </summary>
    [ProtoContract(SkipConstructor = true)]
    internal class NetworkCreateTownMarketData : ICommand
    {
        [ProtoMember(1)]
        public string MarketDataId { get; }

        public NetworkCreateTownMarketData(string marketDataId)
        {
            MarketDataId = marketDataId;
        }
    }
}
