using Common.Messaging;
using System.Reflection;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Messages
{
    public record BesiegerCampPropertyChanged : IEvent
    {
        public PropertyInfo PropertyInfo;
        public BesiegerCamp BesiegerCamp;
        public object Value;

        public BesiegerCampPropertyChanged(PropertyInfo propertyInfo, BesiegerCamp instance, object value)
        {
            this.PropertyInfo = propertyInfo;
            this.BesiegerCamp = instance;
            this.Value = value;
        }
    }
}