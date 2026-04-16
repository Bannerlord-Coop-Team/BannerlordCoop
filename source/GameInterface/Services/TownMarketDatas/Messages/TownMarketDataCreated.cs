using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas.Messages
{
    /// <summary>
    /// An event that is published internally when TownMarketData is created.
    /// </summary>
    internal class TownMarketDataCreated : IEvent
    {
        public TownMarketDataCreated(TownMarketData data)
        {
            Data = data;
        }

        public TownMarketData Data { get; }
    }
}
