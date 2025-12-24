using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageMarketDatas.Messages
{
    /// <summary>
    /// An event that is published internally when VillageMarketData is created.
    /// </summary>
    internal class VillageMarketDataCreated : IEvent
    {
        public VillageMarketDataCreated(VillageMarketData data)
        {
            Data = data;
        }

        public VillageMarketData Data { get; }
    }
}
