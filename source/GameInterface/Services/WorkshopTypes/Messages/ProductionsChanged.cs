using Common.Messaging;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.WorkshopTypes.Messages
{
    internal record ProductionsChanged : IEvent
    {
        public WorkshopType workshopType;
        public WorkshopType.Production production;
        public bool isAdd;

        public ProductionsChanged(WorkshopType workshopType, WorkshopType.Production production, bool isAdd)
        {
            this.workshopType = workshopType;
            this.production = production;
            this.isAdd = isAdd;
        }
    }
}
