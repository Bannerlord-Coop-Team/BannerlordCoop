using Common.Messaging;

namespace GameInterface.Services.MobileParties.Patches
{
    public record MobilePartyPropertyChanged : IEvent
    {
        public PropertyType _propertyType;
        public string? value1;
        public string? value2;
        public string? value3;

        public MobilePartyPropertyChanged(PropertyType propertyType, string value1, string value2, string value3 = null)
        {
            _propertyType = propertyType;
            this.value1 = value1;
            this.value2 = value2;
            this.value3 = value3;
        }
    }
}