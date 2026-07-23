using Common.Messaging;
using GameInterface.Services.Workshops.Patches;
using TaleWorlds.CampaignSystem.Settlements.Workshops;

namespace GameInterface.Services.Workshops.Messages
{
    public record WorkshopPropertyChanged : IEvent
    {
        public PropertyType _propertyType;
        public Workshop workshop;
        public string mainData;
        public string extraData;

        public WorkshopPropertyChanged(PropertyType propertyType, Workshop _workshop, string _mainData, string _extraData = null)
        {
            _propertyType = propertyType;
            workshop = _workshop;
            mainData = _mainData;
            extraData = _extraData;
        }
    }
}
